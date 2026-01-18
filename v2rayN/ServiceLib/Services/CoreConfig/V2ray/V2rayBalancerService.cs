namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private async Task<int> GenObservatory(V2rayConfig v2rayConfig, EMultipleLoad multipleLoad, string baseTagName = AppConfig.ProxyTag)
    {
        // Collect all existing subject selectors from both observatories
        var subjectSelectors = new List<string>();
        subjectSelectors.AddRange(v2rayConfig.burstObservatory?.subjectSelector ?? []);
        subjectSelectors.AddRange(v2rayConfig.observatory?.subjectSelector ?? []);

        // Case 1: exact match already exists -> nothing to do
        if (subjectSelectors.Any(baseTagName.StartsWith))
        {
            return await Task.FromResult(0);
        }

        // Case 2: prefix match exists -> reuse it and move to the first position
        var matched = subjectSelectors.FirstOrDefault(s => s.StartsWith(baseTagName));
        if (matched is not null)
        {
            baseTagName = matched;

            if (v2rayConfig.burstObservatory?.subjectSelector?.Contains(baseTagName) == true)
            {
                v2rayConfig.burstObservatory.subjectSelector.Remove(baseTagName);
                v2rayConfig.burstObservatory.subjectSelector.Insert(0, baseTagName);
            }

            if (v2rayConfig.observatory?.subjectSelector?.Contains(baseTagName) == true)
            {
                v2rayConfig.observatory.subjectSelector.Remove(baseTagName);
                v2rayConfig.observatory.subjectSelector.Insert(0, baseTagName);
            }

            return await Task.FromResult(0);
        }

        // Case 3: need to create or insert based on multipleLoad type
        if (multipleLoad is EMultipleLoad.LeastLoad or EMultipleLoad.Fallback)
        {
            if (v2rayConfig.burstObservatory is null)
            {
                // Create new burst observatory with default ping config
                v2rayConfig.burstObservatory = new BurstObservatory4Ray
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
            }
            else
            {
                v2rayConfig.burstObservatory.subjectSelector ??= new();
                v2rayConfig.burstObservatory.subjectSelector.Add(baseTagName);
            }
        }
        else if (multipleLoad is EMultipleLoad.LeastPing)
        {
            if (v2rayConfig.observatory is null)
            {
                // Create new observatory with default probe config
                v2rayConfig.observatory = new Observatory4Ray
                {
                    subjectSelector = [baseTagName],
                    probeUrl = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl,
                    probeInterval = "3m",
                    enableConcurrency = true,
                };
            }
            else
            {
                v2rayConfig.observatory.subjectSelector ??= new();
                v2rayConfig.observatory.subjectSelector.Add(baseTagName);
            }
        }

        return await Task.FromResult(0);
    }

    private async Task<string> GenBalancer(V2rayConfig v2rayConfig, EMultipleLoad multipleLoad, string selector = AppConfig.ProxyTag)
    {
        var strategyType = multipleLoad switch
        {
            EMultipleLoad.Random => "random",
            EMultipleLoad.RoundRobin => "roundRobin",
            EMultipleLoad.LeastPing => "leastPing",
            EMultipleLoad.LeastLoad => "leastLoad",
            _ => "roundRobin",
        };
        var balancerTag = $"{selector}{AppConfig.BalancerTagSuffix}";
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
