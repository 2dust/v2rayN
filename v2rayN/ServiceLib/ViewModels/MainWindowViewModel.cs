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

            Init();

            _config.uiItem.showInTaskbar = true;

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
                await AddServerViaScanTaskAsync();
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
                await UpdateSubscriptionProcess(_config.subIndexId, false);
            });
            SubGroupUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await UpdateSubscriptionProcess(_config.subIndexId, true);
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

            #endregion WhenAnyValue && ReactiveCommand

            AutoHideStartup();
        }

        private void Init()
        {
            ConfigHandler.InitBuiltinRouting(_config);
            ConfigHandler.InitBuiltinDNS(_config);
            CoreHandler.Instance.Init(_config, UpdateHandler);
            TaskHandler.Instance.RegUpdateTask(_config, UpdateTaskHandler);

            if (_config.guiItem.enableStatistics)
            {
                StatisticsHandler.Instance.Init(_config, UpdateStatisticsHandler);
            }

            Reload();
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
                var indexIdOld = _config.indexId;
                RefreshServers();
                if (indexIdOld != _config.indexId)
                {
                    Reload();
                }
                if (_config.uiItem.enableAutoAdjustMainLvColWidth)
                {
                    _updateView?.Invoke(EViewAction.AdjustMainLvColWidth, null);
                }
            }
        }

        private void UpdateStatisticsHandler(ServerSpeedItem update)
        {
            if (!_config.uiItem.showInTaskbar)
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
                if ((update.proxyUp + update.proxyDown) > 0 && DateTime.Now.Second % 3 == 0)
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
                Logging.SaveLog("MyAppExit Begin");
                //if (blWindowsShutDown)
                await _updateView?.Invoke(EViewAction.UpdateSysProxy, true);

                ConfigHandler.SaveConfig(_config);
                ProfileExHandler.Instance.SaveTo();
                StatisticsHandler.Instance.SaveTo();
                StatisticsHandler.Instance.Close();
                CoreHandler.Instance.CoreStop();

                Logging.SaveLog("MyAppExit End");
            }
            catch { }
            finally
            {
                _updateView?.Invoke(EViewAction.Shutdown, null);
            }
        }

        public async Task UpgradeApp(string fileName)
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "AmazTool",
                    Arguments = fileName.AppendQuotes(),
                    WorkingDirectory = Utils.StartupPath()
                }
            };
            process.Start();
            if (process.Id > 0)
            {
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
                subid = _config.subIndexId,
                configType = eConfigType,
                isSub = false,
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
                if (item.indexId == _config.indexId)
                {
                    Reload();
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
            int ret = ConfigHandler.AddBatchServers(_config, clipboardData, _config.subIndexId, false);
            if (ret > 0)
            {
                RefreshSubscriptions();
                RefreshServers();
                NoticeHandler.Instance.Enqueue(string.Format(ResUI.SuccessfullyImportedServerViaClipboard, ret));
            }
        }

        public async Task AddServerViaScanTaskAsync()
        {
            _updateView?.Invoke(EViewAction.ScanScreenTask, null);
        }

        public void ScanScreenResult(string result)
        {
            if (Utils.IsNullOrEmpty(result))
            {
                NoticeHandler.Instance.Enqueue(ResUI.NoValidQRcodeFound);
            }
            else
            {
                int ret = ConfigHandler.AddBatchServers(_config, result, _config.subIndexId, false);
                if (ret > 0)
                {
                    RefreshSubscriptions();
                    RefreshServers();
                    NoticeHandler.Instance.Enqueue(ResUI.SuccessfullyImportedServerViaScan);
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
            (new UpdateService()).UpdateSubscriptionProcess(_config, subId, blProxy, UpdateTaskHandler);
        }

        #endregion Subscription

        #region Setting

        private async Task OptionSettingAsync()
        {
            var ret = await _updateView?.Invoke(EViewAction.OptionSettingWindow, null);
            if (ret == true)
            {
                Locator.Current.GetService<StatusBarViewModel>()?.InboundDisplayStatus();
                Reload();
            }
        }

        private async Task RoutingSettingAsync()
        {
            var ret = await _updateView?.Invoke(EViewAction.RoutingSettingWindow, null);
            if (ret == true)
            {
                ConfigHandler.InitBuiltinRouting(_config);
                Locator.Current.GetService<StatusBarViewModel>()?.RefreshRoutingsMenu();
                Reload();
            }
        }

        private async Task DNSSettingAsync()
        {
            var ret = await _updateView?.Invoke(EViewAction.DNSSettingWindow, null);
            if (ret == true)
            {
                Reload();
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
            StatisticsHandler.Instance.ClearAllServerStatistics();
            RefreshServers();
        }

        private async Task OpenTheFileLocation()
        {
            if (Utils.IsWindows())
            {
                Utils.ProcessStart("Explorer", $"/select,{Utils.GetConfigPath()}");
            }
            else if (Utils.IsLinux())
            {
                Utils.ProcessStart("nautilus", Utils.GetConfigPath());
            }
        }

        #endregion Setting

        #region core job

        public async Task Reload()
        {
            BlReloadEnabled = false;

            await LoadCore();
            Locator.Current.GetService<StatusBarViewModel>()?.TestServerAvailability();
            _updateView?.Invoke(EViewAction.DispatcherReload, null);
        }

        public void ReloadResult()
        {
            //ChangeSystemProxyStatusAsync(_config.systemProxyItem.sysProxyType, false);
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
            await Task.Run(() =>
            {
                //if (_config.tunModeItem.enableTun)
                //{
                //    Task.Delay(1000).Wait();
                //    WindowsUtils.RemoveTunDevice();
                //}

                var node = ConfigHandler.GetDefaultServer(_config);
                CoreHandler.Instance.LoadCore(node);
            });
        }

        public void CloseCore()
        {
            ConfigHandler.SaveConfig(_config, false);
            CoreHandler.Instance.CoreStop();
        }

        private void AutoHideStartup()
        {
            if (_config.uiItem.autoHideStartup)
            {
                Observable.Range(1, 1)
                    .Delay(TimeSpan.FromSeconds(1))
                    .Subscribe(async x =>
                    {
                        ShowHideWindow(false);
                    });
            }
        }

        #endregion core job
    }
}