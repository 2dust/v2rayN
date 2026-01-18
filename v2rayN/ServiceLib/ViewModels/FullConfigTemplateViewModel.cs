namespace ServiceLib.ViewModels;

public class FullConfigTemplateViewModel : MyReactiveObject
{
    #region Reactive

    [Reactive]
    public bool EnableFullConfigTemplate4Ray { get; set; }

    [Reactive]
    public bool EnableFullConfigTemplate4Singbox { get; set; }

    [Reactive]
    public string FullConfigTemplate4Ray { get; set; }

    [Reactive]
    public string FullConfigTemplate4Singbox { get; set; }

    [Reactive]
    public string FullTunConfigTemplate4Singbox { get; set; }

    [Reactive]
    public bool AddProxyOnly4Ray { get; set; }

    [Reactive]
    public bool AddProxyOnly4Singbox { get; set; }

    [Reactive]
    public string ProxyDetour4Ray { get; set; }

    [Reactive]
    public string ProxyDetour4Singbox { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    #endregion Reactive

    public FullConfigTemplateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;
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
        {
            return;
        }

        if (!await SaveSingboxConfigAsync())
        {
            return;
        }

        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        _ = UpdateView?.Invoke(EViewAction.CloseWindow, null);
    }

    private async Task<bool> SaveXrayConfigAsync()
    {
        var item = await AppManager.Instance.GetFullConfigTemplateItem(ECoreType.Xray);
        item.Enabled = EnableFullConfigTemplate4Ray;
        item.Config = null;

        item.Config = FullConfigTemplate4Ray;

        item.AddProxyOnly = AddProxyOnly4Ray;
        item.ProxyDetour = ProxyDetour4Ray;

        await ConfigHandler.SaveFullConfigTemplate(Config, item);
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

        await ConfigHandler.SaveFullConfigTemplate(Config, item);
        return true;
    }
}
