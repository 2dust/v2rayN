namespace ServiceLib.Manager;

/// <summary>
/// Background service that periodically runs a real-ping test over all
/// servers and switches the active server according to the configured
/// <see cref="EAutoSwitchStrategy"/>: failover-only self healing, fastest
/// server within the current region, or fastest server overall.
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

    // A current delay at or above this threshold counts as severely degraded
    // (a fault) for the failover strategies; a delay of 0 or less means the
    // server is unreachable.
    private const int FaultDelayMs = 1000;

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

    /// <summary>Region key used when no keyword matches a server's remarks.</summary>
    public const string UnknownRegion = "unknown";

    // Region keywords parsed from server remarks, first match wins. Both the
    // Chinese and English names (plus common city names) are listed.
    private static readonly (string Region, string[] Keywords)[] RegionKeywords =
    [
        ("JP", ["日本", "JP", "Japan", "Tokyo", "东京", "大阪", "Osaka"]),
        ("SG", ["新加坡", "SG", "Singapore"]),
        ("HK", ["香港", "HK", "HongKong", "Hong Kong"]),
        ("TW", ["台湾", "TW", "Taiwan", "台北"]),
        ("US", ["美国", "US", "USA", "Los Angeles", "洛杉矶", "San Jose", "New York"]),
        ("KR", ["韩国", "KR", "Korea", "Seoul", "首尔"]),
        ("UK", ["英国", "UK", "London"]),
        ("DE", ["德国", "DE", "Germany"]),
        ("CA", ["加拿大", "CA"]),
        ("AU", ["澳大利亚", "AU", "Sydney"]),
        ("IN", ["印度", "IN"]),
        ("RU", ["俄罗斯", "RU"]),
        ("TR", ["土耳其", "TR"]),
        ("AR", ["阿根廷", "AR"]),
        ("BR", ["巴西", "BR"]),
        ("FR", ["法国", "FR", "Paris"]),
        ("NL", ["荷兰", "NL"]),
    ];

    private static bool IsShortLatinCode(string keyword)
    {
        return keyword.Length == 2 && keyword.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    /// <summary>
    /// Resolves the region key of a server from its remarks. Two-letter Latin
    /// codes are only consulted in a second pass, because as substrings they
    /// produce false positives ("US" in "Russia", "IN" in "Singapore").
    /// </summary>
    public static string GetRegionKey(string? remarks)
    {
        if (string.IsNullOrEmpty(remarks))
        {
            return UnknownRegion;
        }

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var (region, keywords) in RegionKeywords)
            {
                foreach (var keyword in keywords)
                {
                    if ((pass == 1) != IsShortLatinCode(keyword))
                    {
                        continue;
                    }
                    if (remarks.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        return region;
                    }
                }
            }
        }

        return UnknownRegion;
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
        if (item is null)
        {
            return;
        }

        var strategy = item.GetStrategy();
        if (strategy == EAutoSwitchStrategy.Off)
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
            await RunAutoSwitchAsync(strategy);
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

    private async Task RunAutoSwitchAsync(EAutoSwitchStrategy strategy)
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

        Logging.SaveLog($"{_tag}: strategy={strategy} start realping test for {lstSelected.Count} servers");
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
            Logging.SaveLog($"{_tag}: strategy={strategy} current server is a complex type, skip auto switch");
            return;
        }

        var currentDelay = currentId is not null && delayMap.TryGetValue(currentId, out var curDelay)
            ? curDelay
            : -1;
        var currentRegion = GetRegionKey(currentProfile?.Remarks);
        var currentFault = currentDelay <= 0 || currentDelay >= FaultDelayMs;

        // Candidates: single (non-complex) servers with a valid delay, excluding the current one.
        // Info/ad pseudo-servers are never candidates (the current server may
        // still be one; switching away from it is unaffected).
        var candidates = new List<(ProfileItem Profile, int Delay, string Region)>();
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
            if (delayMap.TryGetValue(profile.IndexId, out var delay) && delay > 0)
            {
                candidates.Add((profile, delay, GetRegionKey(profile.Remarks)));
            }
        }

        if (candidates.Count == 0)
        {
            Logging.SaveLog($"{_tag}: strategy={strategy} no server with a valid delay found, skip auto switch");
            return;
        }

        var globalBest = PickBest(candidates)!.Value;
        var regionBest = currentRegion != UnknownRegion
            ? PickBest(candidates.Where(t => t.Region == currentRegion))
            : null;

        Logging.SaveLog($"{_tag}: strategy={strategy} current [{currentProfile?.Remarks}] delay={currentDelay} ms region={currentRegion} fault={currentFault}");

        switch (strategy)
        {
            case EAutoSwitchStrategy.FailoverOnly:
                if (!currentFault)
                {
                    Logging.SaveLog($"{_tag}: strategy=FailoverOnly current server is healthy ({currentDelay} ms), no switch");
                    return;
                }
                Logging.SaveLog($"{_tag}: strategy=FailoverOnly failover trigger delay={currentDelay} ms");
                await SwitchToAsync(globalBest, strategy, "failover", exemptDwell: currentDelay <= 0, currentProfile, currentDelay);
                break;

            case EAutoSwitchStrategy.SameRegion:
                if (currentRegion == UnknownRegion)
                {
                    Logging.SaveLog($"{_tag}: strategy=SameRegion current region=unknown, degrade to Global behavior");
                    await SwitchGlobalAsync(globalBest, strategy, currentProfile, currentDelay);
                }
                else if (currentDelay <= 0)
                {
                    // Current server is dead: stay in the region if possible,
                    // fall back to the global best only when no server in the
                    // region is alive (self healing beats region pinning).
                    if (regionBest is { } sameRegion)
                    {
                        await SwitchToAsync(sameRegion, strategy, $"failover region={currentRegion}", exemptDwell: true, currentProfile, currentDelay);
                    }
                    else
                    {
                        Logging.SaveLog($"{_tag}: strategy=SameRegion no alive server in region={currentRegion}, cross-region fallback");
                        await SwitchToAsync(globalBest, strategy, "cross-region fallback", exemptDwell: true, currentProfile, currentDelay);
                    }
                }
                else if (regionBest is not { } regionCandidate)
                {
                    Logging.SaveLog($"{_tag}: strategy=SameRegion no candidate in region={currentRegion}, no switch");
                }
                else if (!IsSignificantlyBetter(regionCandidate.Delay, currentDelay))
                {
                    Logging.SaveLog($"{_tag}: strategy=SameRegion best in region={currentRegion} [{regionCandidate.Profile.Remarks}] {regionCandidate.Delay} ms is not significantly better than current {currentDelay} ms, no switch");
                }
                else
                {
                    await SwitchToAsync(regionCandidate, strategy, $"same-region region={currentRegion}", exemptDwell: false, currentProfile, currentDelay);
                }
                break;

            default: // EAutoSwitchStrategy.Global
                await SwitchGlobalAsync(globalBest, strategy, currentProfile, currentDelay);
                break;
        }
    }

    /// <summary>
    /// The v1 behavior: switch to the fastest server overall, guarded by the
    /// hysteresis so small latency gains never cause a switch.
    /// </summary>
    private async Task SwitchGlobalAsync((ProfileItem Profile, int Delay, string Region) best, EAutoSwitchStrategy strategy, ProfileItem? currentProfile, int currentDelay)
    {
        if (currentDelay > 0 && !IsSignificantlyBetter(best.Delay, currentDelay))
        {
            Logging.SaveLog($"{_tag}: strategy={strategy} best server [{best.Profile.Remarks}] {best.Delay} ms is not significantly better than current {currentDelay} ms, no switch");
            return;
        }

        await SwitchToAsync(best, strategy, "global", exemptDwell: false, currentProfile, currentDelay);
    }

    private static bool IsSignificantlyBetter(int candidateDelay, int currentDelay)
    {
        return candidateDelay <= currentDelay * SwitchRatio && currentDelay - candidateDelay >= SwitchMinGainMs;
    }

    private static (ProfileItem Profile, int Delay, string Region)? PickBest(IEnumerable<(ProfileItem Profile, int Delay, string Region)> source)
    {
        (ProfileItem Profile, int Delay, string Region)? best = null;
        foreach (var item in source)
        {
            if (best is null || item.Delay < best.Value.Delay)
            {
                best = item;
            }
        }

        return best;
    }

    private async Task SwitchToAsync((ProfileItem Profile, int Delay, string Region) target, EAutoSwitchStrategy strategy, string reason, bool exemptDwell, ProfileItem? currentProfile, int currentDelay)
    {
        if (!exemptDwell && DateTime.Now - _lastSwitchTime < MinSwitchInterval)
        {
            Logging.SaveLog($"{_tag}: strategy={strategy} last switch was less than {MinSwitchInterval.TotalSeconds} s ago, no switch");
            return;
        }

        var currentRemarks = currentProfile?.Remarks ?? _config.IndexId;
        if (await ConfigTextHandler.SetDefaultServerIndex(target.Profile.IndexId) == 0)
        {
            _lastSwitchTime = DateTime.Now;
            Logging.SaveLog($"{_tag}: strategy={strategy} switch ({reason}) [{currentRemarks}] ({currentDelay} ms) -> [{target.Profile.Remarks}] ({target.Delay} ms)");
            ReloadRequested.Publish();
        }
    }
}
