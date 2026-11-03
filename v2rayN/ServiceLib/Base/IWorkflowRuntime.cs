namespace ServiceLib.Base;

/// <summary>
/// The operations a workflow can invoke at runtime. Implemented by the UI layer
/// so that <see cref="Handler.WorkflowHandler"/> stays free of view models.
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
    /// Activate a server of a group, or of every group when <paramref name="subId"/> is null.
    /// <paramref name="selectType"/> chooses the topmost or bottom row of the group's persisted
    /// sort order.
    /// </summary>
    Task ActivateServer(string? subId, EServerSelectType selectType);

    /// <summary>Set the system proxy mode.</summary>
    Task SetSystemProxy(ESysProxyType type);
}
