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
    public static async Task Run(Config config, WorkflowItem workflow, IWorkflowRuntime runtime)
    {
        var steps = DeserializeSteps(workflow).Where(t => t.Enabled).ToList();
        if (steps.Count == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.WorkflowNoStepTip);
            return;
        }

        var subId = config.SubIndexId;
        NoticeManager.Instance.SendMessageEx($"{ResUI.WorkflowStartTip} - {workflow.Remarks}");
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var prefix = $"[{i + 1}/{steps.Count}] {(step.SubId.IsNotEmpty() ? step.SubId : subId)}";
            try
            {
                await RunStep(config, step, subId, runtime);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                NoticeManager.Instance.SendMessageEx($"{prefix} {ResUI.WorkflowStepFailedTip}: {ex.Message}");
            }
        }
        NoticeManager.Instance.Enqueue($"{ResUI.WorkflowEndTip} - {workflow.Remarks}");
    }

    private static async Task RunStep(Config config, WorkflowStep step, string? subId, IWorkflowRuntime runtime)
    {
        switch (step.Action)
        {
            case EWorkflowAction.UpdateSubscriptions:
                await runtime.UpdateSubscriptions(step.SubId, step.BoolParameter);
                break;

            case EWorkflowAction.DeduplicateServers:
                await runtime.DeduplicateServers(step.SubId.IsNotEmpty() ? step.SubId : subId);
                break;

            case EWorkflowAction.SortServers:
                await runtime.SortServers(step.SubId.IsNotEmpty() ? step.SubId : subId,
                    step.Parameter ?? nameof(EServerColName.DelayVal), step.BoolParameter);
                break;

            case EWorkflowAction.TestServers:
                await runtime.TestServers(step.SubId.IsNotEmpty() ? step.SubId : subId, ParseTestType(step.Parameter));
                break;

            case EWorkflowAction.RemoveInvalidServers:
                await runtime.RemoveInvalidServers(step.SubId.IsNotEmpty() ? step.SubId : subId);
                break;

            case EWorkflowAction.ActivateServer:
                await runtime.ActivateServer(step.SubId.IsNotEmpty() ? step.SubId : subId,
                    ParseServerSelectType(step.Parameter));
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
