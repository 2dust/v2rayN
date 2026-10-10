namespace ServiceLib.Models.Dto;

/// <summary>
/// One step of a workflow. Kept as a plain serializable DTO so it can be
/// persisted inside <see cref="Entities.WorkflowItem.StepsJson"/>.
/// </summary>
[Serializable]
public class WorkflowStep : INotifyPropertyChanged
{
    // Events are not serialized, so the editor can watch this DTO for changes
    // without adding anything to the persisted json.
    public event PropertyChangedEventHandler? PropertyChanged;

    private EWorkflowAction _action;
    private string? _parameter;

    public EWorkflowAction Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }
            _action = value;
            _parameter = WorkflowStepOptions.Normalize(value, _parameter);
            Notify(nameof(Action));
            Notify(nameof(ActionDisplay));
            // Order matters: the parameter dropdown must learn its new choices before it is
            // told which one is selected, otherwise the selection is applied to the old list.
            Notify(nameof(ParameterOptions));
            Notify(nameof(HasParameter));
            Notify(nameof(Parameter));
            Notify(nameof(ParameterText));
        }
    }

    public bool Enabled { get; set; } = true;

    /// <summary>Subscription id for <see cref="EWorkflowAction.UpdateSubscriptions"/>; empty means all.</summary>
    public string? SubId { get; set; }

    /// <summary>Action dependent argument: sort column, test type or system proxy type.</summary>
    public string? Parameter
    {
        get => _parameter;
        set
        {
            if (_parameter == value)
            {
                return;
            }
            _parameter = value;
            Notify(nameof(Parameter));
        }
    }

    /// <summary>Action dependent flag: ascending sort, or update through proxy.</summary>
    public bool BoolParameter { get; set; }

    /// <summary>Editor-only action name, so the step grid can offer a plain text dropdown.</summary>
    [JsonIgnore]
    public string? ActionDisplay
    {
        get => Action.ToString();
        set
        {
            if (Enum.TryParse<EWorkflowAction>(value, true, out var action))
            {
                Action = action;
            }
        }
    }

    /// <summary>
    /// Editor-only group name. <see cref="ViewModels.WorkflowEditViewModel"/> resolves it
    /// back into <see cref="SubId"/> when the workflow is saved.
    /// </summary>
    [JsonIgnore]
    public string? SubDisplay { get; set; }

    /// <summary>Editor-only action choices, so the step grid can bind its dropdown to the row item.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> ActionOptions { get; set; } = [];

    /// <summary>
    /// Editor-only group choices. This is the editor's live collection, so subscriptions
    /// that finish loading after a row was created still reach that row's dropdown.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> GroupOptions { get; set; } = [];

    /// <summary>
    /// Editor-only parameter choices for <see cref="Action"/>; empty for actions that take
    /// none. Every parameter is one of these, so the editor offers a closed list instead of
    /// free text and a value the runner would ignore cannot be typed in.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> ParameterOptions => WorkflowStepOptions.ParametersFor(Action);

    /// <summary>Editor-only flag: false for actions that take no parameter.</summary>
    [JsonIgnore]
    public bool HasParameter => ParameterOptions.Count > 0;

    /// <summary>
    /// Editor-only binding target for the parameter cell, matching a non-null item of
    /// <see cref="ParameterOptions"/>. Null (an empty cell) when the action takes no parameter.
    /// </summary>
    [JsonIgnore]
    public string? ParameterText
    {
        get => Parameter;
        set => Parameter = value.IsNotEmpty() ? value : null;
    }

    private void Notify(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
