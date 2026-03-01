namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenObservatory(EMultipleLoad multipleLoad, string baseTagName = Global.ProxyTag)
    {
        // Collect all existing subject selectors from both observatories
        var subjectSelectors = new List<string>();
        subjectSelectors.AddRange(_coreConfig.burstObservatory?.subjectSelector ?? []);
        subjectSelectors.AddRange(_coreConfig.observatory?.subjectSelector ?? []);

        // Case 1: exact match already exists -> nothing to do
        if (subjectSelectors.Any(baseTagName.StartsWith))
        {
            return;
        }

        // Case 2: prefix match exists -> reuse it and move to the first position
        var matched = subjectSelectors.FirstOrDefault(s => s.StartsWith(baseTagName));
        if (matched is not null)
        {
            baseTagName = matched;

            if (_coreConfig.burstObservatory?.subjectSelector?.Contains(baseTagName) == true)
            {
                _coreConfig.burstObservatory.subjectSelector.Remove(baseTagName);
                _coreConfig.burstObservatory.subjectSelector.Insert(0, baseTagName);
            }

            if (_coreConfig.observatory?.subjectSelector?.Contains(baseTagName) == true)
            {
                _coreConfig.observatory.subjectSelector.Remove(baseTagName);
                _coreConfig.observatory.subjectSelector.Insert(0, baseTagName);
            }

            return;
        }

        // Case 3: need to create or insert based on multipleLoad type
        if (multipleLoad is EMultipleLoad.LeastLoad or EMultipleLoad.Fallback)
        {
            if (_coreConfig.burstObservatory is null)
            {
                // Create new burst observatory with default ping config
                _coreConfig.burstObservatory = new BurstObservatory4Ray
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
                _coreConfig.burstObservatory.subjectSelector ??= new();
                _coreConfig.burstObservatory.subjectSelector.Add(baseTagName);
            }
        }
        else if (multipleLoad is EMultipleLoad.LeastPing)
        {
            if (_coreConfig.observatory is null)
            {
                // Create new observatory with default probe config
                _coreConfig.observatory = new Observatory4Ray
                {
                    subjectSelector = [baseTagName],
                    probeUrl = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl,
                    probeInterval = "3m",
                    enableConcurrency = true,
                };
            }
            else
            {
                _coreConfig.observatory.subjectSelector ??= new();
                _coreConfig.observatory.subjectSelector.Add(baseTagName);
            }
        }
    }

    private void GenBalancer(EMultipleLoad multipleLoad, string selector = Global.ProxyTag)
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
        _coreConfig.routing.balancers ??= new();
        _coreConfig.routing.balancers.Add(balancer);
    }
}
