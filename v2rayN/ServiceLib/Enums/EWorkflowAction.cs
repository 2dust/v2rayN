namespace ServiceLib.Enums;

/// <summary>
/// A single reusable operation that can be chained in a workflow.
/// </summary>
public enum EWorkflowAction
{
    /// <summary>Update one subscription, or every subscription when no sub is set.</summary>
    UpdateSubscriptions = 0,

    /// <summary>Remove duplicate servers in the current group.</summary>
    DeduplicateServers = 1,

    /// <summary>Sort servers in the current group by a column, ascending or descending.</summary>
    SortServers = 2,

    /// <summary>Run a connectivity/speed test over the current group.</summary>
    TestServers = 3,

    /// <summary>Remove servers whose last test result was invalid (timeout).</summary>
    RemoveInvalidServers = 4,

    /// <summary>Activate the best (first) server of the current group, as the server list orders it.</summary>
    ActivateServer = 5,

    /// <summary>Set or clear the system proxy mode.</summary>
    SystemProxy = 6,
}
