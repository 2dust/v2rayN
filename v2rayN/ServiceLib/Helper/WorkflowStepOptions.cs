namespace ServiceLib.Helper;

/// <summary>
/// The <see cref="Models.Dto.WorkflowStep.Parameter"/> values each action accepts.
/// The parameter is persisted as a string, so the editor offers these instead of
/// asking the user to type an enum name from memory.
/// </summary>
public static class WorkflowStepOptions
{
    // Actions that take no parameter get no choices: the editor leaves the cell blank and
    // disables it. An empty dropdown is never offered, which would otherwise open as an
    // empty popup and look broken.
    private static readonly List<string> _none = [];

    /// <summary>
    /// Sortable columns. <see cref="EServerColName.Def"/> is a placeholder that
    /// <see cref="Handler.ConfigHandler.SortServers"/> silently ignores, so it is
    /// deliberately not offered.
    /// </summary>
    private static readonly List<string> _sortColumns =
        Utils.GetEnumNames<EServerColName>().Where(t => t != nameof(EServerColName.Def)).ToList();

    private static readonly List<string> _testTypes = Utils.GetEnumNames<ESpeedActionType>();

    private static readonly List<string> _proxyTypes = Utils.GetEnumNames<ESysProxyType>();

    public static IReadOnlyList<string> ParametersFor(EWorkflowAction action)
    {
        return action switch
        {
            EWorkflowAction.SortServers => _sortColumns,
            EWorkflowAction.TestServers => _testTypes,
            EWorkflowAction.SystemProxy => _proxyTypes,
            _ => _none,
        };
    }

    /// <summary>
    /// Parameter a freshly chosen action starts with. Mirrors the fallbacks in
    /// <see cref="Handler.WorkflowHandler"/> so the editor never shows a value the
    /// runner would treat differently. Null for actions that take no parameter.
    /// </summary>
    public static string? DefaultParameter(EWorkflowAction action)
    {
        return action switch
        {
            EWorkflowAction.SortServers => nameof(EServerColName.DelayVal),
            EWorkflowAction.TestServers => nameof(ESpeedActionType.Realping),
            EWorkflowAction.SystemProxy => nameof(ESysProxyType.ForcedClear),
            _ => null,
        };
    }

    /// <summary>
    /// Coerce <paramref name="parameter"/> to a value the action accepts, falling back to its
    /// default. Keeps the stored value honest when the user switches action, so a leftover
    /// value that <see cref="Handler.WorkflowHandler"/> would ignore cannot survive.
    /// </summary>
    public static string? Normalize(EWorkflowAction action, string? parameter)
    {
        return parameter.IsNotEmpty() && ParametersFor(action).Contains(parameter)
            ? parameter
            : DefaultParameter(action);
    }
}
