namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<string> ApplyFullConfigTemplate(V2rayConfig v2rayConfig)
    {
        var fullConfigTemplate = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.Xray);
        if (fullConfigTemplate is null || !fullConfigTemplate.Enabled || fullConfigTemplate.Config.IsNullOrEmpty())
        {
            return JsonUtils.Serialize(v2rayConfig);
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplate.Config);
        if (fullConfigTemplateNode is null)
        {
            return JsonUtils.Serialize(v2rayConfig);
        }

        // Handle balancer and rules modifications (for multiple load scenarios)
        if (v2rayConfig.routing?.balancers?.Count > 0)
        {
            var balancer = v2rayConfig.routing.balancers.First();

            // Modify existing rules in custom config
            var rulesNode = fullConfigTemplateNode["routing"]?["rules"];
            if (rulesNode is not null)
            {
                foreach (var rule in rulesNode.AsArray())
                {
                    if (rule["outboundTag"]?.GetValue<string>() == AppConfig.ProxyTag)
                    {
                        rule.AsObject().Remove("outboundTag");
                        rule["balancerTag"] = balancer.tag;
                    }
                }
            }

            // Ensure routing node exists
            if (fullConfigTemplateNode["routing"] is null)
            {
                fullConfigTemplateNode["routing"] = new JsonObject();
            }

            // Handle balancers - append instead of override
            if (fullConfigTemplateNode["routing"]["balancers"] is JsonArray customBalancersNode)
            {
                if (JsonNode.Parse(JsonUtils.Serialize(v2rayConfig.routing.balancers)) is JsonArray newBalancers)
                {
                    foreach (var balancerNode in newBalancers)
                    {
                        customBalancersNode.Add(balancerNode?.DeepClone());
                    }
                }
            }
            else
            {
                fullConfigTemplateNode["routing"]["balancers"] = JsonNode.Parse(JsonUtils.Serialize(v2rayConfig.routing.balancers));
            }
        }

        if (v2rayConfig.observatory is not null)
        {
            if (fullConfigTemplateNode["observatory"] is null)
            {
                fullConfigTemplateNode["observatory"] = JsonNode.Parse(JsonUtils.Serialize(v2rayConfig.observatory));
            }
            else
            {
                var subjectSelector = v2rayConfig.observatory.subjectSelector;
                subjectSelector.AddRange(fullConfigTemplateNode["observatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["observatory"]["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector.Distinct().ToList()));
            }
        }

        if (v2rayConfig.burstObservatory is not null)
        {
            if (fullConfigTemplateNode["burstObservatory"] is null)
            {
                fullConfigTemplateNode["burstObservatory"] = JsonNode.Parse(JsonUtils.Serialize(v2rayConfig.burstObservatory));
            }
            else
            {
                var subjectSelector = v2rayConfig.burstObservatory.subjectSelector;
                subjectSelector.AddRange(fullConfigTemplateNode["burstObservatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["burstObservatory"]["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector.Distinct().ToList()));
            }
        }

        var customOutboundsNode = new JsonArray();

        foreach (var outbound in v2rayConfig.outbounds)
        {
            if (outbound.protocol.ToLower() is "blackhole" or "dns" or "freedom")
            {
                if (fullConfigTemplate.AddProxyOnly == true)
                {
                    continue;
                }
            }
            else if ((!fullConfigTemplate.ProxyDetour.IsNullOrEmpty())
                && ((outbound.streamSettings?.sockopt?.dialerProxy.IsNullOrEmpty() ?? true) == true))
            {
                var outboundAddress = outbound.settings?.servers?.FirstOrDefault()?.address
                    ?? outbound.settings?.vnext?.FirstOrDefault()?.address
                    ?? string.Empty;
                if (!Utils.IsPrivateNetwork(outboundAddress))
                {
                    outbound.streamSettings ??= new StreamSettings4Ray();
                    outbound.streamSettings.sockopt ??= new Sockopt4Ray();
                    outbound.streamSettings.sockopt.dialerProxy = fullConfigTemplate.ProxyDetour;
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

        return await Task.FromResult(JsonUtils.Serialize(fullConfigTemplateNode));
    }
}
