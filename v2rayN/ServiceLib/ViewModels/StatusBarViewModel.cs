using System.Reactive;
using System.Text;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace ServiceLib.ViewModels;

public class StatusBarViewModel : MyReactiveObject
{
    #region ObservableCollection

    private IObservableCollection<RoutingItem> _routingItems = new ObservableCollectionExtended<RoutingItem>();
    public IObservableCollection<RoutingItem> RoutingItems => _routingItems;

    private IObservableCollection<ComboItem> _servers = new ObservableCollectionExtended<ComboItem>();
    public IObservableCollection<ComboItem> Servers => _servers;

    [Reactive]
    public RoutingItem SelectedRouting { get; set; }

    [Reactive]
    public ComboItem SelectedServer { get; set; }

    [Reactive]
    public bool BlServers { get; set; }

    #endregion ObservableCollection

    public ReactiveCommand<Unit, Unit> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> AddServerViaScanCmd { get; }
    public ReactiveCommand<Unit, Unit> SubUpdateCmd { get; }
    public ReactiveCommand<Unit, Unit> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<Unit, Unit> CopyProxyCmdToClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> NotifyLeftClickCmd { get; }
    public ReactiveCommand<Unit, Unit> ShowWindowCmd { get; }
    public ReactiveCommand<Unit, Unit> HideWindowCmd { get; }

    #region System Proxy

    [Reactive]
    public bool BlSystemProxyClear { get; set; }

    [Reactive]
    public bool BlSystemProxySet { get; set; }

    [Reactive]
    public bool BlSystemProxyNothing { get; set; }

    [Reactive]
    public bool BlSystemProxyPac { get; set; }

    public ReactiveCommand<Unit, Unit> SystemProxyClearCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxySetCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxyNothingCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxyPacCmd { get; }

    [Reactive]
    public bool BlRouting { get; set; }

    [Reactive]
    public int SystemProxySelected { get; set; }

    [Reactive]
    public bool BlSystemProxyPacVisible { get; set; }

    #endregion System Proxy

    #region UI

    [Reactive]
    public string InboundDisplay { get; set; }

    [Reactive]
    public string InboundLanDisplay { get; set; }

    [Reactive]
    public string RunningServerDisplay { get; set; }

    [Reactive]
    public string RunningServerToolTipText { get; set; }

    [Reactive]
    public string RunningInfoDisplay { get; set; }

    [Reactive]
    public string SpeedProxyDisplay { get; set; }

    [Reactive]
    public string SpeedDirectDisplay { get; set; }

    [Reactive]
    public bool EnableTun { get; set; }

    [Reactive]
    public bool BlIsNonWindows { get; set; }

    #endregion UI

    public StatusBarViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        SelectedRouting = new();
        SelectedServer = new();
        RunningServerToolTipText = "-";
        BlSystemProxyPacVisible = Utils.IsWindows();
        BlIsNonWindows = Utils.IsNonWindows();

        if (_config.TunModeItem.EnableTun && AllowEnableTun())
        {
            EnableTun = true;
        }
        else
        {
            _config.TunModeItem.EnableTun = EnableTun = false;
        }

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(
                x => x.SelectedRouting,
                y => y != null && !y.Remarks.IsNullOrEmpty())
            .Subscribe(async c => await RoutingSelectedChangedAsync(c));

        this.WhenAnyValue(
                x => x.SelectedServer,
                y => y != null && !y.Text.IsNullOrEmpty())
            .Subscribe(c => ServerSelectedChanged(c));

        SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
        this.WhenAnyValue(
                x => x.SystemProxySelected,
                y => y >= 0)
            .Subscribe(async c => await DoSystemProxySelected(c));

        this.WhenAnyValue(
                x => x.EnableTun,
                y => y == true)
            .Subscribe(async c => await DoEnableTun(c));

        CopyProxyCmdToClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyProxyCmdToClipboard();
        });

        NotifyLeftClickCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            Locator.Current.GetService<MainWindowViewModel>()?.ShowHideWindow(null);
            await Task.CompletedTask;
        });
        ShowWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            Locator.Current.GetService<MainWindowViewModel>()?.ShowHideWindow(true);
            await Task.CompletedTask;
        });
        HideWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            Locator.Current.GetService<MainWindowViewModel>()?.ShowHideWindow(false);
            await Task.CompletedTask;
        });

        AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await AddServerViaClipboard();
            });
        AddServerViaScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaScan();
        });
        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(false);
        });
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(true);
        });

        //System proxy
        SystemProxyClearCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedClear);
        });
        SystemProxySetCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedChange);
        });
        SystemProxyNothingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Unchanged);
        });
        SystemProxyPacCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Pac);
        });

        #endregion WhenAnyValue && ReactiveCommand

        if (updateView != null)
        {
            InitUpdateView(updateView);
        }
        _ = Init();
    }

    private async Task Init()
    {
        await RefreshRoutingsMenu();
        await InboundDisplayStatus();
        await ChangeSystemProxyAsync(_config.SystemProxyItem.SysProxyType, true);
    }

    public void InitUpdateView(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _updateView = updateView;
        if (_updateView != null)
        {
            MessageBus.Current.Listen<string>(EMsgCommand.RefreshProfiles.ToString()).Subscribe(OnNext);
        }
    }

    private async void OnNext(string x)
    {
        await _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null);
    }

    private async Task CopyProxyCmdToClipboard()
    {
        var cmd = Utils.IsWindows() ? "set" : "export";
        var address = $"{Global.Loopback}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)}";

        var sb = new StringBuilder();
        sb.AppendLine($"{cmd} http_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} https_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} all_proxy={Global.Socks5Protocol}{address}");
        sb.AppendLine("");
        sb.AppendLine($"{cmd} HTTP_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} HTTPS_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} ALL_PROXY={Global.Socks5Protocol}{address}");

        await _updateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
    }

    private async Task AddServerViaClipboard()
    {
        var service = Locator.Current.GetService<MainWindowViewModel>();
        if (service != null)
            await service.AddServerViaClipboardAsync(null);
    }

    private async Task AddServerViaScan()
    {
        var service = Locator.Current.GetService<MainWindowViewModel>();
        if (service != null)
            await service.AddServerViaScanAsync();
    }

    private async Task UpdateSubscriptionProcess(bool blProxy)
    {
        var service = Locator.Current.GetService<MainWindowViewModel>();
        if (service != null)
            await service.UpdateSubscriptionProcess("", blProxy);
    }

    public async Task RefreshServersBiz()
    {
        await RefreshServersMenu();

        //display running server
        var running = await ConfigHandler.GetDefaultServer(_config);
        if (running != null)
        {
            RunningServerDisplay =
                RunningServerToolTipText = running.GetSummary();
        }
        else
        {
            RunningServerDisplay =
                RunningServerToolTipText = ResUI.CheckServerSettings;
        }
    }

    private async Task RefreshServersMenu()
    {
        var lstModel = await AppHandler.Instance.ProfileItems(_config.SubIndexId, "");

        _servers.Clear();
        if (lstModel.Count > _config.GuiItem.TrayMenuServersLimit)
        {
            BlServers = false;
            return;
        }

        BlServers = true;
        for (int k = 0; k < lstModel.Count; k++)
        {
            ProfileItem it = lstModel[k];
            string name = it.GetSummary();

            var item = new ComboItem() { ID = it.IndexId, Text = name };
            _servers.Add(item);
            if (_config.IndexId == it.IndexId)
            {
                SelectedServer = item;
            }
        }
    }

    private void ServerSelectedChanged(bool c)
    {
        if (!c)
        {
            return;
        }
        if (SelectedServer == null)
        {
            return;
        }
        if (SelectedServer.ID.IsNullOrEmpty())
        {
            return;
        }
        Locator.Current.GetService<ProfilesViewModel>()?.SetDefaultServer(SelectedServer.ID);
    }

    public async Task TestServerAvailability()
    {
        var item = await ConfigHandler.GetDefaultServer(_config);
        if (item == null)
        {
            return;
        }

        _updateView?.Invoke(EViewAction.DispatcherServerAvailability, ResUI.Speedtesting);

        var msg = await Task.Run(async () =>
        {
            return await ConnectionHandler.Instance.RunAvailabilityCheck();
        });

        NoticeHandler.Instance.SendMessageEx(msg);
        _updateView?.Invoke(EViewAction.DispatcherServerAvailability, msg);
    }

    public void TestServerAvailabilityResult(string msg)
    {
        RunningInfoDisplay = msg;
    }

    #region System proxy and Routings

    public async Task SetListenerType(ESysProxyType type)
    {
        if (_config.SystemProxyItem.SysProxyType == type)
        {
            return;
        }
        _config.SystemProxyItem.SysProxyType = type;
        await ChangeSystemProxyAsync(type, true);
        NoticeHandler.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {_config.SystemProxyItem.SysProxyType.ToString()}");

        SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
        await ConfigHandler.SaveConfig(_config);
    }

    public async Task ChangeSystemProxyAsync(ESysProxyType type, bool blChange)
    {
        await SysProxyHandler.UpdateSysProxy(_config, false);

        BlSystemProxyClear = (type == ESysProxyType.ForcedClear);
        BlSystemProxySet = (type == ESysProxyType.ForcedChange);
        BlSystemProxyNothing = (type == ESysProxyType.Unchanged);
        BlSystemProxyPac = (type == ESysProxyType.Pac);

        if (blChange)
        {
            _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    public async Task RefreshRoutingsMenu()
    {
        _routingItems.Clear();

        BlRouting = true;
        var routings = await AppHandler.Instance.RoutingItems();
        foreach (var item in routings)
        {
            _routingItems.Add(item);
            if (item.IsActive)
            {
                SelectedRouting = item;
            }
        }
    }

    private async Task RoutingSelectedChangedAsync(bool c)
    {
        if (!c)
        {
            return;
        }

        if (SelectedRouting == null)
        {
            return;
        }

        var item = await AppHandler.Instance.GetRoutingItem(SelectedRouting?.Id);
        if (item is null)
        {
            return;
        }

        if (await ConfigHandler.SetDefaultRouting(_config, item) == 0)
        {
            NoticeHandler.Instance.SendMessageEx(ResUI.TipChangeRouting);
            Locator.Current.GetService<MainWindowViewModel>()?.Reload();
            _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    private async Task DoSystemProxySelected(bool c)
    {
        if (!c)
        {
            return;
        }
        if (_config.SystemProxyItem.SysProxyType == (ESysProxyType)SystemProxySelected)
        {
            return;
        }
        await SetListenerType((ESysProxyType)SystemProxySelected);
    }

    private async Task DoEnableTun(bool c)
    {
        if (_config.TunModeItem.EnableTun == EnableTun)
        {
            return;
        }

        _config.TunModeItem.EnableTun = EnableTun;

        if (EnableTun && AllowEnableTun() == false)
        {
            // When running as a non-administrator, reboot to administrator mode
            if (Utils.IsWindows())
            {
                _config.TunModeItem.EnableTun = false;
                Locator.Current.GetService<MainWindowViewModel>()?.RebootAsAdmin();
                return;
            }
            else
            {
                bool? passwordResult = await _updateView?.Invoke(EViewAction.PasswordInput, null);
                if (passwordResult == false)
                {
                    _config.TunModeItem.EnableTun = false;
                    return;
                }
            }
        }
        await ConfigHandler.SaveConfig(_config);
        Locator.Current.GetService<MainWindowViewModel>()?.Reload();
    }

    private bool AllowEnableTun()
    {
        if (Utils.IsWindows())
        {
            return Utils.IsAdministrator();
        }
        else if (Utils.IsLinux())
        {
            return AppHandler.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        else if (Utils.IsOSX())
        {
            return AppHandler.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        return false;
    }

    #endregion System proxy and Routings

    #region UI

    public async Task InboundDisplayStatus()
    {
        StringBuilder sb = new();
        sb.Append($"[{EInboundProtocol.mixed}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)}");
        if (_config.Inbound.First().SecondLocalPortEnabled)
        {
            sb.Append($",{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks2)}");
        }
        sb.Append(']');
        InboundDisplay = $"{ResUI.LabLocal}:{sb}";

        if (_config.Inbound.First().AllowLANConn)
        {
            var lan = _config.Inbound.First().NewPort4LAN
                ? $"[{EInboundProtocol.mixed}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks3)}]"
                : $"[{EInboundProtocol.mixed}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)}]";
            InboundLanDisplay = $"{ResUI.LabLAN}:{lan}";
        }
        else
        {
            InboundLanDisplay = $"{ResUI.LabLAN}:{Global.None}";
        }
        await Task.CompletedTask;
    }

    public void UpdateStatistics(ServerSpeedItem update)
    {
        try
        {
            if (_config.IsRunningCore(ECoreType.sing_box))
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, EInboundProtocol.mixed, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Empty;
            }
            else
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.DirectUp), Utils.HumanFy(update.DirectDown));
            }
        }
        catch
        {
        }
    }

    #endregion UI
}
