namespace ServiceLib.Services;

/// <summary>
/// Runs the operations a workflow can invoke. Every operation is headless: it calls the
/// handler or service that does the work and returns once that work is done, so no step
/// needs a window to be open. Where the result has to reach the UI, the service publishes
/// an app event and the view models refresh themselves.
/// </summary>
public sealed class WorkflowRuntimeService(Config config) : IWorkflowRuntime
{
    public async Task UpdateSubscriptions(string? subId, bool viaProxy)
    {
        var indexIdOld = config.IndexId;
        var success = false;
        var handler = new SubscriptionHandler(config, viaProxy, (ok, msg) =>
        {
            success |= ok;
            NoticeManager.Instance.SendMessageEx(msg);
            return Task.CompletedTask;
        });
        await handler.UpdateProcess(subId);
        AppEvents.ServerStateChanged.Publish();

        // An update can rewrite or drop the server the core is running. Restart it on a valid
        // one, as the interactive update does, so the next step does not run against a stale
        // or dead core.
        if (!success)
        {
            return;
        }
        if (indexIdOld != config.IndexId || config.SubIndexId.IsNullOrEmpty())
        {
            await CoreManager.Instance.ReloadCore();
        }
        else if (await AppManager.Instance.GetProfileItem(config.IndexId) is { } profile
                 && profile.Subid == config.SubIndexId)
        {
            await CoreManager.Instance.ReloadCore();
        }
    }

    public async Task<int> DeduplicateServers(string? subId)
    {
        var tuple = await ConfigHandler.DedupServerList(config, subId);
        var removed = tuple.Item1 - tuple.Item2;
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
        if (removed > 0)
        {
            AppEvents.ServerStateChanged.Publish();
        }
        return removed;
    }

    public async Task SortServers(string? subId, string column, bool asc)
    {
        if (await ConfigHandler.SortServers(config, subId, column, asc) == 0)
        {
            AppEvents.ServerStateChanged.Publish();
        }
    }

    public async Task TestServers(string? subId, ESpeedActionType actionType)
    {
        // SpeedtestService has no FastRealping case, so a step that selects it would finish
        // without testing anything. Realping is what the interactive command uses for it.
        if (actionType == ESpeedActionType.FastRealping)
        {
            actionType = ESpeedActionType.Realping;
        }

        var lstSelected = await GetProfilesForGroup(subId);
        if (lstSelected.Count == 0)
        {
            return;
        }
        // The service persists each result itself, so there is no view to update live.
        await new SpeedtestService(config, _ => Task.CompletedTask).RunLoop(actionType, lstSelected);
        AppEvents.ServerStateChanged.Publish();
    }

    public async Task<int> RemoveInvalidServers(string? subId)
    {
        var count = await ConfigHandler.RemoveInvalidServerResult(config, subId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
        AppEvents.ServerStateChanged.Publish();
        return count;
    }

    public async Task ActivateServer(string? subId)
    {
        var lstModel = await AppManager.Instance.ProfileModels(subId ?? string.Empty, string.Empty);
        if (lstModel is not { Count: > 0 })
        {
            return;
        }

        var ids = lstModel.Select(t => t.IndexId).ToHashSet();
        var target = (await ProfileExManager.Instance.GetProfileExs())
            .Where(t => ids.Contains(t.IndexId) && t.Delay > 0)
            .OrderBy(t => t.Delay)
            .FirstOrDefault();
        if (target is null || await ConfigHandler.SetDefaultServerIndex(config, target.IndexId) != 0)
        {
            return;
        }

        await CoreManager.Instance.ReloadCore();
        AppEvents.ServerStateChanged.Publish();
    }

    public async Task SetSystemProxy(ESysProxyType type)
    {
        if (config.SystemProxyItem.SysProxyType == type)
        {
            return;
        }
        config.SystemProxyItem.SysProxyType = type;
        await SysProxyHandler.UpdateSysProxy(config, false);
        await ConfigHandler.SaveConfig(config);
        AppEvents.SysProxyChanged.Publish();
    }

    private static async Task<List<ProfileItem>> GetProfilesForGroup(string? subId)
    {
        return await AppManager.Instance.ProfileItems(subId ?? string.Empty) ?? [];
    }
}
