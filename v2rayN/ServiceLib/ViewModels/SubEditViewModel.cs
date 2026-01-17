namespace ServiceLib.ViewModels;

public class SubEditViewModel : MyReactiveObject
{
    [Reactive]
    public SubItem SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSubAsync();
        });

        SelectedSource = subItem.Id.IsNullOrEmpty() ? subItem : JsonUtils.DeepCopy(subItem);
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
            if (uri is null)
            {
                NoticeManager.Instance.Enqueue(ResUI.InvalidUrlTip);
                return;
            }
            //Do not allow http protocol
            if (url.StartsWith(AppConfig.HttpProtocol) && !Utils.IsPrivateNetwork(uri.IdnHost))
            {
                NoticeManager.Instance.Enqueue(ResUI.InsecureUrlProtocol);
                //return;
            }
        }

        if (await ConfigHandler.AddSubItem(Config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            UpdateView?.Invoke(EViewAction.CloseWindow, null);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
