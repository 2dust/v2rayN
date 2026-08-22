namespace ServiceLib.ViewModels;

public partial class SubEditViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial SubItem SelectedSource { get; set; }

    [Reactive]
    public partial string CustomCoreType { get; set; }

    [Reactive]
    public partial string PrevProfile { get; set; }

    [Reactive]
    public partial string NextProfile { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> SelectPrevProfileCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SelectNextProfileCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem)
    {
        _config = AppManager.Instance.Config;

        SelectPrevProfileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var profileItem = await SelectProfileAsync();
            if (profileItem != null)
            {
                PrevProfile = profileItem.Remarks;
            }
        });
        SelectNextProfileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var profileItem = await SelectProfileAsync();
            if (profileItem != null)
            {
                NextProfile = profileItem.Remarks;
            }
        });
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSubAsync();
        });

        SelectedSource = subItem.Id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
        CustomCoreType = SelectedSource.CustomCoreType?.ToString() ?? string.Empty;
        PrevProfile = SelectedSource.PrevProfile;
        NextProfile = SelectedSource.NextProfile;
    }

    private async Task SaveSubAsync()
    {
        var remarks = SelectedSource.Remarks;
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        var url = SelectedSource.Url;
        if (url.IsNotEmpty())
        {
            var uri = Utils.TryUri(url);
            if (uri == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.InvalidUrlTip);
                return;
            }
            //Do not allow http protocol
            if (url.StartsWith(Global.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
            {
                NoticeManager.Instance.Enqueue(ResUI.InsecureUrlProtocol);
                //return;
            }
        }

        SelectedSource.CustomCoreType = Enum.TryParse<ECoreType>(CustomCoreType, out var coreType) ? coreType : null;
        SelectedSource.PrevProfile = PrevProfile;
        SelectedSource.NextProfile = NextProfile;

        if (await ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private async Task<ProfileItem?> SelectProfileAsync()
    {
        var profileSelectViewModel = new ProfilesSelectViewModel();
        profileSelectViewModel.SetConfigTypeFilter([EConfigType.Custom], exclude: true);
        var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(profileSelectViewModel);
        if (result != true)
        {
            return null;
        }
        var profileItem = await profileSelectViewModel.GetProfileItem();
        return profileItem;
    }
}
