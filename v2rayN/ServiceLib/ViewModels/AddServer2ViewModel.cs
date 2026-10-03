namespace ServiceLib.ViewModels;

public partial class AddServer2ViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    public Interaction<RxVoid, string?> BrowseConfigFileInteraction { get; } = new();

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

    [Reactive]
    public partial string? CoreType { get; set; }

    [Reactive]
    public partial bool IsSingboxEndpoint { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> BrowseServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> EditServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveServerCmd { get; }
    public bool IsModified { get; set; }

    public AddServer2ViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;

        BrowseServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileName = await BrowseConfigFileInteraction.HandleSafe(RxVoid.Default);
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
        var coreStr = SelectedSource?.CoreType?.ToString();
        coreStr = coreStr.IsNullOrEmpty() ? Global.CoreTypes.FirstOrDefault() : coreStr;
        CoreType = coreStr;
        IsSingboxEndpoint = SelectedSource?.GetProtocolExtra()?.IsSingboxEndpoint ?? false;
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
        SelectedSource.SetProtocolExtra(SelectedSource?.GetProtocolExtra() with
        {
            IsSingboxEndpoint = IsSingboxEndpoint ? true : null,
        });

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
        var result = item.ConfigType == EConfigType.Outbound ? await ConfigHandler.AddCustomOutboundServer(_config, item, false) : await ConfigHandler.AddCustomServer(_config, item, false);
        if (result == 0)
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
