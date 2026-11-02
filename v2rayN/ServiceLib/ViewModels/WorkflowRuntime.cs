namespace ServiceLib.ViewModels;

/// <summary>
/// Bridges <see cref="WorkflowHandler"/> to the profile and status bar view models.
/// Created by the UI layer once the main view models exist.
/// </summary>
public sealed class WorkflowRuntime(ProfilesViewModel profiles, Func<string?, bool, Task> updateSubscriptions) : IWorkflowRuntime
{
    public async Task UpdateSubscriptions(string? subId, bool viaProxy)
    {
        await updateSubscriptions(subId, viaProxy);
    }

    public async Task<int> DeduplicateServers(string subId)
    {
        var count = await profiles.DedupServersAsync(subId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, count, 0));
        return count;
    }

    public async Task SortServers(string subId, string column, bool asc)
    {
        await profiles.SortServersAsync(subId, column, asc);
    }

    public async Task TestServers(string subId, ESpeedActionType actionType)
    {
        await profiles.TestServersAsync(subId, actionType);
    }

    public async Task<int> RemoveInvalidServers(string subId)
    {
        var count = await profiles.RemoveInvalidServersAsync(subId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
        return count;
    }

    public async Task ActivateServer(string subId, EServerSelectType selectType)
    {
        await profiles.SetDefaultServerForGroupAsync(subId, selectType);
    }

    public Task SetSystemProxy(ESysProxyType type)
    {
        AppEvents.SysProxyChangeRequested.Publish(type);
        return Task.CompletedTask;
    }
}
