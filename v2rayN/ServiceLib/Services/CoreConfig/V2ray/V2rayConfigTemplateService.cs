namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private string ApplyFullConfigTemplate()
    {
        var fullConfigTemplate = context.FullConfigTemplate;
        if (fullConfigTemplate == null || !fullConfigTemplate.Enabled || fullConfigTemplate.Config.IsNullOrEmpty())
        {
            return JsonUtils.Serialize(_coreConfig);
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplate.Config);
        if (fullConfigTemplateNode == null)
        {
            return JsonUtils.Serialize(_coreConfig);
        }

        // Handle balancer and rules modifications (for multiple load scenarios)
        if (_coreConfig.routing?.balancers?.Count > 0)
        {
            var balancer = _coreConfig.routing.balancers.First();

            // Modify existing rules in custom config
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

            // Ensure routing node exists
            if (fullConfigTemplateNode["routing"] == null)
            {
                fullConfigTemplateNode["routing"] = new JsonObject();
            }

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
                subjectSelector.AddRange(fullConfigTemplateNode["observatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["observatory"]["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector.Distinct().ToList()));
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
                subjectSelector.AddRange(fullConfigTemplateNode["burstObservatory"]?["subjectSelector"]?.AsArray()?.Select(x => x?.GetValue<string>()) ?? []);
                fullConfigTemplateNode["burstObservatory"]["subjectSelector"] = JsonNode.Parse(JsonUtils.Serialize(subjectSelector.Distinct().ToList()));
            }
        }

        var customOutboundsNode = new JsonArray();

        foreach (var outbound in _coreConfig.outbounds)
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

        return JsonUtils.Serialize(fullConfigTemplateNode);
    }
}
