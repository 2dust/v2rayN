namespace ServiceLib.Manager;

/// <summary>
/// Background service that periodically runs a real-ping test over all servers
/// and switches the active server to the lowest-latency one when it is
/// significantly better than the current server.
/// </summary>
public class AutoSwitchManager
{
    private static readonly Lazy<AutoSwitchManager> _instance = new(() => new());
    public static AutoSwitchManager Instance => _instance.Value;
    private static readonly string _tag = "AutoSwitchManager";

    // The best server must be at most 85% of the current delay ...
    private const double SwitchRatio = 0.85;

    // ... and at least 80 ms faster than the current server to trigger a switch.
    private const int SwitchMinGainMs = 80;

    // Minimum dwell time between two switches.
    private static readonly TimeSpan MinSwitchInterval = TimeSpan.FromSeconds(600);

    private const int MinIntervalMinutes = 3;

    // Remarks keywords that mark an info/ad pseudo-server published by some
    // providers (remaining traffic, expiry notices, official site links,
    // renewal ads, ...). Such entries are never selected as a switch target,
    // because their real exit location is unknown. Matching is
    // case-insensitive and substring-based.
    private static readonly string[] InfoNodeKeywords =
    [
        "剩余流量", "重置", "到期", "过期", "官网", "续费",
        "购买", "套餐", "广告", "公告", "邀请", "客服",
    ];

    private static bool IsInfoNode(string? remarks)
    {
        if (string.IsNullOrEmpty(remarks))
        {
            return false;
        }

        foreach (var keyword in InfoNodeKeywords)
        {
            if (remarks.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private Config _config;
    private bool _initialized;
    private int _testRunningFlag;
    private DateTime _lastTestTime = DateTime.MinValue;
    private DateTime _lastSwitchTime = DateTime.MinValue;

    /// <summary>
    /// Published after the active server has been changed, so the main window
    /// reloads the core (same pattern as the view-model reload channels).
    /// </summary>
    public EventChannel<RxVoid> ReloadRequested { get; } = new();

    public Task Init(Config config)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }
        _initialized = true;
        _config = config;

        _ = Task.Run(RunLoopAsync);
        Logging.SaveLog($"{_tag}: initialized");
        return Task.CompletedTask;
    }

    private async Task RunLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                await TickAsync();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private async Task TickAsync()
    {
        var item = _config?.AutoSwitchItem;
        if (item is null || !item.EnableAutoSwitch)
        {
            return;
        }

        var intervalMinutes = Math.Max(item.AutoSwitchInterval, MinIntervalMinutes);
        if (DateTime.Now - _lastTestTime < TimeSpan.FromMinutes(intervalMinutes))
        {
            return;
        }

        // Guard against re-entry: only one auto test may run at a time.
        if (Interlocked.CompareExchange(ref _testRunningFlag, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _lastTestTime = DateTime.Now;
            await RunAutoSwitchAsync();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            Interlocked.Exchange(ref _testRunningFlag, 0);
        }
    }

    private async Task RunAutoSwitchAsync()
    {
        var profiles = await ConfigDbQuery.ProfileItems("");
        if (profiles is null || profiles.Count == 0)
        {
            return;
        }

        // Same eligibility filter as SpeedtestService.GetClearItem.
        var lstSelected = profiles
            .Where(t => t.ConfigType != EConfigType.Custom
                        && (t.ConfigType.IsComplexType() || t.Port > 0))
            .ToList();
        if (lstSelected.Count == 0)
        {
            return;
        }

        Logging.SaveLog($"{_tag}: start realping test for {lstSelected.Count} servers");
        var speedtestService = new SpeedtestService(_config, (SpeedTestResult result) => Task.CompletedTask);
        await speedtestService.RunLoop(ESpeedActionType.Realping, lstSelected);

        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        var delayMap = profileExs
            .GroupBy(t => t.IndexId)
            .ToDictionary(g => g.Key, g => g.First().Delay);

        var currentId = _config.IndexId;
        var currentProfile = lstSelected.FirstOrDefault(t => t.IndexId == currentId)
                             ?? await ConfigDbQuery.GetProfileItem(currentId);
        if (currentProfile is not null && currentProfile.ConfigType.IsComplexType())
        {
            // The user picked a group/policy server; never fight that choice.
            Logging.SaveLog($"{_tag}: current server is a complex type, skip auto switch");
            return;
        }

        var currentDelay = currentId is not null && delayMap.TryGetValue(currentId, out var curDelay)
            ? curDelay
            : -1;

        // Candidates: single (non-complex) servers with a valid delay, excluding the current one.
        // Info/ad pseudo-servers are never candidates (the current server may
        // still be one; switching away from it is unaffected).
        ProfileItem? best = null;
        var bestDelay = int.MaxValue;
        foreach (var profile in lstSelected)
        {
            if (profile.IndexId == currentId || profile.ConfigType.IsComplexType())
            {
                continue;
            }
            if (IsInfoNode(profile.Remarks))
            {
                Logging.SaveLog($"{_tag}: skip info node: {profile.Remarks}");
                continue;
            }
            if (delayMap.TryGetValue(profile.IndexId, out var delay) && delay > 0 && delay < bestDelay)
            {
                best = profile;
                bestDelay = delay;
            }
        }

        if (best is null)
        {
            Logging.SaveLog($"{_tag}: no server with a valid delay found, skip auto switch");
            return;
        }

        if (currentDelay > 0)
        {
            // Hysteresis: only switch when the best server is significantly better.
            if (bestDelay > currentDelay * SwitchRatio || currentDelay - bestDelay < SwitchMinGainMs)
            {
                Logging.SaveLog($"{_tag}: best server [{best.Remarks}] {bestDelay} ms is not significantly better than current {currentDelay} ms, no switch");
                return;
            }
        }

        if (DateTime.Now - _lastSwitchTime < MinSwitchInterval)
        {
            Logging.SaveLog($"{_tag}: last switch was less than {MinSwitchInterval.TotalSeconds} s ago, no switch");
            return;
        }

        var currentRemarks = currentProfile?.Remarks ?? currentId;
        if (await ConfigTextHandler.SetDefaultServerIndex(best.IndexId) == 0)
        {
            _lastSwitchTime = DateTime.Now;
            Logging.SaveLog($"{_tag}: switch server [{currentRemarks}] ({currentDelay} ms) -> [{best.Remarks}] ({bestDelay} ms)");
            ReloadRequested.Publish();
        }
    }
}
