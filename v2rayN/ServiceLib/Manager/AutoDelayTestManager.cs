using ServiceLib.Events;
using ServiceLib.ViewModels;

namespace ServiceLib.Manager;

/// <summary>
/// Scheduled real-delay test within the subscription group of the active node.
/// Optionally switches to the lowest-latency node when it beats the current one
/// by the configured threshold (anti-flapping).
/// </summary>
public class AutoDelayTestManager
{
    private static readonly Lazy<AutoDelayTestManager> _instance = new(() => new());
    public static AutoDelayTestManager Instance => _instance.Value;

    private static readonly string _tag = "AutoDelayTestManager";
    private readonly Lock _runLock = new();
    private bool _testing;
    private Config? _config;

    public void Init(Config config)
    {
        _config = config;
    }

    public async Task RunOnce()
    {
        var config = _config;
        if (config is null || !config.SpeedTestItem.AutoDelayTestEnabled)
        {
            return;
        }

        // Skip this round instead of cancelling, in case a test is already running
        lock (_runLock)
        {
            if (_testing)
            {
                return;
            }
            _testing = true;
        }

        try
        {
            await RunAsync(config);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            lock (_runLock)
            {
                _testing = false;
            }
        }
    }

    private async Task RunAsync(Config config)
    {
        // Only test within the subscription group of the active node
        var currentId = config.IndexId;
        var current = await AppManager.Instance.GetProfileItem(currentId);
        var lstProfiles = await AppManager.Instance.ProfileItems(current?.Subid);
        if (lstProfiles is not { Count: > 1 })
        {
            return;
        }

        // RunLoop resolves after the whole test round finishes (results are
        // already persisted into ProfileExManager by SpeedtestService)
        var speedtestService = new SpeedtestService(config, async (SpeedTestResult result) => await Task.CompletedTask);
        Logging.SaveLog($"{_tag} - Start auto delay test, {lstProfiles.Count} nodes in group {current?.Subid}");
        await speedtestService.RunLoop(ESpeedActionType.Realping, lstProfiles);

        // Refresh the profile list so updated delays show up in the UI
        AppEvents.RefreshDelayTestResultsRequested.Publish();

        // Pick the lowest-latency node after the test round completes
        var (best, bestDelay) = await GetBestNode(lstProfiles);
        if (best is null || best.IndexId == currentId)
        {
            return;
        }

        // Anti-flapping: only switch when the new best beats the CURRENT node
        // by at least the threshold (skipped when the current node has no result)
        var currentDelay = await GetNodeDelay(currentId);
        if (currentDelay > 0 && currentDelay - bestDelay < config.SpeedTestItem.AutoSwitchThresholdMs)
        {
            return;
        }

        if (!config.SpeedTestItem.AutoDelayTestAutoSwitch)
        {
            NoticeManager.Instance.Enqueue(string.Format(ResUI.AutoDelayTestSuggestSwitch, best.Remarks, bestDelay));
            return;
        }

        await ConfigHandler.SetDefaultServerIndex(config, best.IndexId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.AutoDelayTestSwitched, best.Remarks, bestDelay));
        StatusBarViewModel.Instance.ReloadRequested.Publish();
    }

    /// <summary>
    /// Returns the node with the lowest cached delay (> 0) and its delay, or (null, 0) if none was tested.
    /// </summary>
    private static async Task<(ProfileItem? Node, int Delay)> GetBestNode(List<ProfileItem> lstProfiles)
    {
        var delays = (await ProfileExManager.Instance.GetProfileExs())
            .Where(t => t.Delay > 0)
            .GroupBy(t => t.IndexId)
            .ToDictionary(g => g.Key, g => g.Min(t => t.Delay));

        var best = lstProfiles
            .Where(t => delays.ContainsKey(t.IndexId))
            .OrderBy(t => delays[t.IndexId])
            .FirstOrDefault();
        return (best, best is null ? 0 : delays[best.IndexId]);
    }

    /// <summary>
    /// Returns the cached delay of a single node, or 0 when it has not been tested.
    /// </summary>
    private static async Task<int> GetNodeDelay(string indexId)
    {
        var profileEx = (await ProfileExManager.Instance.GetProfileExs())
            .FirstOrDefault(t => t.IndexId == indexId);
        return profileEx?.Delay ?? 0;
    }
}
