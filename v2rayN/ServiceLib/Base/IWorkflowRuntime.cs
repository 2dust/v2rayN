namespace ServiceLib.Base;

/// <summary>
/// The operations a workflow can invoke at runtime. Implemented headlessly in
/// <see cref="Services.WorkflowRuntimeService"/> so that <see cref="Handler.WorkflowHandler"/>
/// stays free of view models and a workflow can run with no window open.
/// </summary>
public interface IWorkflowRuntime
{
    /// <summary>Update one subscription, or every subscription when <paramref name="subId"/> is null.</summary>
    Task UpdateSubscriptions(string? subId, bool viaProxy);

    /// <summary>
    /// Remove duplicate servers in a group, or in every group when <paramref name="subId"/> is null.
    /// Returns the number of removed servers.
    /// </summary>
    Task<int> DeduplicateServers(string? subId);

    /// <summary>Sort a group, or every group when <paramref name="subId"/> is null.</summary>
    Task SortServers(string? subId, string column, bool asc);

    /// <summary>Run a connectivity/speed test over a group, or every group when <paramref name="subId"/> is null.</summary>
    Task TestServers(string? subId, ESpeedActionType actionType);

    /// <summary>
    /// Remove servers whose last test result was invalid, in a group or in every group when
    /// <paramref name="subId"/> is null. Returns the number of removed servers.
    /// </summary>
    Task<int> RemoveInvalidServers(string? subId);

    /// <summary>
    /// Activate the lowest-latency server of a group, or of every group when
    /// <paramref name="subId"/> is null. Servers that were never tested, or whose last test
    /// failed, are not candidates.
    /// </summary>
    Task ActivateServer(string? subId);

    /// <summary>Set the system proxy mode.</summary>
    Task SetSystemProxy(ESysProxyType type);
}
