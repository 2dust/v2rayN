namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
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
        ReplaceCustomOutbounds(_coreConfig.outbounds, coreConfigOutboundsNode);
        coreConfigNode!["outbounds"] = coreConfigOutboundsNode;
        var coreConfigEndpointsNode = coreConfigNode?["endpoints"] as JsonArray ?? [];
        ReplaceCustomOutbounds(_coreConfig.endpoints, coreConfigEndpointsNode);
        if (coreConfigEndpointsNode.Count > 0)
        {
            coreConfigNode!["endpoints"] = coreConfigEndpointsNode;
        }
        else
        {
            coreConfigNode?.Remove("endpoints");
        }
        return JsonUtils.Serialize(coreConfigNode);

        void ReplaceCustomOutbounds(IReadOnlyList<BaseServer4Sbox>? source, JsonArray jsonArrayOutbounds)
        {
            foreach (var outbound in source ?? [])
            {
                if (!context.CustomOutboundMap.TryGetValue(outbound, out var customOutboundIndex))
                {
                    continue;
                }
                var outboundTag = outbound.tag;
                var outboundDetour = outbound.detour ?? string.Empty;
                var outboundBindInterface = outbound.bind_interface ?? string.Empty;
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
                    customOutboundObj?["detour"] = outboundDetour;
                }
                else if (outboundDetour.IsNullOrEmpty())
                {
                    customOutboundObj?.Remove("detour");
                }
                if (!containBindInterfacePlaceholder && !outboundBindInterface.IsNullOrEmpty())
                {
                    customOutboundObj?["bind_interface"] = outboundBindInterface;
                }

                var index = jsonArrayOutbounds
                    .Select((node, idx) => new { node, idx })
                    .FirstOrDefault(x => x.node?["tag"]?.ToString() == outboundTag)?.idx ?? -1;
                if (index != -1)
                {
                    jsonArrayOutbounds[index] = customOutboundObj;
                }
            }
        }
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

        // Process outbounds
        var customOutboundsNode = fullConfigTemplateNode["outbounds"] as JsonArray ?? [];
        var coreConfigNode = JsonNode.Parse(coreConfigContent);
        var coreConfigOutboundsNode = coreConfigNode?["outbounds"] as JsonArray ?? [];
        foreach (var outbound in coreConfigOutboundsNode)
        {
            if (outbound["type"]?.ToString()?.ToLower() is "direct" or "block")
            {
                if (fullConfigTemplate.AddProxyOnly == true)
                {
                    continue;
                }
            }
            if (outbound["detour"] is null && !fullConfigTemplate.ProxyDetour.IsNullOrEmpty() && !Utils.IsPrivateNetwork(outbound["server"]?.ToString() ?? string.Empty))
            {
                outbound["detour"] = fullConfigTemplate.ProxyDetour;
            }
            customOutboundsNode.Add(JsonUtils.DeepCopy(outbound));
        }
        fullConfigTemplateNode["outbounds"] = customOutboundsNode;

        // Process endpoints
        if (fullConfigTemplateNode["endpoints"] is JsonArray { Count: > 0 } coreConfigEndpointsNode)
        {
            var customEndpointsNode = fullConfigTemplateNode["endpoints"] as JsonArray ?? [];
            foreach (var endpoint in coreConfigEndpointsNode)
            {
                if (endpoint["detour"] is null && !fullConfigTemplate.ProxyDetour.IsNullOrEmpty())
                {
                    endpoint["detour"] = fullConfigTemplate.ProxyDetour;
                }
                customEndpointsNode.Add(JsonUtils.DeepCopy(endpoint));
            }
            fullConfigTemplateNode["endpoints"] = customEndpointsNode;
        }

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
            outbound.bind_interface = ShouldBindNet(outbound) ? bindInterface : null;
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
            outbound.inet4_bind_address = ShouldBindNet(outbound) ? sendThrough : null;
        }
    }

    private static bool ShouldBindNet(Outbound4Sbox outbound)
    {
        if (outbound.type is "direct" or "block" or "dns" or "selector" or "urltest")
        {
            return false;
        }

        if (!outbound.detour.IsNullOrEmpty())
        {
            return false;
        }

        var outboundAddress = outbound.server ?? string.Empty;

        if (outboundAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IPAddress.TryParse(outboundAddress, out var address) || !IPAddress.IsLoopback(address);
    }
}
