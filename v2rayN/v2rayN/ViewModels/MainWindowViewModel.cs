using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using v2rayN.Base;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Handler.Statistics;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class MainWindowViewModel : MyReactiveObject
    {
        #region private prop

        private CoreHandler _coreHandler;

        #endregion private prop

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

        //CheckUpdate
        public ReactiveCommand<Unit, Unit> CheckUpdateNCmd { get; }

        public ReactiveCommand<Unit, Unit> CheckUpdateXrayCoreCmd { get; }
        public ReactiveCommand<Unit, Unit> CheckUpdateClashMetaCoreCmd { get; }
        public ReactiveCommand<Unit, Unit> CheckUpdateSingBoxCoreCmd { get; }
        public ReactiveCommand<Unit, Unit> CheckUpdateGeoCmd { get; }
        public ReactiveCommand<Unit, Unit> ReloadCmd { get; }

        [Reactive]
        public bool BlReloadEnabled { get; set; }

        public ReactiveCommand<Unit, Unit> NotifyLeftClickCmd { get; }

        #endregion Menu

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
        public bool ShowClashUI { get; set; }

        [Reactive]
        public int TabMainSelectedIndex { get; set; }

        #endregion UI

        #region Init

        public MainWindowViewModel(Func<EViewAction, object?, bool>? updateView)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _updateView = updateView;

            ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);
            MessageBus.Current.Listen<string>(Global.CommandRefreshProfiles).Subscribe(x => _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null));

            SelectedRouting = new();
            SelectedServer = new();
            if (_config.tunModeItem.enableTun)
            {
                if (WindowsUtils.IsAdministrator())
                {
                    EnableTun = true;
                }
                else
                {
                    _config.tunModeItem.enableTun = EnableTun = false;
                }
            }

            Init();

            #region WhenAnyValue && ReactiveCommand

            this.WhenAnyValue(
                x => x.SelectedRouting,
                y => y != null && !y.remarks.IsNullOrEmpty())
                    .Subscribe(c => RoutingSelectedChanged(c));

            this.WhenAnyValue(
              x => x.SelectedServer,
              y => y != null && !y.Text.IsNullOrEmpty())
                  .Subscribe(c => ServerSelectedChanged(c));

            SystemProxySelected = (int)_config.systemProxyItem.sysProxyType;
            this.WhenAnyValue(
              x => x.SystemProxySelected,
              y => y >= 0)
                  .Subscribe(c => DoSystemProxySelected(c));

            this.WhenAnyValue(
              x => x.EnableTun,
               y => y == true)
                  .Subscribe(c => DoEnableTun(c));

            //servers
            AddVmessServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.VMess);
            });
            AddVlessServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.VLESS);
            });
            AddShadowsocksServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Shadowsocks);
            });
            AddSocksServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Socks);
            });
            AddHttpServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Http);
            });
            AddTrojanServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Trojan);
            });
            AddHysteria2ServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Hysteria2);
            });
            AddTuicServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Tuic);
            });
            AddWireguardServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Wireguard);
            });
            AddCustomServerCmd = ReactiveCommand.Create(() =>
            {
                AddServer(true, EConfigType.Custom);
            });
            AddServerViaClipboardCmd = ReactiveCommand.Create(() =>
            {
                AddServerViaClipboard();
            });
            AddServerViaScanCmd = ReactiveCommand.Create(() =>
            {
                _updateView?.Invoke(EViewAction.ScanScreenTask, null);
            });

            //Subscription
            SubSettingCmd = ReactiveCommand.Create(() =>
            {
                SubSetting();
            });

            SubUpdateCmd = ReactiveCommand.Create(() =>
            {
                UpdateSubscriptionProcess("", false);
            });
            SubUpdateViaProxyCmd = ReactiveCommand.Create(() =>
            {
                UpdateSubscriptionProcess("", true);
            });
            SubGroupUpdateCmd = ReactiveCommand.Create(() =>
            {
                UpdateSubscriptionProcess(_config.subIndexId, false);
            });
            SubGroupUpdateViaProxyCmd = ReactiveCommand.Create(() =>
            {
                UpdateSubscriptionProcess(_config.subIndexId, true);
            });

            //Setting
            OptionSettingCmd = ReactiveCommand.Create(() =>
            {
                OptionSetting();
            });
            RoutingSettingCmd = ReactiveCommand.Create(() =>
            {
                RoutingSetting();
            });
            DNSSettingCmd = ReactiveCommand.Create(() =>
            {
                DNSSetting();
            });
            GlobalHotkeySettingCmd = ReactiveCommand.Create(() =>
            {
                if (_updateView?.Invoke(EViewAction.GlobalHotkeySettingWindow, null) == true)
                {
                    _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                }
            });
            RebootAsAdminCmd = ReactiveCommand.Create(() =>
            {
                RebootAsAdmin();
            });
            ClearServerStatisticsCmd = ReactiveCommand.Create(() =>
            {
                StatisticsHandler.Instance.ClearAllServerStatistics();
                RefreshServers();
            });
            OpenTheFileLocationCmd = ReactiveCommand.Create(() =>
            {
                Utils.ProcessStart("Explorer", $"/select,{Utils.GetConfigPath()}");
            });

            //CheckUpdate
            CheckUpdateNCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdateN();
            });
            CheckUpdateXrayCoreCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdateCore(ECoreType.Xray, null);
            });
            CheckUpdateClashMetaCoreCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdateCore(ECoreType.mihomo, false);
            });
            CheckUpdateSingBoxCoreCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdateCore(ECoreType.sing_box, null);
            });
            CheckUpdateGeoCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdateGeo();
            });

            ReloadCmd = ReactiveCommand.Create(() =>
            {
                Reload();
            });

            NotifyLeftClickCmd = ReactiveCommand.Create(() =>
            {
                _updateView?.Invoke(EViewAction.ShowHideWindow, null);
            });

            //System proxy
            SystemProxyClearCmd = ReactiveCommand.Create(() =>
            {
                SetListenerType(ESysProxyType.ForcedClear);
            });
            SystemProxySetCmd = ReactiveCommand.Create(() =>
            {
                SetListenerType(ESysProxyType.ForcedChange);
            });
            SystemProxyNothingCmd = ReactiveCommand.Create(() =>
            {
                SetListenerType(ESysProxyType.Unchanged);
            });
            SystemProxyPacCmd = ReactiveCommand.Create(() =>
            {
                SetListenerType(ESysProxyType.Pac);
            });

            #endregion WhenAnyValue && ReactiveCommand

            AutoHideStartup();

            _config.uiItem.showInTaskbar = true;
        }

        private void Init()
        {
            ConfigHandler.InitBuiltinRouting(_config);
            ConfigHandler.InitBuiltinDNS(_config);
            _coreHandler = new CoreHandler(_config, UpdateHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => _coreHandler, typeof(CoreHandler));

            if (_config.guiItem.enableStatistics)
            {
                StatisticsHandler.Instance.Init(_config, UpdateStatisticsHandler);
            }

            RegUpdateTask(_config, UpdateTaskHandler);
            RefreshRoutingsMenu();
            //RefreshServers();

            Reload();
            ChangeSystemProxyStatus(_config.systemProxyItem.sysProxyType, true);
        }

        private void OnProgramStarted(object state, bool timeout)
        {
            _updateView?.Invoke(EViewAction.ShowHideWindow, true);
        }

        #endregion Init

        #region Actions

        private void UpdateHandler(bool notify, string msg)
        {
            if (!_config.uiItem.showInTaskbar)
            {
                return;
            }
            _noticeHandler?.SendMessage(msg);
            if (notify)
            {
                _noticeHandler?.Enqueue(msg);
            }
        }

        private void UpdateTaskHandler(bool success, string msg)
        {
            _noticeHandler?.SendMessage(msg);
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
                    Locator.Current.GetService<ProfilesViewModel>()?.AutofitColumnWidth();
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
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.proxyUp), Utils.HumanFy(update.proxyDown));
                SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.directUp), Utils.HumanFy(update.directDown));

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

        public void MyAppExit(bool blWindowsShutDown)
        {
            try
            {
                Logging.SaveLog("MyAppExit Begin");

                ConfigHandler.SaveConfig(_config);

                if (blWindowsShutDown)
                {
                    SysProxyHandle.ResetIEProxy4WindowsShutDown();
                }
                else
                {
                    SysProxyHandle.UpdateSysProxy(_config, true);
                }

                ProfileExHandler.Instance.SaveTo();

                StatisticsHandler.Instance.SaveTo();
                StatisticsHandler.Instance.Close();

                _coreHandler.CoreStop();
                Logging.SaveLog("MyAppExit End");
            }
            catch { }
            finally
            {
                _updateView?.Invoke(EViewAction.Shutdown, null);
            }
        }

        #endregion Actions

        #region Servers && Groups

        private void RefreshServers()
        {
            MessageBus.Current.SendMessage("", Global.CommandRefreshProfiles);
        }

        public void RefreshServersBiz()
        {
            RefreshServersMenu();

            //display running server
            var running = ConfigHandler.GetDefaultServer(_config);
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

        private void RefreshServersMenu()
        {
            var lstModel = LazyConfig.Instance.ProfileItems(_config.subIndexId, "");

            _servers.Clear();
            if (lstModel.Count > _config.guiItem.trayMenuServersLimit)
            {
                BlServers = false;
                return;
            }

            BlServers = true;
            for (int k = 0; k < lstModel.Count; k++)
            {
                ProfileItem it = lstModel[k];
                string name = it.GetSummary();

                var item = new ComboItem() { ID = it.indexId, Text = name };
                _servers.Add(item);
                if (_config.indexId == it.indexId)
                {
                    SelectedServer = item;
                }
            }
        }

        private void RefreshSubscriptions()
        {
            Locator.Current.GetService<ProfilesViewModel>()?.RefreshSubscriptions();
        }

        #endregion Servers && Groups

        #region Add Servers

        public void AddServer(bool blNew, EConfigType eConfigType)
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
                ret = _updateView?.Invoke(EViewAction.AddServer2Window, item);
            }
            else
            {
                ret = _updateView?.Invoke(EViewAction.AddServerWindow, item);
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

        public void AddServerViaClipboard()
        {
            var clipboardData = WindowsUtils.GetClipboardData();
            int ret = ConfigHandler.AddBatchServers(_config, clipboardData!, _config.subIndexId, false);
            if (ret > 0)
            {
                RefreshSubscriptions();
                RefreshServers();
                _noticeHandler?.Enqueue(string.Format(ResUI.SuccessfullyImportedServerViaClipboard, ret));
            }
        }

        public void ScanScreenTaskAsync(string result)
        {
            if (Utils.IsNullOrEmpty(result))
            {
                _noticeHandler?.Enqueue(ResUI.NoValidQRcodeFound);
            }
            else
            {
                int ret = ConfigHandler.AddBatchServers(_config, result, _config.subIndexId, false);
                if (ret > 0)
                {
                    RefreshSubscriptions();
                    RefreshServers();
                    _noticeHandler?.Enqueue(ResUI.SuccessfullyImportedServerViaScan);
                }
            }
        }

        private void SetDefaultServer(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return;
            }
            if (indexId == _config.indexId)
            {
                return;
            }
            var item = LazyConfig.Instance.GetProfileItem(indexId);
            if (item is null)
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                return;
            }

            if (ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
            {
                RefreshServers();
                Reload();
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
            if (Utils.IsNullOrEmpty(SelectedServer.ID))
            {
                return;
            }
            SetDefaultServer(SelectedServer.ID);
        }

        public void TestServerAvailability()
        {
            var item = ConfigHandler.GetDefaultServer(_config);
            if (item == null)
            {
                return;
            }
            (new UpdateHandle()).RunAvailabilityCheck((bool success, string msg) =>
            {
                _noticeHandler?.SendMessage(msg, true);

                if (!_config.uiItem.showInTaskbar)
                {
                    return;
                }
                _updateView?.Invoke(EViewAction.DispatcherServerAvailability, msg);
            });
        }

        public void TestServerAvailabilityResult(string msg)
        {
            RunningInfoDisplay = msg;
        }

        #endregion Add Servers

        #region Subscription

        private void SubSetting()
        {
            if (_updateView?.Invoke(EViewAction.SubSettingWindow, null) == true)
            {
                RefreshSubscriptions();
            }
        }

        private void UpdateSubscriptionProcess(string subId, bool blProxy)
        {
            (new UpdateHandle()).UpdateSubscriptionProcess(_config, subId, blProxy, UpdateTaskHandler);
        }

        #endregion Subscription

        #region Setting

        private void OptionSetting()
        {
            var ret = _updateView?.Invoke(EViewAction.OptionSettingWindow, null);
            if (ret == true)
            {
                //RefreshServers();
                Reload();
            }
        }

        private void RoutingSetting()
        {
            var ret = _updateView?.Invoke(EViewAction.RoutingSettingWindow, null);
            if (ret == true)
            {
                ConfigHandler.InitBuiltinRouting(_config);
                RefreshRoutingsMenu();
                //RefreshServers();
                Reload();
            }
        }

        private void DNSSetting()
        {
            var ret = _updateView?.Invoke(EViewAction.DNSSettingWindow, null);
            if (ret == true)
            {
                Reload();
            }
        }

        private void RebootAsAdmin()
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                Arguments = Global.RebootAs,
                WorkingDirectory = Utils.StartupPath(),
                FileName = Utils.GetExePath().AppendQuotes(),
                Verb = "runas",
            };
            try
            {
                Process.Start(startInfo);
                MyAppExit(false);
            }
            catch { }
        }

        #endregion Setting

        #region CheckUpdate

        private void CheckUpdateN()
        {
            void _updateUI(bool success, string msg)
            {
                _noticeHandler?.SendMessage(msg);
                if (success)
                {
                    MyAppExit(false);
                }
            }
            (new UpdateHandle()).CheckUpdateGuiN(_config, _updateUI, _config.guiItem.checkPreReleaseUpdate);
        }

        private void CheckUpdateCore(ECoreType type, bool? preRelease)
        {
            void _updateUI(bool success, string msg)
            {
                _noticeHandler?.SendMessage(msg);
                if (success)
                {
                    CloseCore();

                    string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(msg));
                    string toPath = Utils.GetBinPath("", type.ToString());

                    FileManager.ZipExtractToFile(fileName, toPath, _config.guiItem.ignoreGeoUpdateCore ? "geo" : "");

                    _noticeHandler?.SendMessage(ResUI.MsgUpdateV2rayCoreSuccessfullyMore);

                    Reload();

                    _noticeHandler?.SendMessage(ResUI.MsgUpdateV2rayCoreSuccessfully);

                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }
            }
            (new UpdateHandle()).CheckUpdateCore(type, _config, _updateUI, preRelease ?? _config.guiItem.checkPreReleaseUpdate);
        }

        private void CheckUpdateGeo()
        {
            (new UpdateHandle()).UpdateGeoFileAll(_config, UpdateTaskHandler);
        }

        #endregion CheckUpdate

        #region core job

        public void Reload()
        {
            BlReloadEnabled = false;

            LoadCore().ContinueWith(task =>
            {
                TestServerAvailability();

                _updateView?.Invoke(EViewAction.DispatcherReload, null);
            });
        }

        public void ReloadResult()
        {
            ChangeSystemProxyStatus(_config.systemProxyItem.sysProxyType, false);
            BlReloadEnabled = true;
            ShowClashUI = _config.IsRunningCore(ECoreType.clash);
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
                if (_config.tunModeItem.enableTun)
                {
                    Thread.Sleep(1000);
                    //WindowsUtils.RemoveTunDevice();
                }

                var node = ConfigHandler.GetDefaultServer(_config);
                _coreHandler.LoadCore(node);
            });
        }

        private void CloseCore()
        {
            ConfigHandler.SaveConfig(_config, false);

            ChangeSystemProxyStatus(ESysProxyType.ForcedClear, false);

            _coreHandler.CoreStop();
        }

        #endregion core job

        #region System proxy and Routings

        public void SetListenerType(ESysProxyType type)
        {
            if (_config.systemProxyItem.sysProxyType == type)
            {
                return;
            }
            _config.systemProxyItem.sysProxyType = type;
            ChangeSystemProxyStatus(type, true);

            SystemProxySelected = (int)_config.systemProxyItem.sysProxyType;
            ConfigHandler.SaveConfig(_config, false);
        }

        private void ChangeSystemProxyStatus(ESysProxyType type, bool blChange)
        {
            SysProxyHandle.UpdateSysProxy(_config, _config.tunModeItem.enableTun ? true : false);
            _noticeHandler?.SendMessage($"{ResUI.TipChangeSystemProxy} - {_config.systemProxyItem.sysProxyType.ToString()}", true);

            BlSystemProxyClear = (type == ESysProxyType.ForcedClear);
            BlSystemProxySet = (type == ESysProxyType.ForcedChange);
            BlSystemProxyNothing = (type == ESysProxyType.Unchanged);
            BlSystemProxyPac = (type == ESysProxyType.Pac);

            InboundDisplayStaus();

            if (blChange)
            {
                _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
            }
        }

        private void RefreshRoutingsMenu()
        {
            _routingItems.Clear();
            if (!_config.routingBasicItem.enableRoutingAdvanced)
            {
                BlRouting = false;
                return;
            }

            BlRouting = true;
            var routings = LazyConfig.Instance.RoutingItems();
            foreach (var item in routings)
            {
                _routingItems.Add(item);
                if (item.id == _config.routingBasicItem.routingIndexId)
                {
                    SelectedRouting = item;
                }
            }
        }

        private void RoutingSelectedChanged(bool c)
        {
            if (!c)
            {
                return;
            }

            if (SelectedRouting == null)
            {
                return;
            }

            var item = LazyConfig.Instance.GetRoutingItem(SelectedRouting?.id);
            if (item is null)
            {
                return;
            }
            if (_config.routingBasicItem.routingIndexId == item.id)
            {
                return;
            }

            if (ConfigHandler.SetDefaultRouting(_config, item) == 0)
            {
                _noticeHandler?.SendMessage(ResUI.TipChangeRouting, true);
                Reload();
                _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
            }
        }

        private void DoSystemProxySelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (_config.systemProxyItem.sysProxyType == (ESysProxyType)SystemProxySelected)
            {
                return;
            }
            SetListenerType((ESysProxyType)SystemProxySelected);
        }

        private void DoEnableTun(bool c)
        {
            if (_config.tunModeItem.enableTun != EnableTun)
            {
                _config.tunModeItem.enableTun = EnableTun;
                // When running as a non-administrator, reboot to administrator mode
                if (EnableTun && !WindowsUtils.IsAdministrator())
                {
                    _config.tunModeItem.enableTun = false;
                    RebootAsAdmin();
                    return;
                }
                Reload();
            }
        }

        #endregion System proxy and Routings

        #region UI

        public void InboundDisplayStaus()
        {
            StringBuilder sb = new();
            sb.Append($"[{EInboundProtocol.socks}:{LazyConfig.Instance.GetLocalPort(EInboundProtocol.socks)}]");
            sb.Append(" | ");
            //if (_config.systemProxyItem.sysProxyType == ESysProxyType.ForcedChange)
            //{
            //    sb.Append($"[{Global.InboundHttp}({ResUI.SystemProxy}):{LazyConfig.Instance.GetLocalPort(Global.InboundHttp)}]");
            //}
            //else
            //{
            sb.Append($"[{EInboundProtocol.http}:{LazyConfig.Instance.GetLocalPort(EInboundProtocol.http)}]");
            //}
            InboundDisplay = $"{ResUI.LabLocal}:{sb}";

            if (_config.inbound[0].allowLANConn)
            {
                if (_config.inbound[0].newPort4LAN)
                {
                    StringBuilder sb2 = new();
                    sb2.Append($"[{EInboundProtocol.socks}:{LazyConfig.Instance.GetLocalPort(EInboundProtocol.socks2)}]");
                    sb2.Append(" | ");
                    sb2.Append($"[{EInboundProtocol.http}:{LazyConfig.Instance.GetLocalPort(EInboundProtocol.http2)}]");
                    InboundLanDisplay = $"{ResUI.LabLAN}:{sb2}";
                }
                else
                {
                    InboundLanDisplay = $"{ResUI.LabLAN}:{sb}";
                }
            }
            else
            {
                InboundLanDisplay = $"{ResUI.LabLAN}:None";
            }
        }

        private void AutoHideStartup()
        {
            if (_config.uiItem.autoHideStartup)
            {
                Observable.Range(1, 1)
                 .Delay(TimeSpan.FromSeconds(1))
                 .Subscribe(x =>
                 {
                     _updateView?.Invoke(EViewAction.ShowHideWindow, false);
                 });
            }
        }

        #endregion UI

        #region UpdateTask

        private void RegUpdateTask(Config config, Action<bool, string> update)
        {
            Task.Run(() => UpdateTaskRunSubscription(config, update));
            Task.Run(() => UpdateTaskRunGeo(config, update));
        }

        private async Task UpdateTaskRunSubscription(Config config, Action<bool, string> update)
        {
            await Task.Delay(60000);
            Logging.SaveLog("UpdateTaskRunSubscription");

            var updateHandle = new UpdateHandle();
            while (true)
            {
                var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                var lstSubs = LazyConfig.Instance.SubItems()
                            .Where(t => t.autoUpdateInterval > 0)
                            .Where(t => updateTime - t.updateTime >= t.autoUpdateInterval * 60)
                            .ToList();

                foreach (var item in lstSubs)
                {
                    updateHandle.UpdateSubscriptionProcess(config, item.id, true, (bool success, string msg) =>
                    {
                        update(success, msg);
                        if (success)
                            Logging.SaveLog("subscription" + msg);
                    });
                    item.updateTime = updateTime;
                    ConfigHandler.AddSubItem(config, item);

                    await Task.Delay(5000);
                }
                await Task.Delay(60000);
            }
        }

        private async Task UpdateTaskRunGeo(Config config, Action<bool, string> update)
        {
            var autoUpdateGeoTime = DateTime.Now;

            await Task.Delay(1000 * 120);
            Logging.SaveLog("UpdateTaskRunGeo");

            var updateHandle = new UpdateHandle();
            while (true)
            {
                var dtNow = DateTime.Now;
                if (config.guiItem.autoUpdateInterval > 0)
                {
                    if ((dtNow - autoUpdateGeoTime).Hours % config.guiItem.autoUpdateInterval == 0)
                    {
                        updateHandle.UpdateGeoFileAll(config, (bool success, string msg) =>
                        {
                            update(false, msg);
                        });
                        autoUpdateGeoTime = dtNow;
                    }
                }

                await Task.Delay(1000 * 3600);
            }
        }

        #endregion UpdateTask
    }
}