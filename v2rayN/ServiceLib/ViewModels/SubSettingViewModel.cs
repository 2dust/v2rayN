namespace ServiceLib.ViewModels;

public class SubSettingViewModel : MyReactiveObject
{
    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public IList<SubItem> SelectedSources { get; set; }

    public ReactiveCommand<Unit, Unit> SubAddCmd { get; }
    public ReactiveCommand<Unit, Unit> SubDeleteCmd { get; }
    public ReactiveCommand<Unit, Unit> SubEditCmd { get; }
    public ReactiveCommand<Unit, Unit> SubShareCmd { get; }
    public bool IsModified { get; set; }

    public SubSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedSource,
           selectedSource => selectedSource is not null && !selectedSource.Id.IsNullOrEmpty());

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
            await UpdateView?.Invoke(EViewAction.ShareSub, SelectedSource?.Url);
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
        if (await UpdateView?.Invoke(EViewAction.SubEditWindow, item) == true)
        {
            await RefreshSubItems();
            IsModified = true;
        }
    }

    private async Task DeleteSubAsync()
    {
        if (await UpdateView?.Invoke(EViewAction.ShowYesNo, null) == false)
        {
            return;
        }

        foreach (var it in SelectedSources ?? [SelectedSource])
        {
            await ConfigHandler.DeleteSubItem(Config, it.Id);
        }
        await RefreshSubItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }
}
