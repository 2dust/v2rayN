namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenBalancer(V2rayConfig v2rayConfig, EMultipleLoad multipleLoad)
    {
        if (multipleLoad == EMultipleLoad.LeastPing)
        {
            var observatory = new Observatory4Ray
            {
                subjectSelector = [Global.ProxyTag],
                probeUrl = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl,
                probeInterval = "3m",
                enableConcurrency = true,
            };
            v2rayConfig.observatory = observatory;
        }
        else if (multipleLoad is EMultipleLoad.LeastLoad or EMultipleLoad.Fallback)
        {
            var burstObservatory = new BurstObservatory4Ray
            {
                subjectSelector = [Global.ProxyTag],
                pingConfig = new()
                {
                    destination = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl,
                    interval = "5m",
                    timeout = "30s",
                    sampling = 2,
                }
            };
            v2rayConfig.burstObservatory = burstObservatory;
        }
        var strategyType = multipleLoad switch
        {
            EMultipleLoad.Random => "random",
            EMultipleLoad.RoundRobin => "roundRobin",
            EMultipleLoad.LeastPing => "leastPing",
            EMultipleLoad.LeastLoad => "leastLoad",
            _ => "roundRobin",
        };
        var balancer = new BalancersItem4Ray
        {
            selector = [Global.ProxyTag],
            strategy = new()
            {
                type = strategyType,
                settings = new()
                {
                    expected = 1,
                },
            },
            tag = $"{Global.ProxyTag}-round",
        };
        v2rayConfig.routing.balancers = [balancer];
        return await Task.FromResult(0);
    }
}
