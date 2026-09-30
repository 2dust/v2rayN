using ServiceLib.Enums;

namespace v2rayN.Web.Services;

internal enum CoreRuntimeState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Restarting,
    Faulted,
}

internal sealed record RuntimeListenerSnapshot(
    string Name,
    string[] Protocols,
    string ListenAddress,
    int Port);

/// <summary>
/// Immutable description of the Core process that was actually launched. Config changes
/// never mutate this snapshot; only confirmed runtime transitions replace it.
/// </summary>
internal sealed record CoreRuntimeSnapshot(
    CoreRuntimeState State,
    string? ProfileId,
    ECoreType? CoreType,
    DateTimeOffset? StartedAt,
    int? ProxyPort,
    int? ApiPort,
    RuntimeListenerSnapshot[] Listeners,
    int[] ProcessIds,
    string? LastFailure)
{
    public static CoreRuntimeSnapshot Stopped { get; } = new(
        CoreRuntimeState.Stopped,
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        null);
}
