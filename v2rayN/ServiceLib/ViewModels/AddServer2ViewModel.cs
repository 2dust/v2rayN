namespace ServiceLib.ViewModels;

public class AddServer2ViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    public Interaction<Unit, string?> BrowseConfigFileInteraction { get; } = new();

    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public string? CoreType { get; set; }

    public ReactiveCommand<Unit, Unit> BrowseServerCmd { get; }
    public ReactiveCommand<Unit, Unit> EditServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SaveServerCmd { get; }
    public bool IsModified { get; set; }

    public AddServer2ViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;

        BrowseServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileName = await BrowseConfigFileInteraction.Handle(Unit.Default);
            if (fileName.IsNullOrEmpty())
            {
                return;
            }
            await BrowseServer(fileName);
        });
        EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditServer();
        });
        SaveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });

        SelectedSource = profileItem.IndexId.IsNullOrEmpty() ? profileItem : JsonUtils.DeepCopy(profileItem);
        CoreType = SelectedSource?.CoreType?.ToString();
    }

    private async Task SaveServerAsync()
    {
        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (SelectedSource.Address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddressCustom);
            return;
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);

        if (await ConfigHandler.EditCustomServer(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    public async Task BrowseServer(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }

        var item = await AppManager.Instance.GetProfileItem(SelectedSource.IndexId);
        item ??= SelectedSource;
        item.Address = fileName;
        if (await ConfigHandler.AddCustomServer(_config, item, false) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.SuccessfullyImportedCustomServer);
            if (item.IndexId.IsNotEmpty())
            {
                SelectedSource = JsonUtils.DeepCopy(item);
            }
            IsModified = true;
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.FailedImportedCustomServer);
        }
    }

    private async Task EditServer()
    {
        var address = SelectedSource.Address;
        if (address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddressCustom);
            return;
        }

        address = Utils.GetConfigPath(address);
        if (File.Exists(address))
        {
            ProcUtils.ProcessStart(address);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.FailedReadConfiguration);
        }
        await Task.CompletedTask;
    }
}
