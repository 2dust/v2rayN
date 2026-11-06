using ServiceLib.ViewModels;

namespace ServiceLib.Services;

/// <summary>
/// Bridges <see cref="WorkflowHandler"/> to the profile and status bar view models.
/// Created by the UI layer once the main view models exist. Every operation is awaited to
/// completion, including the core reload after activation and the OS proxy change, so a
/// following step never runs against a half-applied state.
/// </summary>
public sealed class WorkflowRuntimeService(
    ProfilesViewModel profiles,
    Func<string?, bool, Task> updateSubscriptions,
    Func<ESysProxyType, Task> setSystemProxy) : IWorkflowRuntime
{
    public async Task UpdateSubscriptions(string? subId, bool viaProxy)
    {
        await updateSubscriptions(subId, viaProxy);
    }

    public async Task<int> DeduplicateServers(string? subId)
    {
        var (removed, oldCount, newCount) = await profiles.DedupServersAsync(subId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, oldCount, newCount));
        return removed;
    }

    public async Task SortServers(string? subId, string column, bool asc)
    {
        await profiles.SortServersAsync(subId, column, asc);
    }

    public async Task TestServers(string? subId, ESpeedActionType actionType)
    {
        await profiles.TestServersAsync(subId, actionType);
    }

    public async Task<int> RemoveInvalidServers(string? subId)
    {
        var count = await profiles.RemoveInvalidServersAsync(subId);
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
        return count;
    }

    public async Task ActivateServer(string? subId, EServerSelectType selectType)
    {
        await profiles.SetDefaultServerForGroupAsync(subId, selectType);
    }

    public async Task SetSystemProxy(ESysProxyType type)
    {
        await setSystemProxy(type);
    }
}
