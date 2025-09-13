namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenObservatory(V2rayConfig v2rayConfig, EMultipleLoad multipleLoad, string baseTagName = Global.ProxyTag)
    {
        if (multipleLoad == EMultipleLoad.LeastPing)
        {
            var observatory = new Observatory4Ray
            {
                subjectSelector = [baseTagName],
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
                subjectSelector = [baseTagName],
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
        return await Task.FromResult(0);
    }

    private async Task<string> GenBalancer(V2rayConfig v2rayConfig, EMultipleLoad multipleLoad, string selector = Global.ProxyTag)
    {
        var strategyType = multipleLoad switch
        {
            EMultipleLoad.Random => "random",
            EMultipleLoad.RoundRobin => "roundRobin",
            EMultipleLoad.LeastPing => "leastPing",
            EMultipleLoad.LeastLoad => "leastLoad",
            _ => "roundRobin",
        };
        var balancerTag = $"{selector}{Global.BalancerTagSuffix}";
        var balancer = new BalancersItem4Ray
        {
            selector = [selector],
            strategy = new()
            {
                type = strategyType,
                settings = new()
                {
                    expected = 1,
                },
            },
            tag = balancerTag,
        };
        v2rayConfig.routing.balancers ??= new();
        v2rayConfig.routing.balancers.Add(balancer);
        return await Task.FromResult(balancerTag);
    }
}
