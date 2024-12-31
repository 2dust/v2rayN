using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;

namespace ServiceLib.ViewModels
{
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

            Init();
        }

        private async Task Init()
        {
            _config.UiItem.ShowInTaskbar = true;

            await ConfigHandler.InitBuiltinRouting(_config);
            await ConfigHandler.InitBuiltinDNS(_config);
            await ProfileExHandler.Instance.Init();
            await CoreHandler.Instance.Init(_config, UpdateHandler);
            TaskHandler.Instance.RegUpdateTask(_config, UpdateTaskHandler);

            if (_config.GuiItem.EnableStatistics)
            {
                await StatisticsHandler.Instance.Init(_config, UpdateStatisticsHandler);
            }

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
                    Reload();
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
            try
            {
                Locator.Current.GetService<StatusBarViewModel>()?.UpdateStatistics(update);
                if ((update.ProxyUp + update.ProxyDown) > 0 && DateTime.Now.Second % 9 == 0)
                {
                    Locator.Current.GetService<ProfilesViewModel>()?.UpdateStatistics(update);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public async Task MyAppExitAsync(bool blWindowsShutDown)
        {
            try
            {
                Logging.SaveLog("MyAppExitAsync Begin");
                MessageBus.Current.SendMessage("", EMsgCommand.AppExit.ToString());

                await ConfigHandler.SaveConfig(_config);
                await SysProxyHandler.UpdateSysProxy(_config, true);
                await ProfileExHandler.Instance.SaveTo();
                await StatisticsHandler.Instance.SaveTo();
                StatisticsHandler.Instance.Close();
                await CoreHandler.Instance.CoreStop();

                Logging.SaveLog("MyAppExitAsync End");
            }
            catch { }
            finally
            {
                if (!blWindowsShutDown)
                {
                    _updateView?.Invoke(EViewAction.Shutdown, null);
                }
            }
        }

        public async Task UpgradeApp(string arg)
        {
            if (!Utils.UpgradeAppExists(out var fileName))
            {
                NoticeHandler.Instance.SendMessageAndEnqueue(ResUI.UpgradeAppNotExistTip);
                Logging.SaveLog("UpgradeApp does not exist");
                return;
            }

            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = fileName,
                    Arguments = arg.AppendQuotes(),
                    WorkingDirectory = Utils.StartupPath()
                }
            };
            process.Start();
            if (process.Id > 0)
            {
                await MyAppExitAsync(false);
                await MyAppExitAsync(false);
            }
        }

        public void ShowHideWindow(bool? blShow)
        {
            _updateView?.Invoke(EViewAction.ShowHideWindow, blShow);
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
        }

        public async Task ScanScreenResult(byte[]? bytes)
        {
            var result = QRCodeHelper.ParseBarcode(bytes);
            await AddScanResultAsync(result);
        }

        public async Task AddServerViaImageAsync()
        {
            _updateView?.Invoke(EViewAction.ScanImageTask, null);
        }

        public async Task ScanImageResult(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var result = QRCodeHelper.ParseBarcode(fileName);
            await AddScanResultAsync(result);
        }

        private async Task AddScanResultAsync(string? result)
        {
            if (Utils.IsNullOrEmpty(result))
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
            await (new UpdateService()).UpdateSubscriptionProcess(_config, subId, blProxy, UpdateTaskHandler);
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

        public async Task RebootAsAdmin()
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    UseShellExecute = true,
                    Arguments = Global.RebootAs,
                    WorkingDirectory = Utils.StartupPath(),
                    FileName = Utils.GetExePath().AppendQuotes(),
                    Verb = "runas",
                };
                Process.Start(startInfo);
                await MyAppExitAsync(false);
            }
            catch { }
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
                Utils.ProcessStart(path);
            }
            else if (Utils.IsLinux())
            {
                Utils.ProcessStart("nautilus", path);
            }
            else if (Utils.IsOSX())
            {
                Utils.ProcessStart("open", path);
            }
        }

        #endregion Setting

        #region core job

        public async Task Reload()
        {
            BlReloadEnabled = false;

            await LoadCore();
            Locator.Current.GetService<StatusBarViewModel>()?.TestServerAvailability();
            await SysProxyHandler.UpdateSysProxy(_config, false);

            _updateView?.Invoke(EViewAction.DispatcherReload, null);
        }

        public void ReloadResult()
        {
            //Locator.Current.GetService<StatusBarViewModel>()?.ChangeSystemProxyAsync(_config.systemProxyItem.sysProxyType, false);
            BlReloadEnabled = true;
            ShowClashUI = _config.IsRunningCore(ECoreType.sing_box);
            if (ShowClashUI)
            {
                Locator.Current.GetService<ClashProxiesViewModel>()?.ProxiesReload();
            }
            else { TabMainSelectedIndex = 0; }
        }

        private async Task LoadCore()
        {
            //if (_config.tunModeItem.enableTun)
            //{
            //    Task.Delay(1000).Wait();
            //    WindowsUtils.RemoveTunDevice();
            //}
            await Task.Run(async () =>
            {
                var node = await ConfigHandler.GetDefaultServer(_config);
                await CoreHandler.Instance.LoadCore(node);
            });
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
}