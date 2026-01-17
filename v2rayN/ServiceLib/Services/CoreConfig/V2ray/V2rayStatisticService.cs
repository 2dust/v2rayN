namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenStatistic(V2rayConfig v2rayConfig)
    {
        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            var tag = EInboundProtocol.api.ToString();
            Metrics4Ray apiObj = new();
            Policy4Ray policyObj = new();
            SystemPolicy4Ray policySystemSetting = new();

            v2rayConfig.stats = new Stats4Ray();

            apiObj.tag = tag;
            v2rayConfig.metrics = apiObj;

            policySystemSetting.statsOutboundDownlink = true;
            policySystemSetting.statsOutboundUplink = true;
            policyObj.system = policySystemSetting;
            v2rayConfig.policy = policyObj;

            if (!v2rayConfig.inbounds.Exists(item => item.tag == tag))
            {
                Inbounds4Ray apiInbound = new();
                Inboundsettings4Ray apiInboundSettings = new();
                apiInbound.tag = tag;
                apiInbound.listen = AppConfig.Loopback;
                apiInbound.port = AppManager.Instance.StatePort;
                apiInbound.protocol = AppConfig.InboundAPIProtocol;
                apiInboundSettings.address = AppConfig.Loopback;
                apiInbound.settings = apiInboundSettings;
                v2rayConfig.inbounds.Add(apiInbound);
            }

            if (!v2rayConfig.routing.rules.Exists(item => item.outboundTag == tag))
            {
                RulesItem4Ray apiRoutingRule = new()
                {
                    inboundTag = new List<string> { tag },
                    outboundTag = tag,
                    type = "field"
                };

                v2rayConfig.routing.rules.Add(apiRoutingRule);
            }
        }
        return await Task.FromResult(0);
    }
}
