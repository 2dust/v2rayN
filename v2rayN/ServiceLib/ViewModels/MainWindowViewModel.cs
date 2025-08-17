using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace ServiceLib.ViewModels;

public class MainWindowViewModel : MyReactiveObject
{
    #region Menu

    //servers
    public ReactiveCommand<Unit, Unit> AddVmessServerCmd { get; }

    public ReactiveCommand<Unit, Unit> AddVlessServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddShadowsocksServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddSocksServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddHttpServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddTrojanServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddHysteria2ServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddTuicServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddWireguardServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddAnytlsServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddCustomServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> AddServerViaScanCmd { get; }
    public ReactiveCommand<Unit, Unit> AddServerViaImageCmd { get; }

    //Subscription
    public ReactiveCommand<Unit, Unit> SubSettingCmd { get; }

    public ReactiveCommand<Unit, Unit> SubUpdateCmd { get; }
    public ReactiveCommand<Unit, Unit> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<Unit, Unit> SubGroupUpdateCmd { get; }
    public ReactiveCommand<Unit, Unit> SubGroupUpdateViaProxyCmd { get; }

    //Setting
    public ReactiveCommand<Unit, Unit> OptionSettingCmd { get; }

    public ReactiveCommand<Unit, Unit> RoutingSettingCmd { get; }
    public ReactiveCommand<Unit, Unit> DNSSettingCmd { get; }
    public ReactiveCommand<Unit, Unit> FullConfigTemplateCmd { get; }
    public ReactiveCommand<Unit, Unit> GlobalHotkeySettingCmd { get; }
    public ReactiveCommand<Unit, Unit> RebootAsAdminCmd { get; }
    public ReactiveCommand<Unit, Unit> ClearServerStatisticsCmd { get; }
    public ReactiveCommand<Unit, Unit> OpenTheFileLocationCmd { get; }

    //Presets
    public ReactiveCommand<Unit, Unit> RegionalPresetDefaultCmd { get; }

    public ReactiveCommand<Unit, Unit> RegionalPresetRussiaCmd { get; }

    public ReactiveCommand<Unit, Unit> RegionalPresetIranCmd { get; }

    public ReactiveCommand<Unit, Unit> ReloadCmd { get; }

    [Reactive]
    public bool BlReloadEnabled { get; set; }

    [Reactive]
    public bool ShowClashUI { get; set; }

    [Reactive]
    public int TabMainSelectedIndex { get; set; }

    #endregion Menu

    private bool _hasNextReloadJob = false;

    #region Init

    public MainWindowViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;

        #region WhenAnyValue && ReactiveCommand

        //servers
        AddVmessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.VMess);
        });
        AddVlessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.VLESS);
        });
        AddShadowsocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.Shadowsocks);
        });
        AddSocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.SOCKS);
        });
        AddHttpServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.HTTP);
        });
        AddTrojanServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.Trojan);
        });
        AddHysteria2ServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.Hysteria2);
        });
        AddTuicServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.TUIC);
        });
        AddWireguardServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.WireGuard);
        });
        AddAnytlsServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.Anytls);
        });
        AddCustomServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(true, EConfigType.Custom);
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
            if (await _updateView?.Invoke(EViewAction.GlobalHotkeySettingWindow, null) == true)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            }
        });
        RebootAsAdminCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RebootAsAdmin();
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

        _ = Init();
    }

    private async Task Init()
    {
        _config.UiItem.ShowInTaskbar = true;

        await ConfigHandler.InitBuiltinRouting(_config);
        await ConfigHandler.InitBuiltinDNS(_config);
        await ConfigHandler.InitBuiltinFullConfigTemplate(_config);
        await ProfileExHandler.Instance.Init();
        await CoreHandler.Instance.Init(_config, UpdateHandler);
        TaskHandler.Instance.RegUpdateTask(_config, UpdateTaskHandler);

        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            await StatisticsHandler.Instance.Init(_config, UpdateStatisticsHandler);
        }

        BlReloadEnabled = true;
        await Reload();
        await AutoHideStartup();
        Locator.Current.GetService<StatusBarViewModel>()?.RefreshRoutingsMenu();
    }

    #endregion Init

    #region Actions

    private void UpdateHandler(bool notify, string msg)
    {
        NoticeHandler.Instance.SendMessage(msg);
        if (notify)
        {
            NoticeHandler.Instance.Enqueue(msg);
        }
    }

    private void UpdateTaskHandler(bool success, string msg)
    {
        NoticeHandler.Instance.SendMessageEx(msg);
        if (success)
        {
            var indexIdOld = _config.IndexId;
            RefreshServers();
            if (indexIdOld != _config.IndexId)
            {
                _ = Reload();
            }
            if (_config.UiItem.EnableAutoAdjustMainLvColWidth)
            {
                _updateView?.Invoke(EViewAction.AdjustMainLvColWidth, null);
            }
        }
    }

    private void UpdateStatisticsHandler(ServerSpeedItem update)
    {
        if (!_config.UiItem.ShowInTaskbar)
        {
            return;
        }
        _updateView?.Invoke(EViewAction.DispatcherStatistics, update);
    }

    public void SetStatisticsResult(ServerSpeedItem update)
    {
        if (_config.GuiItem.DisplayRealTimeSpeed)
        {
            Locator.Current.GetService<StatusBarViewModel>()?.UpdateStatistics(update);
        }
        if (_config.GuiItem.EnableStatistics && (update.ProxyUp + update.ProxyDown) > 0 && DateTime.Now.Second % 9 == 0)
        {
            Locator.Current.GetService<ProfilesViewModel>()?.UpdateStatistics(update);
        }
    }

    public async Task MyAppExitAsync(bool blWindowsShutDown)
    {
        try
        {
            Logging.SaveLog("MyAppExitAsync Begin");

            await SysProxyHandler.UpdateSysProxy(_config, true);
            MessageBus.Current.SendMessage("", EMsgCommand.AppExit.ToString());

            await ConfigHandler.SaveConfig(_config);
            await ProfileExHandler.Instance.SaveTo();
            await StatisticsHandler.Instance.SaveTo();
            await CoreHandler.Instance.CoreStop();
            StatisticsHandler.Instance.Close();

            Logging.SaveLog("MyAppExitAsync End");
        }
        catch { }
        finally
        {
            if (!blWindowsShutDown)
            {
                _updateView?.Invoke(EViewAction.Shutdown, false);
            }
        }
    }

    public async Task UpgradeApp(string arg)
    {
        if (!Utils.UpgradeAppExists(out var upgradeFileName))
        {
            NoticeHandler.Instance.SendMessageAndEnqueue(ResUI.UpgradeAppNotExistTip);
            Logging.SaveLog("UpgradeApp does not exist");
            return;
        }

        var id = ProcUtils.ProcessStart(upgradeFileName, arg, Utils.StartupPath());
        if (id > 0)
        {
            await MyAppExitAsync(false);
        }
    }

    public void ShowHideWindow(bool? blShow)
    {
        _updateView?.Invoke(EViewAction.ShowHideWindow, blShow);
    }

    public void Shutdown(bool byUser)
    {
        _updateView?.Invoke(EViewAction.Shutdown, byUser);
    }

    #endregion Actions

    #region Servers && Groups

    private void RefreshServers()
    {
        MessageBus.Current.SendMessage("", EMsgCommand.RefreshProfiles.ToString());
    }

    private void RefreshSubscriptions()
    {
        Locator.Current.GetService<ProfilesViewModel>()?.RefreshSubscriptions();
    }

    #endregion Servers && Groups

    #region Add Servers

    public async Task AddServerAsync(bool blNew, EConfigType eConfigType)
    {
        ProfileItem item = new()
        {
            Subid = _config.SubIndexId,
            ConfigType = eConfigType,
            IsSub = false,
        };

        bool? ret = false;
        if (eConfigType == EConfigType.Custom)
        {
            ret = await _updateView?.Invoke(EViewAction.AddServer2Window, item);
        }
        else
        {
            ret = await _updateView?.Invoke(EViewAction.AddServerWindow, item);
        }
        if (ret == true)
        {
            RefreshServers();
            if (item.IndexId == _config.IndexId)
            {
                await Reload();
            }
        }
    }

    public async Task AddServerViaClipboardAsync(string? clipboardData)
    {
        if (clipboardData == null)
        {
            await _updateView?.Invoke(EViewAction.AddServerViaClipboard, null);
            return;
        }
        int ret = await ConfigHandler.AddBatchServers(_config, clipboardData, _config.SubIndexId, false);
        if (ret > 0)
        {
            RefreshSubscriptions();
            RefreshServers();
            NoticeHandler.Instance.Enqueue(string.Format(ResUI.SuccessfullyImportedServerViaClipboard, ret));
        }
        else
        {
            NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    public async Task AddServerViaScanAsync()
    {
        _updateView?.Invoke(EViewAction.ScanScreenTask, null);
        await Task.CompletedTask;
    }

    public async Task ScanScreenResult(byte[]? bytes)
    {
        var result = QRCodeUtils.ParseBarcode(bytes);
        await AddScanResultAsync(result);
    }

    public async Task AddServerViaImageAsync()
    {
        _updateView?.Invoke(EViewAction.ScanImageTask, null);
        await Task.CompletedTask;
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
            NoticeHandler.Instance.Enqueue(ResUI.NoValidQRcodeFound);
        }
        else
        {
            int ret = await ConfigHandler.AddBatchServers(_config, result, _config.SubIndexId, false);
            if (ret > 0)
            {
                RefreshSubscriptions();
                RefreshServers();
                NoticeHandler.Instance.Enqueue(ResUI.SuccessfullyImportedServerViaScan);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }
    }

    #endregion Add Servers

    #region Subscription

    private async Task SubSettingAsync()
    {
        if (await _updateView?.Invoke(EViewAction.SubSettingWindow, null) == true)
        {
            RefreshSubscriptions();
        }
    }

    public async Task UpdateSubscriptionProcess(string subId, bool blProxy)
    {
        await SubscriptionHandler.UpdateProcess(_config, subId, blProxy, UpdateTaskHandler);
    }

    #endregion Subscription

    #region Setting

    private async Task OptionSettingAsync()
    {
        var ret = await _updateView?.Invoke(EViewAction.OptionSettingWindow, null);
        if (ret == true)
        {
            Locator.Current.GetService<StatusBarViewModel>()?.InboundDisplayStatus();
            await Reload();
        }
    }

    private async Task RoutingSettingAsync()
    {
        var ret = await _updateView?.Invoke(EViewAction.RoutingSettingWindow, null);
        if (ret == true)
        {
            await ConfigHandler.InitBuiltinRouting(_config);
            Locator.Current.GetService<StatusBarViewModel>()?.RefreshRoutingsMenu();
            await Reload();
        }
    }

    private async Task DNSSettingAsync()
    {
        var ret = await _updateView?.Invoke(EViewAction.DNSSettingWindow, null);
        if (ret == true)
        {
            await Reload();
        }
    }

    private async Task FullConfigTemplateAsync()
    {
        var ret = await _updateView?.Invoke(EViewAction.FullConfigTemplateWindow, null);
        if (ret == true)
        {
            await Reload();
        }
    }

    public async Task RebootAsAdmin()
    {
        ProcUtils.RebootAsAdmin();
        await MyAppExitAsync(false);
    }

    private async Task ClearServerStatistics()
    {
        await StatisticsHandler.Instance.ClearAllServerStatistics();
        RefreshServers();
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
            ProcUtils.ProcessStart("nautilus", path);
        }
        else if (Utils.IsOSX())
        {
            ProcUtils.ProcessStart("open", path);
        }
        await Task.CompletedTask;
    }

    #endregion Setting

    #region core job

    public async Task Reload()
    {
        //If there are unfinished reload job, marked with next job.
        if (!BlReloadEnabled)
        {
            _hasNextReloadJob = true;
            return;
        }

        BlReloadEnabled = false;

        await Task.Run(async () =>
        {
            await LoadCore();
            await SysProxyHandler.UpdateSysProxy(_config, false);
            await Task.Delay(1000);
        });
        Locator.Current.GetService<StatusBarViewModel>()?.TestServerAvailability();

        _updateView?.Invoke(EViewAction.DispatcherReload, null);

        BlReloadEnabled = true;
        if (_hasNextReloadJob)
        {
            _hasNextReloadJob = false;
            await Reload();
        }
    }

    public void ReloadResult()
    {
        // BlReloadEnabled = true;
        //Locator.Current.GetService<StatusBarViewModel>()?.ChangeSystemProxyAsync(_config.systemProxyItem.sysProxyType, false);
        ShowClashUI = _config.IsRunningCore(ECoreType.sing_box);
        if (ShowClashUI)
        {
            Locator.Current.GetService<ClashProxiesViewModel>()?.ProxiesReload();
        }
        else
        { TabMainSelectedIndex = 0; }
    }

    private async Task LoadCore()
    {
        var node = await ConfigHandler.GetDefaultServer(_config);
        await CoreHandler.Instance.LoadCore(node);
    }

    public async Task CloseCore()
    {
        await ConfigHandler.SaveConfig(_config);
        await CoreHandler.Instance.CoreStop();
    }

    private async Task AutoHideStartup()
    {
        if (_config.UiItem.AutoHideStartup)
        {
            ShowHideWindow(false);
        }
        await Task.CompletedTask;
    }

    #endregion core job

    #region Presets

    public async Task ApplyRegionalPreset(EPresetType type)
    {
        await ConfigHandler.ApplyRegionalPreset(_config, type);
        await ConfigHandler.InitRouting(_config);
        Locator.Current.GetService<StatusBarViewModel>()?.RefreshRoutingsMenu();

        await ConfigHandler.SaveConfig(_config);
        await new UpdateService().UpdateGeoFileAll(_config, UpdateHandler);
        await Reload();
    }

    #endregion Presets
}
