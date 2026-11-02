namespace ServiceLib.ViewModels;

public partial class MainWindowViewModel : MyReactiveObject
{
    public Interaction<RxVoid, string?> ReadTextFromClipboardInteraction { get; } = new();
    public Interaction<RxVoid, byte[]?> ScanScreenInteraction { get; } = new();
    public Interaction<RxVoid, string?> BrowseImageFileInteraction { get; } = new();
    public Interaction<bool?, RxVoid> ShowHideWindowInteraction { get; } = new();

    public bool DesignMode { get; set; }

    public ProfilesViewModel ProfilesViewModel { get; } = new();
    public MsgViewModel MsgViewModel { get; } = new();
    public ClashProxiesViewModel ClashProxiesViewModel { get; } = new();
    public ClashConnectionsViewModel ClashConnectionsViewModel { get; } = new();
    public CheckUpdateViewModel CheckUpdateViewModel { get; } = new();
    public BackupAndRestoreViewModel BackupAndRestoreViewModel { get; } = new();
    public StatusBarViewModel StatusBarViewModel { get; } = StatusBarViewModel.Instance;

    #region Menu

    //servers
    public ReactiveCommand<RxVoid, RxVoid> AddVmessServerCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddVlessServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddShadowsocksServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddSocksServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddHttpServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddTrojanServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddHysteria2ServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddTuicServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddWireguardServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddAnytlsServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddNaiveServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddMasqueServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddCustomServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddCustomOutboundServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddPolicyGroupServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddProxyChainServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaImageCmd { get; }

    //Subscription
    public ReactiveCommand<RxVoid, RxVoid> SubSettingCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> SubUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubGroupUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubGroupUpdateViaProxyCmd { get; }

    //Setting
    public ReactiveCommand<RxVoid, RxVoid> OptionSettingCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RoutingSettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DNSSettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FullConfigTemplateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GlobalHotkeySettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RebootAsAdminCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearServerStatisticsCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenTheFileLocationCmd { get; }

    //Presets
    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetDefaultCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetRussiaCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetIranCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReloadCmd { get; }

    [Reactive]
    public partial bool BlReloadEnabled { get; set; }

    [Reactive]
    public partial bool ShowClashUI { get; set; }

    [Reactive]
    public partial int TabMainSelectedIndex { get; set; }

    [Reactive] public partial bool BlIsWindows { get; set; }

    [Reactive] public partial bool BlNewUpdate { get; set; }

    [Reactive] public partial EGirdOrientation MainGirdOrientation { get; set; }

    #endregion Menu

    #region Init

    public MainWindowViewModel()
    {
        _config = AppManager.Instance.Config;
        BlIsWindows = Utils.IsWindows();
        MainGirdOrientation = _config.UiItem.MainGirdOrientation;

        #region WhenAnyValue && ReactiveCommand

        //servers
        AddVmessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.VMess);
        });
        AddVlessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.VLESS);
        });
        AddShadowsocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Shadowsocks);
        });
        AddSocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.SOCKS);
        });
        AddHttpServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.HTTP);
        });
        AddTrojanServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Trojan);
        });
        AddHysteria2ServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Hysteria2);
        });
        AddTuicServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.TUIC);
        });
        AddWireguardServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.WireGuard);
        });
        AddAnytlsServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Anytls);
        });
        AddNaiveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Naive);
        });
        AddMasqueServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.MASQUE);
        });
        AddCustomServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Custom);
        });
        AddCustomOutboundServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Outbound);
        });
        AddPolicyGroupServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.PolicyGroup);
        });
        AddProxyChainServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.ProxyChain);
        });
        AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaClipboardAsync(null);
        });
        AddServerViaScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaScanAsync();
        });
        AddServerViaImageCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaImageAsync();
        });

        //Subscription
        SubSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SubSettingAsync();
        });

        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess("", false);
        });
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess("", true);
        });
        SubGroupUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(_config.SubIndexId, false);
        });
        SubGroupUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(_config.SubIndexId, true);
        });

        //Setting
        OptionSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OptionSettingAsync();
        });
        RoutingSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RoutingSettingAsync();
        });
        DNSSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DNSSettingAsync();
        });
        FullConfigTemplateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FullConfigTemplateAsync();
        });
        GlobalHotkeySettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var globalHotkeySettingViewModel = new GlobalHotkeySettingViewModel();
            if (await AppManager.Instance.WindowDialog.ShowDialogAsync(globalHotkeySettingViewModel) == true)
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            }
        });
        RebootAsAdminCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AppManager.Instance.RebootAsAdmin();
        });
        ClearServerStatisticsCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClearServerStatistics();
        });
        OpenTheFileLocationCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OpenTheFileLocation();
        });

        ReloadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Reload();
        });

        RegionalPresetDefaultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Default);
        });

        RegionalPresetRussiaCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Russia);
        });

        RegionalPresetIranCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Iran);
        });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.AddServerViaClipboardRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaClipboardAsync(null));

        AppEvents.HasUpdateNotified
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(bl => BlNewUpdate = bl);

        #endregion AppEvents

        ProfilesViewModel.RefreshServersRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await RefreshServersDispatcherAsync());

        var vmReloadRequestedList = new List<IObservable<RxVoid>>
        {
            ProfilesViewModel.ReloadRequested.AsObservable(),
            StatusBarViewModel.ReloadRequested.AsObservable(),
            CheckUpdateViewModel.ReloadRequested.AsObservable(),
        };

        foreach (var reloadRequested in vmReloadRequestedList)
        {
            reloadRequested
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeAsync(async _ => await Reload());
        }

        StatusBarViewModel.AddServerViaScanRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaScanAsync());

        StatusBarViewModel.AddServerViaClipboardRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaClipboardAsync(null));

        StatusBarViewModel.ShowHideWindowRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async blShow =>
            {
                await ShowHideWindowInteraction.HandleSafe(blShow);
            });

        StatusBarViewModel.SetDefaultServerRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async indexId => await ProfilesViewModel.SetDefaultServer(indexId));

        StatusBarViewModel.SubscriptionsUpdateRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async blProxy => await UpdateSubscriptionProcess("", blProxy));

        _ = Init();
    }

    private async Task Init()
    {
        AppManager.Instance.ShowInTaskbar = true;

        if (DesignMode)
        {
            return;
        }

        //await ConfigHandler.InitBuiltinRouting(_config);
        await ConfigHandler.InitBuiltinDNS(_config);
        await ConfigHandler.InitBuiltinFullConfigTemplate(_config);
        await ProfileExManager.Instance.Init();
        await CoreManager.Instance.Init(_config, UpdateHandler);
        await CertPemManager.Instance.Init(_config);
        TaskManager.Instance.RegUpdateTask(_config, UpdateTaskHandler, UpdateSubscriptionCompleted);

        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            await StatisticsManager.Instance.Init(_config, UpdateStatisticsHandler);
        }
        await RefreshServersDispatcherAsync();

        await Reload();
    }

    #endregion Init

    #region Actions

    private async Task UpdateHandler(bool notify, string msg)
    {
        NoticeManager.Instance.SendMessage(msg);
        if (notify)
        {
            NoticeManager.Instance.Enqueue(msg);
        }
        await Task.CompletedTask;
    }

    private async Task UpdateTaskHandler(bool success, string msg)
    {
        NoticeManager.Instance.SendMessageEx(msg);
        if (success)
        {
            await RefreshServersDispatcherAsync();
            await Reload();

            if (_config.UiItem.EnableAutoAdjustMainLvColWidth)
            {
                await ProfilesViewModel.AdjustMainLvColWidth();
            }
        }
    }

    private async Task UpdateSubscriptionCompleted(SubscriptionRefreshResult result)
    {
        if (!result.Success)
            return;
        await RefreshServersDispatcherAsync(preserveDefaultServer: true);
        await Reload(onlyIfChanged: true);
        if (_config.UiItem.EnableAutoAdjustMainLvColWidth)
            await ProfilesViewModel.AdjustMainLvColWidth();
    }

    private async Task UpdateStatisticsHandler(ServerSpeedItem update)
    {
        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }
        AppEvents.DispatcherStatisticsRequested.Publish(update);
        await Task.CompletedTask;
    }

    #endregion Actions

    #region Servers && Groups

    private readonly SemaphoreSlim _refreshServersSemaphore = new(1, 1);

    private async Task RefreshServers(bool preserveDefaultServer = false)
    {
        await ProfilesViewModel.RefreshServersBiz(preserveDefaultServer);
        await StatusBarViewModel.RefreshServersBiz(preserveDefaultServer);

        // await Task.Delay(200);
    }

    private async Task RefreshServersDispatcherAsync(bool preserveDefaultServer = false)
    {
        //await Observable.Start(async () => await RefreshServers(), RxSchedulers.MainThreadScheduler);
        await _refreshServersSemaphore.WaitAsync();
        try
        {
            await Signal.FromAsync(async () =>
            {
                await RefreshServers(preserveDefaultServer);
                return RxVoid.Default;
            })
                .SubscribeOn(RxSchedulers.MainThreadScheduler)
                .ToTask();
        }
        finally
        {
            _refreshServersSemaphore.Release();
        }
    }

    private async Task RefreshSubscriptions()
    {
        //await Observable.Start(async () => await ProfilesViewModel.RefreshSubscriptions(), RxSchedulers.MainThreadScheduler);
        await Signal.FromAsync(async () =>
            {
                await ProfilesViewModel.RefreshSubscriptions();
                return RxVoid.Default;
            })
            .SubscribeOn(RxSchedulers.MainThreadScheduler)
            .ToTask();
    }

    #endregion Servers && Groups

    #region Add Servers

    public async Task AddServerAsync(EConfigType eConfigType)
    {
        ProfileItem item = new()
        {
            Subid = _config.SubIndexId,
            ConfigType = eConfigType,
            IsSub = false,
        };

        bool? ret = false;
        if (eConfigType is EConfigType.Custom or EConfigType.Outbound)
        {
            var addServer2ViewModel = new AddServer2ViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addServer2ViewModel);
        }
        else if (eConfigType.IsGroupType())
        {
            var addGroupServerViewModel = new AddGroupServerViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addGroupServerViewModel);
        }
        else
        {
            var addServerViewModel = new AddServerViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addServerViewModel);
        }
        if (ret == true)
        {
            await RefreshServersDispatcherAsync();
            if (item.IndexId == _config.IndexId)
            {
                await Reload();
            }
        }
    }

    public async Task AddServerViaClipboardAsync(string? clipboardData)
    {
        var stringData = clipboardData;
        if (clipboardData == null)
        {
            var result = await ReadTextFromClipboardInteraction.HandleSafe(RxVoid.Default);
            if (result.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }
            stringData = result;
        }
        var ret = await ConfigHandler.AddBatchServers(_config, stringData, _config.SubIndexId, false);
        if (ret > 0)
        {
            await RefreshSubscriptions();
            await RefreshServersDispatcherAsync();
            NoticeManager.Instance.Enqueue(string.Format(ResUI.SuccessfullyImportedServerViaClipboard, ret));
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    public async Task AddServerViaScanAsync()
    {
        var result = await ScanScreenInteraction.HandleSafe(RxVoid.Default);
        await ScanScreenResult(result);
    }

    public async Task ScanScreenResult(byte[]? bytes)
    {
        var result = QRCodeUtils.ParseBarcode(bytes);
        await AddScanResultAsync(result);
    }

    public async Task AddServerViaImageAsync()
    {
        var imageFileName = await BrowseImageFileInteraction.HandleSafe(RxVoid.Default);
        await ScanImageResult(imageFileName);
    }

    public async Task ScanImageResult(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }

        var result = QRCodeUtils.ParseBarcode(fileName);
        await AddScanResultAsync(result);
    }

    private async Task AddScanResultAsync(string? result)
    {
        if (result.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.NoValidQRcodeFound);
        }
        else
        {
            var ret = await ConfigHandler.AddBatchServers(_config, result, _config.SubIndexId, false);
            if (ret > 0)
            {
                await RefreshSubscriptions();
                await RefreshServersDispatcherAsync();
                NoticeManager.Instance.Enqueue(ResUI.SuccessfullyImportedServerViaScan);
            }
            else
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            }
        }
    }

    #endregion Add Servers

    #region Subscription

    private async Task SubSettingAsync()
    {
        var subSettingViewModel = new SubSettingViewModel();
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subSettingViewModel) == true)
        {
            await RefreshSubscriptions();
        }
    }

    public async Task UpdateSubscriptionProcess(string subId, bool blProxy)
    {
        var result = await Task.Run(async () => await SubscriptionHandler.UpdateProcess(_config, subId, blProxy,
            (success, message) => UpdateTaskHandler(false, message)));
        await UpdateSubscriptionCompleted(result);
    }

    #endregion Subscription

    #region Setting

    private async Task OptionSettingAsync()
    {
        var settingViewModel = new OptionSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(settingViewModel);
        if (ret == true)
        {
            MainGirdOrientation = _config.UiItem.MainGirdOrientation;
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                await StatusBarViewModel.InboundDisplayStatus();
            });
            await Reload();
        }
    }

    private async Task RoutingSettingAsync()
    {
        var routingSettingViewModel = new RoutingSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(routingSettingViewModel);
        if (ret == true)
        {
            await ConfigHandler.InitBuiltinRouting(_config);
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                await StatusBarViewModel.RefreshRoutingsMenu();
            });
            await Reload();
        }
    }

    private async Task DNSSettingAsync()
    {
        var dnsSettingViewModel = new DNSSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(dnsSettingViewModel);
        if (ret == true)
        {
            await Reload();
        }
    }

    private async Task FullConfigTemplateAsync()
    {
        var fullConfigTemplateViewModel = new FullConfigTemplateViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(fullConfigTemplateViewModel);
        if (ret == true)
        {
            await Reload();
        }
    }

    private async Task ClearServerStatistics()
    {
        await StatisticsManager.Instance.ClearAllServerStatistics();
        await RefreshServersDispatcherAsync();
    }

    private async Task OpenTheFileLocation()
    {
        var path = Utils.StartupPath();
        if (Utils.IsWindows())
        {
            ProcUtils.ProcessStart(path);
        }
        else if (Utils.IsLinux())
        {
            ProcUtils.ProcessStart("xdg-open", path);
        }
        else if (Utils.IsMacOS())
        {
            ProcUtils.ProcessStart("open", path);
        }
        await Task.CompletedTask;
    }

    #endregion Setting

    #region core job

    private int _pendingReload; // 0: none, 1: subscription comparison, 2: explicit reload/recovery.
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);

    public Task Reload() => Reload(onlyIfChanged: false);

    private void QueueReload(bool onlyIfChanged)
    {
        if (onlyIfChanged)
            Interlocked.CompareExchange(ref _pendingReload, 1, 0);
        else
            Interlocked.Exchange(ref _pendingReload, 2);
    }

    private async Task Reload(bool onlyIfChanged)
    {
        //If there are unfinished reload job, marked with next job.
        if (!await _reloadSemaphore.WaitAsync(0))
        {
            QueueReload(onlyIfChanged);
            return;
        }

        if (DesignMode)
        {
            _reloadSemaphore.Release();
            return;
        }

        try
        {
            SetReloadEnabled(false);

            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                if (TabMainSelectedIndex < 0)
                {
                    TabMainSelectedIndex = 0;
                }
            });
            var state = PreparedCoreConfigBundle.CaptureConfigState(_config);
            var profileItem = onlyIfChanged || CoreManager.Instance.HasRunningConfiguration
                ? await AppManager.Instance.GetProfileItem(_config.IndexId)
                : await ConfigHandler.GetDefaultServer(_config);
            if (profileItem == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.CheckServerSettings);
                return;
            }
            var snapshot = JsonUtils.DeepCopy(_config)!;
            var allResult = await CoreConfigContextBuilder.BuildAll(snapshot, profileItem);
            if (NoticeManager.Instance.NotifyValidatorResult(allResult.CombinedValidatorResult) && !allResult.Success)
            {
                return;
            }
            // Missing dependencies may be replaced by fallback routes by the builder. Defer these changes.
            if (onlyIfChanged && !allResult.CanApplySubscriptionUpdate)
            {
                await CoreManager.Instance.DeferSubscriptionChanges();
                return;
            }

            var applied = await Task.Run(async () =>
            {
                var loaded = await CoreManager.Instance.LoadCore(allResult.MainResult.Context, allResult.PreSocksResult?.Context,
                    onlyIfChanged, () => state == PreparedCoreConfigBundle.CaptureConfigState(_config));
                if (!loaded)
                    return false;
                await SysProxyHandler.UpdateSysProxy(_config, false);
                await Task.Delay(1000);
                return true;
            });
            if (!applied)
            {
                if (state != PreparedCoreConfigBundle.CaptureConfigState(_config))
                    QueueReload(onlyIfChanged);
                return;
            }
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                var result = await StatusBarViewModel.TestServerAvailability();
                if (result == null || profileItem.IndexId.IsNullOrEmpty())
                {
                    return;
                }

                await ProfilesViewModel.SetSpeedTestResult(new()
                {
                    IndexId = profileItem.IndexId,
                    IpInfo = result.Ip,
                    Delay = result.Time > 0 ? result.Time.ToString() : null
                });
            });

            var showClashUI = AppManager.Instance.IsRunningCore(ECoreType.sing_box);
            if (showClashUI)
            {
                //await Observable.Start(async () =>
                //{
                //    await ClashProxiesViewModel.ProxiesReload();
                //}, RxSchedulers.MainThreadScheduler);
                await Signal.FromAsync(async () =>
                    {
                        await ClashProxiesViewModel.ProxiesReload();
                        return RxVoid.Default;
                    }).SubscribeOn(RxSchedulers.MainThreadScheduler)
                    .ToTask();
            }

            ReloadResult(showClashUI);
        }
        finally
        {
            SetReloadEnabled(true);
            _reloadSemaphore.Release();
            //If there is a next reload job, execute it.
            var pending = Interlocked.Exchange(ref _pendingReload, 0);
            if (pending != 0)
            {
                await Reload(onlyIfChanged: pending == 1);
            }
        }
    }

    private void ReloadResult(bool showClashUI)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            ShowClashUI = showClashUI;
            if (!showClashUI || TabMainSelectedIndex < 0)
            {
                TabMainSelectedIndex = 0;
            }
        });
    }

    private void SetReloadEnabled(bool enabled)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() => BlReloadEnabled = enabled);
    }

    #endregion core job

    #region Presets

    public async Task ApplyRegionalPreset(EPresetType type)
    {
        await ConfigHandler.ApplyRegionalPreset(_config, type);
        await ConfigHandler.InitRouting(_config);
        RxSchedulers.MainThreadScheduler.Schedule(async () =>
        {
            await StatusBarViewModel.RefreshRoutingsMenu();
        });

        await ConfigHandler.SaveConfig(_config);
        await new UpdateService(_config, UpdateTaskHandler).UpdateGeoFileAll();
        await Reload();
    }

    #endregion Presets
}
