namespace ServiceLib.ViewModels;

/// <summary>A workflow list entry with a human readable summary of its steps.</summary>
public class WorkflowItemModel
{
    public string Id { get; set; }
    public string Remarks { get; set; }
    public bool Enabled { get; set; }
    public int Sort { get; set; }
    public int StepCount { get; set; }
    public string StepsDisplay { get; set; }
}

/// <summary>
/// Workflow manager window: lists stored workflows and lets the user
/// add, edit, delete and run them.
/// </summary>
public partial class WorkflowViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<WorkflowItem, bool> EditWorkflowInteraction { get; } = new();

    public BulkObservableCollection<WorkflowItemModel> WorkflowItems { get; } = [];

    [Reactive]
    public partial WorkflowItemModel SelectedWorkflow { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> AddCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> EditCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DeleteCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RunCmd { get; }

    public bool IsModified { get; set; }

    public WorkflowViewModel()
    {
        _config = AppManager.Instance.Config;

        var canEdit = this.WhenAnyValue(
            x => x.SelectedWorkflow,
            selected => selected != null && !selected.Id.IsNullOrEmpty());

        AddCmd = ReactiveCommand.CreateFromTask(async () => await EditWorkflowAsync(true));
        EditCmd = ReactiveCommand.CreateFromTask(async () => await EditWorkflowAsync(false), canEdit);
        DeleteCmd = ReactiveCommand.CreateFromTask(async () => await DeleteWorkflowAsync(), canEdit);
        RunCmd = ReactiveCommand.CreateFromTask(async () => await RunWorkflowAsync(), canEdit);

        _ = Init();
    }

    private async Task Init()
    {
        SelectedWorkflow = new();
        await RefreshWorkflowItems();
    }

    public async Task RefreshWorkflowItems()
    {
        var workflows = await AppManager.Instance.WorkflowItems();
        WorkflowItems.ReplaceRange(workflows.Select(ToModel));
    }

    private static WorkflowItemModel ToModel(WorkflowItem item)
    {
        var steps = WorkflowHandler.DeserializeSteps(item);
        return new()
        {
            Id = item.Id,
            Remarks = item.Remarks,
            Enabled = item.Enabled,
            Sort = item.Sort,
            StepCount = steps.Count,
            StepsDisplay = Utils.List2String(steps.Select(t => t.Action.ToString()).ToList(), true),
        };
    }

    public async Task EditWorkflowAsync(bool blNew)
    {
        WorkflowItem item;
        if (blNew)
        {
            item = new() { Remarks = ResUI.menuWorkflowSetting };
        }
        else
        {
            item = await AppManager.Instance.GetWorkflowItem(SelectedWorkflow?.Id);
            if (item is null)
            {
                return;
            }
        }

        if (await EditWorkflowInteraction.HandleSafe(item) == true)
        {
            await RefreshWorkflowItems();
            IsModified = true;
        }
    }

    private async Task DeleteWorkflowAsync()
    {
        if (await ShowYesNoInteraction.HandleSafe(ResUI.menuWorkflowDelete) == false)
        {
            return;
        }
        await ConfigHandler.DeleteWorkflowItem(_config, SelectedWorkflow.Id);
        await RefreshWorkflowItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }

    private async Task RunWorkflowAsync()
    {
        var item = await AppManager.Instance.GetWorkflowItem(SelectedWorkflow?.Id);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.WorkflowRunSelectTip);
            return;
        }
        await WorkflowRunner.Instance.Run(_config, item);
    }
}
