namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenStatistic()
    {
        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            var tag = EInboundProtocol.api.ToString();
            Metrics4Ray apiObj = new();
            Policy4Ray policyObj = new();
            SystemPolicy4Ray policySystemSetting = new();

            _coreConfig.stats = new Stats4Ray();

            apiObj.tag = tag;
            _coreConfig.metrics = apiObj;

            policySystemSetting.statsOutboundDownlink = true;
            policySystemSetting.statsOutboundUplink = true;
            policyObj.system = policySystemSetting;
            _coreConfig.policy = policyObj;

            if (!_coreConfig.inbounds.Exists(item => item.tag == tag))
            {
                Inbounds4Ray apiInbound = new();
                Inboundsettings4Ray apiInboundSettings = new();
                apiInbound.tag = tag;
                apiInbound.listen = Global.Loopback;
                apiInbound.port = AppManager.Instance.StatePort;
                apiInbound.protocol = Global.InboundAPIProtocol;
                apiInboundSettings.address = Global.Loopback;
                apiInbound.settings = apiInboundSettings;
                _coreConfig.inbounds.Add(apiInbound);
            }

            if (!_coreConfig.routing.rules.Exists(item => item.outboundTag == tag))
            {
                RulesItem4Ray apiRoutingRule = new()
                {
                    inboundTag = new List<string> { tag },
                    outboundTag = tag,
                    type = "field"
                };

                _coreConfig.routing.rules.Add(apiRoutingRule);
            }
        }
    }
}
