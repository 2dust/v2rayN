using v2rayN.Web.Contracts;

namespace v2rayN.Web.Services;

internal enum CoreSettingsApplyAction
{
    SaveOnly,
    Restart,
    Busy,
    Inconsistent,
}

internal static class CoreSettingsApplyPolicy
{
    public static CoreSettingsApplyAction Decide(CoreRuntimeState state, bool hasActiveChild, bool changed) => state switch
    {
        CoreRuntimeState.Stopped when !hasActiveChild => CoreSettingsApplyAction.SaveOnly,
        CoreRuntimeState.Stopped => CoreSettingsApplyAction.Inconsistent,
        CoreRuntimeState.Running when hasActiveChild && changed => CoreSettingsApplyAction.Restart,
        CoreRuntimeState.Running when hasActiveChild => CoreSettingsApplyAction.SaveOnly,
        CoreRuntimeState.Running => CoreSettingsApplyAction.Inconsistent,
        CoreRuntimeState.Faulted when hasActiveChild => CoreSettingsApplyAction.Restart,
        CoreRuntimeState.Faulted => CoreSettingsApplyAction.SaveOnly,
        CoreRuntimeState.Starting or CoreRuntimeState.Stopping or CoreRuntimeState.Restarting => CoreSettingsApplyAction.Busy,
        _ => CoreSettingsApplyAction.Inconsistent,
    };
}

internal sealed record CoreSettingsApplyExecution(CoreSettingsApplyAction Action, OperationView? RestartResult);

internal static class CoreSettingsApplyExecutor
{
    public static async Task<CoreSettingsApplyExecution> ExecuteAsync(
        CoreRuntimeState state,
        bool hasActiveChild,
        bool changed,
        Func<Task<OperationView>> restart)
    {
        var action = CoreSettingsApplyPolicy.Decide(state, hasActiveChild, changed);
        var restartResult = action == CoreSettingsApplyAction.Restart ? await restart() : null;
        return new(action, restartResult);
    }
}
