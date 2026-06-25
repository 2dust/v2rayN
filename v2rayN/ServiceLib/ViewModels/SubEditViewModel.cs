namespace ServiceLib.ViewModels;

public class SubEditViewModel : MyReactiveObject
{
    public Interaction<Unit, Unit> CloseWindowInteraction { get; } = new();

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public SubEditViewModel(SubItem subItem)
    {
        _config = AppManager.Instance.Config;

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

        if (await ConfigHandler.AddSubItem(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            await CloseWindowInteraction.Handle(Unit.Default);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
