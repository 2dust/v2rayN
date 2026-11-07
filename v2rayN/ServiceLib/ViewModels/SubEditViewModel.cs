using ServiceLib.Models.Editors;

namespace ServiceLib.ViewModels;

public partial class SubEditViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial SubscriptionEditorModel SelectedSource { get; set; }

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

        SelectedSource = SubscriptionEditorModel.FromEntity(subItem);
        CustomCoreType = subItem.CustomCoreType?.ToString() ?? string.Empty;
        PrevProfile = subItem.PrevProfile;
        NextProfile = subItem.NextProfile;
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

        if (!HttpRequestHeadersHelper.TryParse(SelectedSource.RequestHeaders, out _))
        {
            NoticeManager.Instance.Enqueue(ResUI.SubRequestHeadersInvalid);
            return;
        }

        var item = SelectedSource.ToEntity();
        item.CustomCoreType = Enum.TryParse<ECoreType>(CustomCoreType, out var coreType) ? coreType : null;
        item.PrevProfile = PrevProfile;
        item.NextProfile = NextProfile;

        if (await ConfigHandler.AddSubItem(_config, item) == 0)
        {
            SelectedSource = SubscriptionEditorModel.FromEntity(item);
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
