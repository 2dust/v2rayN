namespace ServiceLib.ViewModels;

public partial class FullConfigTemplateViewModel : MyReactiveObject
{
    #region Reactive

    [Reactive]
    private bool _enableFullConfigTemplate4Ray;

    [Reactive]
    private bool _enableFullConfigTemplate4Singbox;

    [Reactive]
    private string _fullConfigTemplate4Ray;

    [Reactive]
    private string _fullConfigTemplate4Singbox;

    [Reactive]
    private string _fullTunConfigTemplate4Singbox;

    [Reactive]
    private bool _addProxyOnly4Ray;

    [Reactive]
    private bool _addProxyOnly4Singbox;

    [Reactive]
    private string _proxyDetour4Ray;

    [Reactive]
    private string _proxyDetour4Singbox;

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    #endregion Reactive

    public FullConfigTemplateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettingAsync();
        });

        _ = Init();
    }

    private async Task Init()
    {
        var item = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.Xray);
        EnableFullConfigTemplate4Ray = item?.Enabled ?? false;
        FullConfigTemplate4Ray = item?.Config ?? string.Empty;
        AddProxyOnly4Ray = item?.AddProxyOnly ?? false;
        ProxyDetour4Ray = item?.ProxyDetour ?? string.Empty;

        var item2 = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.sing_box);
        EnableFullConfigTemplate4Singbox = item2?.Enabled ?? false;
        FullConfigTemplate4Singbox = item2?.Config ?? string.Empty;
        FullTunConfigTemplate4Singbox = item2?.TunConfig ?? string.Empty;
        AddProxyOnly4Singbox = item2?.AddProxyOnly ?? false;
        ProxyDetour4Singbox = item2?.ProxyDetour ?? string.Empty;
    }

    private async Task SaveSettingAsync()
    {
        if (!await SaveXrayConfigAsync())
            return;

        if (!await SaveSingboxConfigAsync())
            return;

        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        _ = _updateView?.Invoke(EViewAction.CloseWindow, null);
    }

    private async Task<bool> SaveXrayConfigAsync()
    {
        var item = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.Xray);
        item.Enabled = EnableFullConfigTemplate4Ray;
        item.Config = null;

        item.Config = FullConfigTemplate4Ray;

        item.AddProxyOnly = AddProxyOnly4Ray;
        item.ProxyDetour = ProxyDetour4Ray;

        await ConfigHandler.SaveFullConfigTemplate(_config, item);
        return true;
    }

    private async Task<bool> SaveSingboxConfigAsync()
    {
        var item = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.sing_box);
        item.Enabled = EnableFullConfigTemplate4Singbox;
        item.Config = null;
        item.TunConfig = null;

        item.Config = FullConfigTemplate4Singbox;
        item.TunConfig = FullTunConfigTemplate4Singbox;

        item.AddProxyOnly = AddProxyOnly4Singbox;
        item.ProxyDetour = ProxyDetour4Singbox;

        await ConfigHandler.SaveFullConfigTemplate(_config, item);
        return true;
    }
}
