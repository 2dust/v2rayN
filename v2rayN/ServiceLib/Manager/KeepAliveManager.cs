namespace ServiceLib.Manager;

public class KeepAliveManager
{
    private static readonly Lazy<KeepAliveManager> _instance = new(() => new());
    public static KeepAliveManager Instance => _instance.Value;

    private static readonly string _tag = "KeepAliveManager";
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private Config _config = null!;
    private Func<Task> _reloadFunc = null!;
    private Func<bool, string, Task> _updateFunc = null!;

    public void Init(Config config, Func<Task> reloadFunc, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _reloadFunc = reloadFunc;
        _updateFunc = updateFunc;
    }

    public async Task RunKeepAliveAsync()
    {
        if (!await _semaphore.WaitAsync(0))
        {
            return;
        }

        try
        {
            await RunKeepAliveInternalAsync();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RunPostUpdateFallbackAsync(string subId)
    {
        await _semaphore.WaitAsync();

        try
        {
            await RunPostUpdateFallbackInternalAsync(subId);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RunKeepAliveInternalAsync()
    {
        var activeProfile = await AppManager.Instance.GetProfileItem(_config.IndexId);
        if (activeProfile == null)
        {
            return;
        }

        var subId = activeProfile.Subid;
        if (subId.IsNullOrEmpty())
        {
            return;
        }

        var subItem = await AppManager.Instance.GetSubItem(subId);
        if (subItem == null || !subItem.KeepAlive)
        {
            return;
        }

        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (now - subItem.KeepAliveLastCheck < subItem.KeepAliveInterval * 60)
        {
            return;
        }

        subItem.KeepAliveLastCheck = now;
        await ConfigHandler.AddSubItem(_config, subItem);

        var delay = await ConnectionHandler.GetCurrentRealPingAsync();
        if (delay > 0)
        {
            Logging.SaveLog($"{_tag}: current node {activeProfile.Remarks} is alive, delay {delay}");
            return;
        }

        Logging.SaveLog($"{_tag}: current node {activeProfile.Remarks} failed, testing all nodes in subscription {subItem.Remarks}");

        var bestIndexId = await FindBestNodeAsync(subId);
        if (bestIndexId.IsNotEmpty())
        {
            await SwitchToBestNodeAsync(bestIndexId);
            return;
        }

        if (now - subItem.KeepAliveLastUpdate >= subItem.KeepAliveInterval * 60)
        {
            Logging.SaveLog($"{_tag}: all nodes in subscription {subItem.Remarks} failed, triggering subscription update");
            subItem.KeepAliveLastUpdate = now;
            await ConfigHandler.AddSubItem(_config, subItem);
            await SubscriptionHandler.UpdateProcess(_config, subId, true, _updateFunc);
        }
        else
        {
            var message = string.Format(ResUI.MsgKeepAliveAllFailed, subItem.Remarks);
            NoticeManager.Instance.Enqueue(message);
            Logging.SaveLog($"{_tag}: {message}");
        }
    }

    private async Task RunPostUpdateFallbackInternalAsync(string subId)
    {
        if (subId.IsNullOrEmpty())
        {
            return;
        }

        var subItem = await AppManager.Instance.GetSubItem(subId);
        if (subItem == null || !subItem.KeepAlive)
        {
            return;
        }

        Logging.SaveLog($"{_tag}: running post-update fallback for subscription {subItem.Remarks}");

        var bestIndexId = await FindBestNodeAsync(subId);
        if (bestIndexId.IsNotEmpty())
        {
            await SwitchToBestNodeAsync(bestIndexId);
            return;
        }

        var message = string.Format(ResUI.MsgKeepAliveAllFailed, subItem.Remarks);
        NoticeManager.Instance.Enqueue(message);
        Logging.SaveLog($"{_tag}: {message}");
    }

    private async Task<string> FindBestNodeAsync(string subId)
    {
        var nodes = await AppManager.Instance.ProfileItems(subId);
        if (nodes == null || nodes.Count == 0)
        {
            return string.Empty;
        }

        foreach (var node in nodes)
        {
            ProfileExManager.Instance.SetTestDelay(node.IndexId, 0);
        }

        var speedtestService = new SpeedtestService(_config, _ => Task.CompletedTask);
        await speedtestService.RunAsync(ESpeedActionType.Realping, nodes);

        var bestIndexId = string.Empty;
        var bestDelay = int.MaxValue;

        foreach (var node in nodes)
        {
            var delay = ProfileExManager.Instance.GetTestDelay(node.IndexId);
            if (delay > 0 && delay < bestDelay)
            {
                bestDelay = delay;
                bestIndexId = node.IndexId;
            }
        }

        return bestIndexId;
    }

    private async Task SwitchToBestNodeAsync(string indexId)
    {
        var profile = await AppManager.Instance.GetProfileItem(indexId);
        await ConfigHandler.SetDefaultServerIndex(_config, indexId);
        await _reloadFunc();

        var message = string.Format(ResUI.MsgKeepAliveSwitched, profile?.Remarks ?? indexId);
        NoticeManager.Instance.Enqueue(message);
        Logging.SaveLog($"{_tag}: {message}");
    }
}
