namespace ServiceLib.ViewModels;

/// <summary>
/// Holds the <see cref="IWorkflowRuntime"/> used when a workflow runs. The UI layer
/// registers it after the main view models are built; a registration failure is
/// surfaced instead of silently doing nothing.
/// </summary>
public sealed class WorkflowRunner
{
    private static readonly Lazy<WorkflowRunner> _instance = new(() => new());
    public static WorkflowRunner Instance => _instance.Value;

    private IWorkflowRuntime? _runtime;

    public void Register(IWorkflowRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task Run(Config config, WorkflowItem workflow)
    {
        if (_runtime is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            Logging.SaveLog("WorkflowRunner", new InvalidOperationException("Workflow runtime is not registered."));
            return;
        }
        await WorkflowHandler.Run(config, workflow, _runtime);
    }
}
