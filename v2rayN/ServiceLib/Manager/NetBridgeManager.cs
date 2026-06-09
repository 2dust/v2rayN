using NetBridgeLib.Services;

namespace ServiceLib.Manager;

public sealed class NetBridgeManager
{
    private static readonly Lazy<NetBridgeManager> _instance = new(() => new());
    public static NetBridgeManager Instance => _instance.Value;
    private readonly Config _config = AppManager.Instance.Config;
    private NetBridgeService? _netBridgeService;
    private bool _isProxyRunning;
    private bool _isInitialized;
    private List<NetBridgeRuleConfig> _ruleConfigs = [];
    private Func<bool, string, Task>? _updateFunc;
    private uint _proxyConfigId;

    public async Task Init(Func<bool, string, Task>? updateFunc = null)
    {
        if (_isInitialized)
        {
            return;
        }

        _updateFunc = updateFunc;

        try
        {
            _netBridgeService = new NetBridgeService();
            _netBridgeService.LogReceived += msg =>
            {
                var message = $"NetBridge Log: {msg}";
                _ = _updateFunc?.Invoke(false, message);
            };

            _netBridgeService.ConnectionReceived += (processName, pid, destIp, destPort, proxyInfo) =>
            {
                var message = $"NetBridge Connection: {processName} (PID: {pid}) -> {destIp}:{destPort} -> {proxyInfo}";
                _ = _updateFunc?.Invoke(false, message);
            };

            _ruleConfigs = BuildRuleConfigs(_config.NetBridgeItem?.RuleProcess);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            var error = $"Failed to initialize NetBridgeService: {ex.Message}";
            await _updateFunc?.Invoke(true, error);
        }
    }

    public async Task<bool> Start()
    {
        if (_isProxyRunning)
        {
            return true;
        }

        try
        {
            if (_netBridgeService == null)
            {
                return false;
            }

            var started = _netBridgeService.Start();
            if (!started)
            {
                return false;
            }

            _isProxyRunning = true;
        }
        catch (Exception ex)
        {
            var error = $"Failed to start NetBridgeService: {ex.Message}";
            await _updateFunc?.Invoke(true, error);
            return false;
        }

        return true;
    }

    public async Task<bool> Stop()
    {
        if (!_isProxyRunning)
        {
            return true;
        }

        try
        {
            if (_netBridgeService == null)
            {
                return false;
            }

            var stopped = _netBridgeService.Stop();
            if (!stopped)
            {
                return false;
            }

            _isProxyRunning = false;
        }
        catch (Exception ex)
        {
            var error = $"Failed to stop NetBridgeService: {ex.Message}";
            await _updateFunc?.Invoke(true, error);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateRoutes(string? ruleProcess)
    {
        var newRuleConfigs = BuildRuleConfigs(ruleProcess);

        _ruleConfigs = newRuleConfigs;

        if (!_isProxyRunning)
        {
            return true;
        }

        return await ApplyRoutesInternal();
    }

    public async Task<bool> UpdateProxyConfig(string proxyHost, int proxyPort)
    {
        try
        {
            if (_netBridgeService == null)
            {
                return false;
            }

            var proxyType = "SOCKS5";
            var username = "";
            var password = "";

            if (_proxyConfigId > 0)
            {
                var edited = _netBridgeService.EditProxyConfig(_proxyConfigId, proxyType, proxyHost, (ushort)proxyPort, username, password);
                if (!edited)
                {
                    return false;
                }
            }
            else
            {
                _proxyConfigId = _netBridgeService.AddProxyConfig(proxyType, proxyHost, (ushort)proxyPort, username, password);
                if (_proxyConfigId == 0)
                {
                    return false;
                }
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            var error = $"Failed to update proxy config: {ex.Message}";
            await _updateFunc?.Invoke(true, error);
            return false;
        }
    }

    private async Task<bool> ApplyRoutesInternal()
    {
        if (_netBridgeService == null)
        {
            return false;
        }

        List<NetBridgeRuleConfig> rules;

        rules = _ruleConfigs.Select(JsonUtils.DeepCopy).ToList();

        foreach (var rule in rules.Where(x => x.RuleId > 0))
        {
            try
            {
                _ = _netBridgeService.DeleteRule(rule.RuleId);
            }
            catch
            {
                // ignored
            }
        }

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var newRuleId = _netBridgeService.AddRule(rule.ProcessName, rule.TargetHosts, rule.TargetPorts, rule.Protocol, rule.Action, rule.ProxyConfigId);
            if (newRuleId == 0)
            {
                return false;
            }

            rules[i].RuleId = newRuleId;
        }

        _ruleConfigs = rules;

        return await Task.FromResult(true);
    }

    private static List<NetBridgeRuleConfig> BuildRuleConfigs(string? ruleProcess)
    {
        var processNames = Utils.String2List(Utils.Convert2Comma(ruleProcess));
        return processNames.Select(processName => new NetBridgeRuleConfig
        {
            ProcessName = processName,
            TargetHosts = "*",
            TargetPorts = "*",
            Protocol = "BOTH",
            Action = "PROXY",
            ProxyConfigId = 0
        }).ToList();
    }
}
