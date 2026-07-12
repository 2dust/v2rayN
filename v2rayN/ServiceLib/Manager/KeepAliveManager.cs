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

        SendLog(ResUI.MsgKeepAliveStartCheck);

        var delay = await ConnectionHandler.GetCurrentRealPingAsync();
        if (delay > 0)
        {
            SendLog(string.Format(ResUI.MsgKeepAliveCurrentNodeAlive, delay));
            return;
        }

        SendLog(string.Format(ResUI.MsgKeepAliveCurrentNodeFailed, subItem.Remarks));

        var (bestIndexId, bestDelay) = await FindBestNodeAsync(subId);
        if (bestIndexId.IsNotEmpty())
        {
            await SwitchToBestNodeAsync(bestIndexId, bestDelay);
            return;
        }

        if (now - subItem.KeepAliveLastUpdate >= subItem.KeepAliveInterval * 60)
        {
            SendLog(string.Format(ResUI.MsgKeepAliveUpdateTriggered, subItem.Remarks));
            subItem.KeepAliveLastUpdate = now;
            await ConfigHandler.AddSubItem(_config, subItem);
            await SubscriptionHandler.UpdateProcess(_config, subId, true, _updateFunc);
        }
        else
        {
            var message = string.Format(ResUI.MsgKeepAliveAllFailed, subItem.Remarks);
            SendLog(message);
            NoticeManager.Instance.Enqueue(message);
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

        SendLog(string.Format(ResUI.MsgKeepAliveCurrentNodeFailed, subItem.Remarks));

        var (bestIndexId, bestDelay) = await FindBestNodeAsync(subId);
        if (bestIndexId.IsNotEmpty())
        {
            await SwitchToBestNodeAsync(bestIndexId, bestDelay);
            return;
        }

        var message = string.Format(ResUI.MsgKeepAliveAllFailed, subItem.Remarks);
        SendLog(message);
        NoticeManager.Instance.Enqueue(message);
    }

    private async Task<(string IndexId, int Delay)> FindBestNodeAsync(string subId)
    {
        var nodes = await AppManager.Instance.ProfileItems(subId);
        if (nodes == null || nodes.Count == 0)
        {
            return (string.Empty, 0);
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

        return (bestIndexId, bestDelay == int.MaxValue ? 0 : bestDelay);
    }

    private async Task SwitchToBestNodeAsync(string indexId, int delay)
    {
        var profile = await AppManager.Instance.GetProfileItem(indexId);
        await ConfigHandler.SetDefaultServerIndex(_config, indexId);
        await _reloadFunc();

        var message = string.Format(ResUI.MsgKeepAliveSwitched, profile?.Remarks ?? indexId, delay);
        SendLog(message);
        NoticeManager.Instance.Enqueue(message);
    }

    private void SendLog(string message)
    {
        NoticeManager.Instance.SendMessageEx(message);
        Logging.SaveLog($"{_tag}: {message}");
    }
}
