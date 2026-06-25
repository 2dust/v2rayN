namespace ServiceLib.ViewModels;

public class SubSettingViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<string, Unit> ShareSubInteraction { get; } = new();
    public Interaction<SubItem, bool> EditSubInteraction { get; } = new();

    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public IList<SubItem> SelectedSources { get; set; }

    public ReactiveCommand<Unit, Unit> SubAddCmd { get; }
    public ReactiveCommand<Unit, Unit> SubDeleteCmd { get; }
    public ReactiveCommand<Unit, Unit> SubEditCmd { get; }
    public ReactiveCommand<Unit, Unit> SubShareCmd { get; }
    public bool IsModified { get; set; }

    public SubSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedSource,
           selectedSource => selectedSource != null && !selectedSource.Id.IsNullOrEmpty());

        SubAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        SubDeleteCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        }, canEditRemove);
        SubEditCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        }, canEditRemove);
        SubShareCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareSubInteraction.Handle(SelectedSource?.Url);
        }, canEditRemove);

        _ = Init();
    }

    private async Task Init()
    {
        SelectedSource = new();

        await RefreshSubItems();
    }

    public async Task RefreshSubItems()
    {
        SubItems.Clear();
        SubItems.AddRange(await AppManager.Instance.SubItems());
    }

    public async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(SelectedSource?.Id);
            if (item is null)
            {
                return;
            }
        }
        if (await EditSubInteraction.Handle(item) == true)
        {
            await RefreshSubItems();
            IsModified = true;
        }
    }

    private async Task DeleteSubAsync()
    {
        if (await ShowYesNoInteraction.Handle(ResUI.RemoveServer) == false)
        {
            return;
        }

        foreach (var it in SelectedSources ?? [SelectedSource])
        {
            await ConfigHandler.DeleteSubItem(_config, it.Id);
        }
        await RefreshSubItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }
}
