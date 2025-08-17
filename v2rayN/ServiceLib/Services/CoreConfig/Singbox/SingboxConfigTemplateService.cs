using System.Text.Json.Nodes;

namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<string> ApplyFullConfigTemplate(SingboxConfig singboxConfig)
    {
        var fullConfigTemplate = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.sing_box);
        if (fullConfigTemplate == null || !fullConfigTemplate.Enabled)
        {
            return JsonUtils.Serialize(singboxConfig);
        }

        var fullConfigTemplateItem = _config.TunModeItem.EnableTun ? fullConfigTemplate.TunConfig : fullConfigTemplate.Config;
        if (fullConfigTemplateItem.IsNullOrEmpty())
        {
            return JsonUtils.Serialize(singboxConfig);
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplateItem);
        if (fullConfigTemplateNode == null)
        {
            return JsonUtils.Serialize(singboxConfig);
        }

        // Process outbounds
        var customOutboundsNode = fullConfigTemplateNode["outbounds"] is JsonArray outbounds ? outbounds : new JsonArray();
        foreach (var outbound in singboxConfig.outbounds)
        {
            if (outbound.type.ToLower() is "direct" or "block")
            {
                if (fullConfigTemplate.AddProxyOnly == true)
                {
                    continue;
                }
            }
            else if (outbound.detour.IsNullOrEmpty() && !fullConfigTemplate.ProxyDetour.IsNullOrEmpty() && !Utils.IsPrivateNetwork(outbound.server ?? string.Empty))
            {
                outbound.detour = fullConfigTemplate.ProxyDetour;
            }
            customOutboundsNode.Add(JsonUtils.DeepCopy(outbound));
        }
        fullConfigTemplateNode["outbounds"] = customOutboundsNode;

        // Process endpoints
        if (singboxConfig.endpoints != null && singboxConfig.endpoints.Count > 0)
        {
            var customEndpointsNode = fullConfigTemplateNode["endpoints"] is JsonArray endpoints ? endpoints : new JsonArray();
            foreach (var endpoint in singboxConfig.endpoints)
            {
                if (endpoint.detour.IsNullOrEmpty() && !fullConfigTemplate.ProxyDetour.IsNullOrEmpty())
                {
                    endpoint.detour = fullConfigTemplate.ProxyDetour;
                }
                customEndpointsNode.Add(JsonUtils.DeepCopy(endpoint));
            }
            fullConfigTemplateNode["endpoints"] = customEndpointsNode;
        }

        return await Task.FromResult(JsonUtils.Serialize(fullConfigTemplateNode));
    }
}
