namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private string ApplyFullConfigTemplate()
    {
        var fullConfigTemplate = context.FullConfigTemplate;
        if (fullConfigTemplate is not { Enabled: true })
        {
            return JsonUtils.Serialize(_coreConfig);
        }

        var fullConfigTemplateItem = context.IsTunEnabled ? fullConfigTemplate.TunConfig : fullConfigTemplate.Config;
        if (fullConfigTemplateItem.IsNullOrEmpty())
        {
            return JsonUtils.Serialize(_coreConfig);
        }

        var fullConfigTemplateNode = JsonNode.Parse(fullConfigTemplateItem);
        if (fullConfigTemplateNode == null)
        {
            return JsonUtils.Serialize(_coreConfig);
        }

        // Process outbounds
        var customOutboundsNode = fullConfigTemplateNode["outbounds"] is JsonArray outbounds ? outbounds : [];
        foreach (var outbound in _coreConfig.outbounds)
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
        if (_coreConfig.endpoints != null && _coreConfig.endpoints.Count > 0)
        {
            var customEndpointsNode = fullConfigTemplateNode["endpoints"] is JsonArray endpoints ? endpoints : [];
            foreach (var endpoint in _coreConfig.endpoints)
            {
                if (endpoint.detour.IsNullOrEmpty() && !fullConfigTemplate.ProxyDetour.IsNullOrEmpty())
                {
                    endpoint.detour = fullConfigTemplate.ProxyDetour;
                }
                customEndpointsNode.Add(JsonUtils.DeepCopy(endpoint));
            }
            fullConfigTemplateNode["endpoints"] = customEndpointsNode;
        }

        return JsonUtils.Serialize(fullConfigTemplateNode);
    }
}
