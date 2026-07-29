namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private string ApplyFinalConfigModifiers()
    {
        ApplyOutboundBindInterface();
        ApplyOutboundSendThrough();

        var coreConfigContent = ApplyCustomOutboundReplace();

        return ApplyFullConfigTemplate(coreConfigContent);
    }

    private string ApplyCustomOutboundReplace()
    {
        var coreConfigContent = JsonUtils.Serialize(_coreConfig);
        if (context.CustomOutboundMap.Count == 0)
        {
            return coreConfigContent;
        }
        var coreConfigNode = JsonNode.Parse(coreConfigContent) as JsonObject;
        var coreConfigOutboundsNode = coreConfigNode?["outbounds"] as JsonArray ?? [];

        foreach (var outbound in _coreConfig.outbounds ?? [])
        {
            if (!context.CustomOutboundMap.TryGetValue(outbound, out var customOutboundIndex))
            {
                continue;
            }
            var outboundTag = outbound.tag;
            var outboundDetour = outbound.streamSettings?.sockopt?.dialerProxy ?? string.Empty;
            var outboundBindInterface = outbound.streamSettings?.sockopt?.Interface ?? string.Empty;
            var customOutboundContent = context.CustomOutboundContent[customOutboundIndex];
            var containTagPlaceholder = customOutboundContent.Contains("{{tag}}");
            var containDetourPlaceholder = customOutboundContent.Contains("{{detour}}");
            var containBindInterfacePlaceholder = customOutboundContent.Contains("{{interface}}");
            customOutboundContent = customOutboundContent.Replace("{{tag}}", outboundTag);
            customOutboundContent = customOutboundContent.Replace("{{detour}}", outboundDetour);
            customOutboundContent = customOutboundContent.Replace("{{interface}}", outboundBindInterface);
            var customOutboundObj = JsonUtils.ParseJson(customOutboundContent) as JsonObject;

            if (!containTagPlaceholder)
            {
                customOutboundObj?["tag"] = outboundTag;
            }
            if (!containDetourPlaceholder && !outboundDetour.IsNullOrEmpty())
            {
                customOutboundObj!["streamSettings"] ??= new JsonObject();
                customOutboundObj["streamSettings"]["sockopt"] ??= new JsonObject();
                customOutboundObj["streamSettings"]["sockopt"]["dialerProxy"] = outboundDetour;
                if (customOutboundObj["streamSettings"]?["xhttpSettings"]?["extra"]?["downloadSettings"] is JsonObject downloadSettings)
                {
                    downloadSettings["sockopt"] ??= new JsonObject();
                    downloadSettings["sockopt"]["dialerProxy"] = outboundDetour;
                }
            }
            else if (outboundDetour.IsNullOrEmpty())
            {
                (customOutboundObj?["streamSettings"]?["sockopt"] as JsonObject)?.Remove("dialerProxy");
            }
            if (!containBindInterfacePlaceholder && !outboundBindInterface.IsNullOrEmpty())
            {
                customOutboundObj!["streamSettings"] ??= new JsonObject();
                customOutboundObj["streamSettings"]["sockopt"] ??= new JsonObject();
                customOutboundObj["streamSettings"]["sockopt"]["interface"] = outboundBindInterface;
                if (customOutboundObj["streamSettings"]?["xhttpSettings"]?["extra"]?["downloadSettings"] is JsonObject downloadSettings)
                {
                    downloadSettings["sockopt"] ??= new JsonObject();
                    downloadSettings["sockopt"]["interface"] = outboundBindInterface;
                }
            }

            var index = coreConfigOutboundsNode
                .Select((node, idx) => new { node, idx })
                .FirstOrDefault(x => x.node?["tag"]?.ToString() == outboundTag)?.idx ?? -1;
            if (index != -1)
            {
                coreConfigOutboundsNode[index] = customOutboundObj;
            }
        }

        return JsonUtils.Serialize(coreConfigNode);
    }

    private string ApplyFullConfigTemplate(string coreConfigContent)
    {
        var fullConfigTemplate = context.FullConfigTemplate;
        if (fullConfigTemplate is not { Enabled: true })
        {
            return coreConfigContent;
        }

        var fullConfigTemplateItem = context.IsTunEnabled ? fullConfigTemplate.TunConfig : fullConfigTemplate.Config;
        if (fullConfigTemplateItem.IsNullOrEmpty())
        {
            return coreConfigContent;
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplateItem);
        if (fullConfigTemplateNode == null)
        {
            return coreConfigContent;
        }

        // Handle balancer and rules modifications (for multiple load scenarios)
        if (_coreConfig.routing?.balancers?.Count > 0)
        {
            var balancer =
                _coreConfig.routing.balancers.FirstOrDefault(b => b.tag == Global.ProxyTag + Global.BalancerTagSuffix, null);

            // Modify existing rules in custom config
            if (balancer != null)
            {
                var rulesNode = fullConfigTemplateNode["routing"]?["rules"];
                if (rulesNode != null)
                {
                    foreach (var rule in rulesNode.AsArray())
                    {
                        if (rule["outboundTag"]?.GetValue<string>() == Global.ProxyTag)
                        {
                            rule.AsObject().Remove("outboundTag");
                            rule["balancerTag"] = balancer.tag;
                        }
                    }
                }
            }

            // Ensure routing node exists
            fullConfigTemplateNode["routing"] ??= new JsonObject();

            // Handle balancers - append instead of override
            if (fullConfigTemplateNode["routing"]["balancers"] is JsonArray customBalancersNode)
            {
                if (JsonNode.Parse(JsonUtils.Serialize(_coreConfig.routing.balancers)) is JsonArray newBalancers)
                {
                    foreach (var balancerNode in newBalancers)
                    {
                        customBalancersNode.Add(balancerNode?.DeepClone());
                    }
                }
            }
            else
            {
                fullConfigTemplateNode["routing"]["balancers"] = JsonNode.Parse(JsonUtils.Serialize(_coreConfig.routing.balancers));
            }
        }

        if (_coreConfig.observatory != null)
        {
            if (fullConfigTemplateNode["observatory"] == null)
            {
                fullConfigTemplateNode["observatory"] = JsonNode.Parse(JsonUtils.Serialize(_coreConfig.observatory));
            }
            else
            {
                var subjectSelector = _coreConfig.observatory.subjectSelector;
                subjectSelector?.AddRange(fullConfigTemplateNode["observatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["observatory"]?["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector?.Distinct().ToList()));
            }
        }

        if (_coreConfig.burstObservatory != null)
        {
            if (fullConfigTemplateNode["burstObservatory"] == null)
            {
                fullConfigTemplateNode["burstObservatory"] = JsonNode.Parse(JsonUtils.Serialize(_coreConfig.burstObservatory));
            }
            else
            {
                var subjectSelector = _coreConfig.burstObservatory.subjectSelector;
                subjectSelector?.AddRange(fullConfigTemplateNode["burstObservatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["burstObservatory"]?["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector?.Distinct().ToList()));
            }
        }

        var customOutboundsNode = new JsonArray();

        var coreConfigNode = JsonNode.Parse(coreConfigContent);
        var coreConfigOutboundsNode = coreConfigNode?["outbounds"] as JsonArray ?? [];
        foreach (var outbound in coreConfigOutboundsNode)
        {
            if (outbound?["protocol"]?.ToString()?.ToLower() is "blackhole" or "dns" or "freedom")
            {
                if (fullConfigTemplate.AddProxyOnly == true)
                {
                    continue;
                }
            }
            else if (!fullConfigTemplate.ProxyDetour.IsNullOrEmpty()
                && (outbound["streamSettings"]?["sockopt"]?["dialerProxy"].ToString().IsNullOrEmpty() ?? true))
            {
                var outboundAddress = outbound["settings"]?["servers"]?.AsArray()?.FirstOrDefault()?["address"]?.ToString()
                    ?? outbound["settings"]?["vnext"]?.AsArray()?.FirstOrDefault()?["address"]?.ToString()
                    ?? string.Empty;
                if (!Utils.IsPrivateNetwork(outboundAddress))
                {
                    //FillDialerProxy(outbound, fullConfigTemplate.ProxyDetour);
                    outbound["streamSettings"] ??= new JsonObject();
                    outbound["streamSettings"]["sockopt"] ??= new JsonObject();
                    outbound["streamSettings"]["sockopt"]["dialerProxy"] = fullConfigTemplate.ProxyDetour;
                    if (outbound["streamSettings"]?["xhttpSettings"]?["extra"]?["downloadSettings"] is JsonObject downloadSettings)
                    {
                        downloadSettings["sockopt"] ??= new JsonObject();
                        downloadSettings["sockopt"]["dialerProxy"] = fullConfigTemplate.ProxyDetour;
                    }
                }
            }
            customOutboundsNode.Add(JsonUtils.DeepCopy(outbound));
        }

        if (fullConfigTemplateNode["outbounds"] is JsonArray templateOutbounds)
        {
            foreach (var outbound in templateOutbounds)
            {
                customOutboundsNode.Add(outbound?.DeepClone());
            }
        }

        fullConfigTemplateNode["outbounds"] = customOutboundsNode;

        return JsonUtils.Serialize(fullConfigTemplateNode);
    }

    private void ApplyOutboundBindInterface()
    {
        var bindInterface = _config.CoreBasicItem.BindInterface?.TrimEx();
        if (bindInterface.IsNullOrEmpty())
        {
            return;
        }
        foreach (var outbound in _coreConfig.outbounds ?? [])
        {
            if (!ShouldBindNet(outbound))
            {
                continue;
            }
            outbound.streamSettings ??= new();
            outbound.streamSettings.sockopt ??= new();
            outbound.streamSettings.sockopt.Interface = bindInterface;
            // xhttp download bind interface
            if (outbound?.streamSettings?.xhttpSettings?.extra is null)
            {
                continue;
            }
            var xhttpExtra = JsonUtils.ParseJson(JsonUtils.Serialize(outbound.streamSettings.xhttpSettings!.extra));
            if (xhttpExtra is not JsonObject xhttpExtraObject
                || xhttpExtraObject["downloadSettings"] is not JsonObject downloadSettings)
            {
                continue;
            }
            var sockopt = downloadSettings["sockopt"] as JsonObject ?? new JsonObject();
            sockopt["interface"] = bindInterface;
            downloadSettings["sockopt"] = sockopt;
            outbound.streamSettings.xhttpSettings.extra = xhttpExtraObject;
        }
    }

    private void ApplyOutboundSendThrough()
    {
        var sendThrough = _config.CoreBasicItem.SendThrough?.TrimEx();
        if (sendThrough.IsNullOrEmpty())
        {
            return;
        }

        foreach (var outbound in _coreConfig.outbounds ?? [])
        {
            outbound.sendThrough = ShouldBindNet(outbound) ? sendThrough : null;
        }
    }

    private static bool ShouldBindNet(Outbounds4Ray outbound)
    {
        if (outbound.protocol is "freedom" or "blackhole" or "dns" or "loopback")
        {
            return false;
        }

        if (outbound.streamSettings?.sockopt?.dialerProxy.IsNullOrEmpty() == false)
        {
            return false;
        }

        var outboundAddress = outbound.settings?.servers?.FirstOrDefault()?.address
                              ?? outbound.settings?.vnext?.FirstOrDefault()?.address
                              ?? outbound.settings?.address?.ToString()
                              ?? string.Empty;

        if (outboundAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IPAddress.TryParse(outboundAddress, out var address) || !IPAddress.IsLoopback(address);
    }
}
