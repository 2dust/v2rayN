namespace ServiceLib.ViewModels;

/// <summary>
/// Editor for a single workflow: its name/enabled flag and the ordered list of steps.
/// </summary>
public partial class WorkflowEditViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial WorkflowItem SelectedSource { get; set; }

    [Reactive]
    public partial WorkflowStep SelectedStep { get; set; }

    public BulkObservableCollection<WorkflowStep> Steps { get; } = [];

    public List<string> ActionNames { get; } = Utils.GetEnumNames<EWorkflowAction>();

    /// <summary>Group choices for the step editor's "Group" column.</summary>
    public BulkObservableCollection<string> GroupNames { get; } = [];

    private void ApplyStepOptions()
    {
        foreach (var step in Steps)
        {
            step.ActionOptions = ActionNames;
            step.GroupOptions = GroupNames;
            step.Parameter = WorkflowStepOptions.Normalize(step.Action, step.Parameter);
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> AddStepCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DeleteStepCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> StepUpCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> StepDownCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public WorkflowEditViewModel(WorkflowItem workflow)
    {
        _config = AppManager.Instance.Config;

        var canEditStep = this.WhenAnyValue(
            x => x.SelectedStep,
            (WorkflowStep selected) => selected != null);

        AddStepCmd = ReactiveCommand.Create(AddStep);
        DeleteStepCmd = ReactiveCommand.Create(DeleteStep, canEditStep);
        StepUpCmd = ReactiveCommand.Create(() => MoveStep(-1), canEditStep);
        StepDownCmd = ReactiveCommand.Create(() => MoveStep(1), canEditStep);
        SaveCmd = ReactiveCommand.CreateFromTask(async () => await SaveWorkflowAsync());

        SelectedSource = workflow.Id.IsNullOrEmpty() ? workflow : JsonUtils.DeepCopy(workflow);
        LoadSteps(workflow.Id.IsNullOrEmpty() ? CreateSample() : WorkflowHandler.DeserializeSteps(workflow));
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var subItems = await AppManager.Instance.SubItems() ?? [];
        var names = subItems.Select(t => t.Remarks).ToList();
        names.Insert(0, ResUI.AllGroupServers);
        GroupNames.ReplaceRange(names);

        var map = subItems.ToDictionary(t => t.Id, t => t.Remarks);
        foreach (var step in Steps.Where(t => t.SubId.IsNotEmpty()))
        {
            map.TryGetValue(step.SubId, out var remarks);
            step.SubDisplay = remarks ?? ResUI.AllGroupServers;
        }
        ApplyStepOptions();
    }

    /// <summary>Turn the editor's group name back into a subscription id before saving.</summary>
    private async Task ResolveStepSubsAsync()
    {
        var subItems = await AppManager.Instance.SubItems() ?? [];
        var map = subItems.ToDictionary(t => t.Remarks, t => t.Id);
        foreach (var step in Steps)
        {
            step.SubId = step.SubDisplay.IsNotEmpty()
                && step.SubDisplay != ResUI.AllGroupServers
                && map.TryGetValue(step.SubDisplay, out var id)
                ? id
                : null;
        }
    }

    private void LoadSteps(List<WorkflowStep> steps)
    {
        Steps.ReplaceRange(steps);
        ApplyStepOptions();
        SelectedStep = Steps.FirstOrDefault();
    }

    /// <summary>Starter chain offered for a brand new workflow; the user can freely edit it.</summary>
    private static List<WorkflowStep> CreateSample()
    {
        return
        [
            new() { Action = EWorkflowAction.UpdateSubscriptions },
            new() { Action = EWorkflowAction.DeduplicateServers },
            new() { Action = EWorkflowAction.SortServers, Parameter = nameof(EServerColName.DelayVal) },
            new() { Action = EWorkflowAction.TestServers, Parameter = nameof(ESpeedActionType.Realping) },
            new() { Action = EWorkflowAction.RemoveInvalidServers },
            new() { Action = EWorkflowAction.SortServers, Parameter = nameof(EServerColName.SpeedVal), BoolParameter = false },
            new() { Action = EWorkflowAction.ActivateServer, Parameter = nameof(EServerSelectType.First) },
            new() { Action = EWorkflowAction.SystemProxy, Parameter = nameof(ESysProxyType.ForcedChange) },
        ];
    }

    private void AddStep()
    {
        var step = new WorkflowStep
        {
            ActionOptions = ActionNames,
            GroupOptions = GroupNames,
        };
        Steps.Add(step);
        SelectedStep = step;
    }

    private void DeleteStep()
    {
        if (SelectedStep is null)
        {
            return;
        }
        var index = Steps.IndexOf(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = Steps.Count > 0 ? Steps[Math.Clamp(index, 0, Steps.Count - 1)] : null;
    }

    private void MoveStep(int offset)
    {
        if (SelectedStep is null)
        {
            return;
        }
        var index = Steps.IndexOf(SelectedStep);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Steps.Count)
        {
            return;
        }
        Steps.Move(index, target);
        SelectedStep = Steps[target];
    }

    private async Task SaveWorkflowAsync()
    {
        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        await ResolveStepSubsAsync();
        WorkflowHandler.SaveSteps(SelectedSource, [.. Steps]);
        if (await ConfigHandler.AddWorkflowItem(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
