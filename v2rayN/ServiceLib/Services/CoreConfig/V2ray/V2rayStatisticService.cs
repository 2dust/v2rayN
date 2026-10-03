namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenStatistic()
    {
        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            Metrics4Ray metricsObj = new();
            Policy4Ray policyObj = new();
            SystemPolicy4Ray policySystemSetting = new();

            _coreConfig.stats = new Stats4Ray();

            metricsObj.listen = $"{Global.Loopback}:{AppManager.Instance.StatePort}";
            _coreConfig.metrics = metricsObj;

            policySystemSetting.statsOutboundDownlink = true;
            policySystemSetting.statsOutboundUplink = true;
            policyObj.system = policySystemSetting;
            _coreConfig.policy = policyObj;
        }
    }
}
