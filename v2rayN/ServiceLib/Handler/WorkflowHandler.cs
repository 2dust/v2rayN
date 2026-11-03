namespace ServiceLib.Handler;

/// <summary>
/// Runs a workflow: a named, ordered list of steps, each mapping onto an existing
/// app operation (subscription update, dedup, sort, test, ...). Steps are executed
/// sequentially so that later steps can consume the results of earlier ones.
/// </summary>
public static class WorkflowHandler
{
    private static readonly string _tag = "WorkflowHandler";

    /// <summary>
    /// Execute every enabled step of the workflow. A failing step does not abort the run.
    /// </summary>
    public static async Task Run(WorkflowItem workflow, IWorkflowRuntime runtime)
    {
        var steps = DeserializeSteps(workflow).Where(t => t.Enabled).ToList();
        if (steps.Count == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.WorkflowNoStepTip);
            return;
        }

        NoticeManager.Instance.SendMessageEx($"{ResUI.WorkflowStartTip} - {workflow.Remarks}");
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var prefix = $"[{i + 1}/{steps.Count}] {(step.SubId.IsNotEmpty() ? step.SubId : ResUI.AllGroupServers)}";
            try
            {
                await RunStep(step, runtime);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                NoticeManager.Instance.SendMessageEx($"{prefix} {ResUI.WorkflowStepFailedTip}: {ex.Message}");
            }
        }
        NoticeManager.Instance.Enqueue($"{ResUI.WorkflowEndTip} - {workflow.Remarks}");
    }

    private static async Task RunStep(WorkflowStep step, IWorkflowRuntime runtime)
    {
        // An empty SubId is the editor's "All groups" choice, so it is passed through as null
        // rather than replaced with the selected group. A step that means "everything" must not
        // silently narrow to one group depending on what the user happened to have selected.
        var subId = step.SubId.IsNotEmpty() ? step.SubId : null;

        switch (step.Action)
        {
            case EWorkflowAction.UpdateSubscriptions:
                await runtime.UpdateSubscriptions(subId, step.BoolParameter);
                break;

            case EWorkflowAction.DeduplicateServers:
                await runtime.DeduplicateServers(subId);
                break;

            case EWorkflowAction.SortServers:
                await runtime.SortServers(subId,
                    step.Parameter ?? nameof(EServerColName.DelayVal), step.BoolParameter);
                break;

            case EWorkflowAction.TestServers:
                await runtime.TestServers(subId, ParseTestType(step.Parameter));
                break;

            case EWorkflowAction.RemoveInvalidServers:
                await runtime.RemoveInvalidServers(subId);
                break;

            case EWorkflowAction.ActivateServer:
                await runtime.ActivateServer(subId, ParseServerSelectType(step.Parameter));
                break;

            case EWorkflowAction.SystemProxy:
                await runtime.SetSystemProxy(ParseSysProxyType(step.Parameter));
                break;
        }
    }

    private static ESpeedActionType ParseTestType(string? parameter)
    {
        return Enum.TryParse<ESpeedActionType>(parameter, true, out var type) ? type : ESpeedActionType.Realping;
    }

    private static ESysProxyType ParseSysProxyType(string? parameter)
    {
        return Enum.TryParse<ESysProxyType>(parameter, true, out var type) ? type : ESysProxyType.ForcedClear;
    }

    private static EServerSelectType ParseServerSelectType(string? parameter)
    {
        return Enum.TryParse<EServerSelectType>(parameter, true, out var type) ? type : EServerSelectType.First;
    }

    /// <summary>Deserialize the step list of a workflow, tolerating malformed json.</summary>
    public static List<WorkflowStep> DeserializeSteps(WorkflowItem? workflow)
    {
        if (workflow?.StepsJson.IsNullOrEmpty() != false)
        {
            return [];
        }
        try
        {
            return JsonUtils.Deserialize<List<WorkflowStep>>(workflow.StepsJson) ?? [];
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return [];
        }
    }

    /// <summary>Serialize the step list of a workflow to its storage field.</summary>
    public static void SaveSteps(WorkflowItem workflow, List<WorkflowStep> steps)
    {
        workflow.StepsJson = JsonUtils.Serialize(steps, false);
    }
}
