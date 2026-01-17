namespace ServiceLib.ViewModels;

public class StatusBarViewModel : MyReactiveObject
{
    private static readonly Lazy<StatusBarViewModel> _instance = new(() => new(null));

    private static readonly CompositeFormat _speedDisplayFormat = CompositeFormat.Parse(ResUI.SpeedDisplayText);

    public static StatusBarViewModel Instance => _instance.Value;

    #region ObservableCollection

    public IObservableCollection<RoutingItem> RoutingItems { get; } = new ObservableCollectionExtended<RoutingItem>();

    public IObservableCollection<ComboItem> Servers { get; } = new ObservableCollectionExtended<ComboItem>();

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
        Config = AppManager.Instance.Config;
        SelectedRouting = new();
        SelectedServer = new();
        RunningServerToolTipText = "-";
        BlSystemProxyPacVisible = Utils.IsWindows();
        BlIsNonWindows = Utils.IsNonWindows();

        if (Config.TunModeItem.EnableTun && AllowEnableTun())
        {
            EnableTun = true;
        }
        else
        {
            Config.TunModeItem.EnableTun = EnableTun = false;
        }

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(
                x => x.SelectedRouting,
                y => y is not null && !y.Remarks.IsNullOrEmpty())
            .Subscribe(async c => await RoutingSelectedChangedAsync(c));

        this.WhenAnyValue(
                x => x.SelectedServer,
                y => y is not null && !y.Text.IsNullOrEmpty())
            .Subscribe(ServerSelectedChanged);

        SystemProxySelected = (int)Config.SystemProxyItem.SysProxyType;
        this.WhenAnyValue(
                x => x.SystemProxySelected,
                y => y >= 0)
            .Subscribe(async c => await DoSystemProxySelected(c));

        this.WhenAnyValue(
                x => x.EnableTun,
                y => y == true)
            .Subscribe(async c => await DoEnableTun());

        CopyProxyCmdToClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyProxyCmdToClipboard();
        });

        NotifyLeftClickCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(null);
            await Task.CompletedTask;
        });
        ShowWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(true);
            await Task.CompletedTask;
        });
        HideWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(false);
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

        #region AppEvents

        if (updateView is not null)
        {
            InitUpdateView(updateView);
        }

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async result => await UpdateStatistics(result));

        AppEvents.RoutingsMenuRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshRoutingsMenu());

        AppEvents.TestServerRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await TestServerAvailability());

        AppEvents.InboundDisplayRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await InboundDisplayStatus());

        AppEvents.SysProxyChangeRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async result => await SetListenerType(result));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        await ConfigHandler.InitBuiltinRouting(Config);
        await RefreshRoutingsMenu();
        await InboundDisplayStatus();
        await ChangeSystemProxyAsync(Config.SystemProxyItem.SysProxyType, true);
    }

    public void InitUpdateView(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        UpdateView = updateView;
        if (UpdateView is not null)
        {
            AppEvents.ProfilesRefreshRequested
              .AsObservable()
              .ObserveOn(RxApp.MainThreadScheduler)
              .Subscribe(async _ => await RefreshServersBiz()); //.DisposeWith(_disposables);
        }
    }

    private async Task CopyProxyCmdToClipboard()
    {
        var cmd = Utils.IsWindows() ? "set" : "export";
        var address = $"{AppConfig.Loopback}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}";

        var sb = new StringBuilder();
        sb.AppendLine($"{cmd} http_proxy={AppConfig.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} https_proxy={AppConfig.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} all_proxy={AppConfig.Socks5Protocol}{address}");
        sb.AppendLine("");
        sb.AppendLine($"{cmd} HTTP_PROXY={AppConfig.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} HTTPS_PROXY={AppConfig.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} ALL_PROXY={AppConfig.Socks5Protocol}{address}");

        await UpdateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
    }

    private static async Task AddServerViaClipboard()
    {
        AppEvents.AddServerViaClipboardRequested.Publish();
        await Task.Delay(1000);
    }

    private static async Task AddServerViaScan()
    {
        AppEvents.AddServerViaScanRequested.Publish();
        await Task.Delay(1000);
    }

    private static async Task UpdateSubscriptionProcess(bool blProxy)
    {
        AppEvents.SubscriptionsUpdateRequested.Publish(blProxy);
        await Task.Delay(1000);
    }

    private async Task RefreshServersBiz()
    {
        await RefreshServersMenu();

        //display running server
        var running = await ConfigHandler.GetDefaultServer(Config);
        if (running is not null)
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
        var lstModel = await AppManager.Instance.ProfileItems(Config.SubIndexId, "");

        Servers.Clear();
        if (lstModel.Count > Config.GuiItem.TrayMenuServersLimit)
        {
            BlServers = false;
            return;
        }

        BlServers = true;
        for (var k = 0; k < lstModel.Count; k++)
        {
            ProfileItem it = lstModel[k];
            var name = it.GetSummary();

            var item = new ComboItem() { ID = it.IndexId, Text = name };
            Servers.Add(item);
            if (Config.IndexId == it.IndexId)
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
        if (SelectedServer is null)
        {
            return;
        }
        if (SelectedServer.ID.IsNullOrEmpty())
        {
            return;
        }
        AppEvents.SetDefaultServerRequested.Publish(SelectedServer.ID);
    }

    public async Task TestServerAvailability()
    {
        var item = await ConfigHandler.GetDefaultServer(Config);
        if (item is null)
        {
            return;
        }

        await TestServerAvailabilitySub(ResUI.Speedtesting);

        var msg = await Task.Run(ConnectionHandler.RunAvailabilityCheck);

        NoticeManager.Instance.SendMessageEx(msg);
        await TestServerAvailabilitySub(msg);
    }

    private async Task TestServerAvailabilitySub(string msg)
    {
        RxApp.MainThreadScheduler.Schedule(msg, (scheduler, msg) =>
        {
            _ = TestServerAvailabilityResult(msg);
            return Disposable.Empty;
        });
        await Task.CompletedTask;
    }

    public async Task TestServerAvailabilityResult(string msg)
    {
        RunningInfoDisplay = msg;
        await Task.CompletedTask;
    }

    #region System proxy and Routings

    private async Task SetListenerType(ESysProxyType type)
    {
        if (Config.SystemProxyItem.SysProxyType == type)
        {
            return;
        }
        Config.SystemProxyItem.SysProxyType = type;
        await ChangeSystemProxyAsync(type, true);
        NoticeManager.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {Config.SystemProxyItem.SysProxyType}");

        SystemProxySelected = (int)Config.SystemProxyItem.SysProxyType;
        await ConfigHandler.SaveConfig(Config);
    }

    public async Task ChangeSystemProxyAsync(ESysProxyType type, bool blChange)
    {
        await SysProxyHandler.UpdateSysProxy(Config, false);

        BlSystemProxyClear = type == ESysProxyType.ForcedClear;
        BlSystemProxySet = type == ESysProxyType.ForcedChange;
        BlSystemProxyNothing = type == ESysProxyType.Unchanged;
        BlSystemProxyPac = type == ESysProxyType.Pac;

        if (blChange)
        {
            UpdateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    private async Task RefreshRoutingsMenu()
    {
        RoutingItems.Clear();

        BlRouting = true;
        var routings = await AppManager.Instance.RoutingItems();
        foreach (var item in routings)
        {
            RoutingItems.Add(item);
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

        if (SelectedRouting is null)
        {
            return;
        }

        var item = await AppManager.Instance.GetRoutingItem(SelectedRouting?.Id);
        if (item is null)
        {
            return;
        }

        if (await ConfigHandler.SetDefaultRouting(Config, item) == 0)
        {
            NoticeManager.Instance.SendMessageEx(ResUI.TipChangeRouting);
            AppEvents.ReloadRequested.Publish();
            UpdateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    private async Task DoSystemProxySelected(bool c)
    {
        if (!c)
        {
            return;
        }
        if (Config.SystemProxyItem.SysProxyType == (ESysProxyType)SystemProxySelected)
        {
            return;
        }
        await SetListenerType((ESysProxyType)SystemProxySelected);
    }

    private async Task DoEnableTun()
    {
        if (Config.TunModeItem.EnableTun == EnableTun)
        {
            return;
        }

        Config.TunModeItem.EnableTun = EnableTun;

        if (EnableTun && AllowEnableTun() == false)
        {
            // When running as a non-administrator, reboot to administrator mode
            if (Utils.IsWindows())
            {
                Config.TunModeItem.EnableTun = false;
                await AppManager.Instance.RebootAsAdmin();
                return;
            }
            else
            {
                bool? passwordResult = await UpdateView?.Invoke(EViewAction.PasswordInput, null);
                if (passwordResult == false)
                {
                    Config.TunModeItem.EnableTun = false;
                    return;
                }
            }
        }
        await ConfigHandler.SaveConfig(Config);
        AppEvents.ReloadRequested.Publish();
    }

    private static bool AllowEnableTun()
    {
        if (Utils.IsWindows())
        {
            return Utils.IsAdministrator();
        }
        else if (Utils.IsLinux())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        else if (Utils.IsMacOS())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        return false;
    }

    #endregion System proxy and Routings

    #region UI

    private async Task InboundDisplayStatus()
    {
        StringBuilder sb = new();
        sb.Append($"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}");
        if (Config.Inbound.First().SecondLocalPortEnabled)
        {
            sb.Append($",{AppManager.Instance.GetLocalPort(EInboundProtocol.socks2)}");
        }
        sb.Append(']');
        InboundDisplay = $"{ResUI.LabLocal}:{sb}";

        if (Config.Inbound.First().AllowLANConn)
        {
            var lan = Config.Inbound.First().NewPort4LAN
                ? $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks3)}]"
                : $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}]";
            InboundLanDisplay = $"{ResUI.LabLAN}:{lan}";
        }
        else
        {
            InboundLanDisplay = $"{ResUI.LabLAN}:{AppConfig.None}";
        }
        await Task.CompletedTask;
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!Config.GuiItem.DisplayRealTimeSpeed)
        {
            return;
        }

        try
        {
            if (AppManager.Instance.IsRunningCore(ECoreType.sing_box))
            {
                SpeedProxyDisplay = string.Format(CultureInfo.CurrentCulture, _speedDisplayFormat, EInboundProtocol.mixed, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Empty;
            }
            else
            {
                SpeedProxyDisplay = string.Format(CultureInfo.CurrentCulture, _speedDisplayFormat, AppConfig.ProxyTag, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Format(CultureInfo.CurrentCulture, _speedDisplayFormat, AppConfig.DirectTag, Utils.HumanFy(update.DirectUp), Utils.HumanFy(update.DirectDown));
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    #endregion UI
}
