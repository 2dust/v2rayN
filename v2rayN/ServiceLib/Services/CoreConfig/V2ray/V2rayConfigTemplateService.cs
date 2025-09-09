using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<string> ApplyFullConfigTemplate(V2rayConfig v2rayConfig, bool handleBalancerAndRules = false)
    {
        var fullConfigTemplate = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.Xray);
        if (fullConfigTemplate == null || !fullConfigTemplate.Enabled || fullConfigTemplate.Config.IsNullOrEmpty())
        {
            return JsonUtils.Serialize(v2rayConfig);
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplate.Config);
        if (fullConfigTemplateNode == null)
        {
            return JsonUtils.Serialize(v2rayConfig);
        }

        // Handle balancer and rules modifications (for multiple load scenarios)
        if (handleBalancerAndRules && v2rayConfig.routing?.balancers?.Count > 0)
        {
            var balancer = v2rayConfig.routing.balancers.First();

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

        // Handle outbounds - append instead of override
        var customOutboundsNode = fullConfigTemplateNode["outbounds"] is JsonArray outbounds ? outbounds : new JsonArray();
        foreach (var outbound in v2rayConfig.outbounds)
        {
            if (outbound.protocol.ToLower() is "blackhole" or "dns" or "freedom")
            {
                if (fullConfigTemplate.AddProxyOnly == true)
                {
                    continue;
                }
            }
            else if ((outbound.streamSettings?.sockopt?.dialerProxy.IsNullOrEmpty() == true) && (!fullConfigTemplate.ProxyDetour.IsNullOrEmpty()) && !(Utils.IsPrivateNetwork(outbound.settings?.servers?.FirstOrDefault()?.address ?? string.Empty) || Utils.IsPrivateNetwork(outbound.settings?.vnext?.FirstOrDefault()?.address ?? string.Empty)))
            {
                outbound.streamSettings ??= new StreamSettings4Ray();
                outbound.streamSettings.sockopt ??= new Sockopt4Ray();
                outbound.streamSettings.sockopt.dialerProxy = fullConfigTemplate.ProxyDetour;
            }
            customOutboundsNode.Add(JsonUtils.DeepCopy(outbound));
        }
        fullConfigTemplateNode["outbounds"] = customOutboundsNode;

        return await Task.FromResult(JsonUtils.Serialize(fullConfigTemplateNode));
    }
}
