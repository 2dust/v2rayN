using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Text;

namespace ServiceLib.ViewModels
{
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
        public ReactiveCommand<Unit, Unit> NotifyLeftClickCmd { get; }

        #region System Proxy

        [Reactive]
        public bool BlSystemProxyClear { get; set; }

        [Reactive]
        public bool BlSystemProxySet { get; set; }

        [Reactive]
        public bool BlSystemProxyNothing { get; set; }

        [Reactive]
        public bool BlSystemProxyPac { get; set; }

        [Reactive]
        public bool BlNotSystemProxyClear { get; set; }

        [Reactive]
        public bool BlNotSystemProxySet { get; set; }

        [Reactive]
        public bool BlNotSystemProxyNothing { get; set; }

        [Reactive]
        public bool BlNotSystemProxyPac { get; set; }

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

        #endregion UI

        public StatusBarViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

            if (updateView != null)
            {
                Init(updateView);
            }

            SelectedRouting = new();
            SelectedServer = new();

            if (_config.tunModeItem.enableTun && AppHandler.Instance.IsAdministrator)
            {
                EnableTun = true;
            }
            else
            {
                _config.tunModeItem.enableTun = EnableTun = false;
            }

            RefreshRoutingsMenu();
            InboundDisplayStatus();
            ChangeSystemProxyAsync(_config.systemProxyItem.sysProxyType, true);

            #region WhenAnyValue && ReactiveCommand

            this.WhenAnyValue(
                    x => x.SelectedRouting,
                    y => y != null && !y.remarks.IsNullOrEmpty())
                .Subscribe(c => RoutingSelectedChangedAsync(c));

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

            NotifyLeftClickCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                Locator.Current.GetService<MainWindowViewModel>()?.ShowHideWindow(null);
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
        }

        public void Init(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _updateView = updateView;
            if (_updateView != null)
            {
                MessageBus.Current.Listen<string>(EMsgCommand.RefreshProfiles.ToString())
                    .Subscribe(async x => await _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null));
            }
        }

        private async Task AddServerViaClipboard()
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.AddServerViaClipboardAsync(null);
        }

        private async Task AddServerViaScan()
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.AddServerViaScanTaskAsync();
        }

        private async Task UpdateSubscriptionProcess(bool blProxy)
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.UpdateSubscriptionProcess("", blProxy);
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
            var lstModel = AppHandler.Instance.ProfileItems(_config.subIndexId, "");

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
            Locator.Current.GetService<ProfilesViewModel>()?.SetDefaultServer(SelectedServer.ID);
        }

        public async Task TestServerAvailability()
        {
            var item = ConfigHandler.GetDefaultServer(_config);
            if (item == null)
            {
                return;
            }
            await (new UpdateService()).RunAvailabilityCheck(async (bool success, string msg) =>
            {
                NoticeHandler.Instance.SendMessageEx(msg);
                _updateView?.Invoke(EViewAction.DispatcherServerAvailability, msg);
            });
        }

        public void TestServerAvailabilityResult(string msg)
        {
            RunningInfoDisplay = msg;
        }

        #region System proxy and Routings

        public async Task SetListenerType(ESysProxyType type)
        {
            if (_config.systemProxyItem.sysProxyType == type)
            {
                return;
            }
            _config.systemProxyItem.sysProxyType = type;
            ChangeSystemProxyAsync(type, true);
            NoticeHandler.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {_config.systemProxyItem.sysProxyType.ToString()}");

            SystemProxySelected = (int)_config.systemProxyItem.sysProxyType;
            ConfigHandler.SaveConfig(_config, false);
        }

        public async Task ChangeSystemProxyAsync(ESysProxyType type, bool blChange)
        {
            await SysProxyHandler.UpdateSysProxy(_config, false);

            BlSystemProxyClear = (type == ESysProxyType.ForcedClear);
            BlSystemProxySet = (type == ESysProxyType.ForcedChange);
            BlSystemProxyNothing = (type == ESysProxyType.Unchanged);
            BlSystemProxyPac = (type == ESysProxyType.Pac);

            BlNotSystemProxyClear = !BlSystemProxyClear;
            BlNotSystemProxySet = !BlSystemProxySet;
            BlNotSystemProxyNothing = !BlSystemProxyNothing;
            BlNotSystemProxyPac = !BlSystemProxyPac;

            if (blChange)
            {
                _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
            }
        }

        public void RefreshRoutingsMenu()
        {
            _routingItems.Clear();
            if (!_config.routingBasicItem.enableRoutingAdvanced)
            {
                BlRouting = false;
                return;
            }

            BlRouting = true;
            var routings = AppHandler.Instance.RoutingItems();
            foreach (var item in routings)
            {
                _routingItems.Add(item);
                if (item.id == _config.routingBasicItem.routingIndexId)
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

            var item = AppHandler.Instance.GetRoutingItem(SelectedRouting?.id);
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
                NoticeHandler.Instance.SendMessageEx(ResUI.TipChangeRouting);
                Locator.Current.GetService<MainWindowViewModel>()?.Reload();
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
                if (EnableTun && !AppHandler.Instance.IsAdministrator)
                {
                    _config.tunModeItem.enableTun = false;
                    Locator.Current.GetService<MainWindowViewModel>()?.RebootAsAdmin();
                    return;
                }
                ConfigHandler.SaveConfig(_config);
                Locator.Current.GetService<MainWindowViewModel>()?.Reload();
            }
        }

        #endregion System proxy and Routings

        #region UI

        public void InboundDisplayStatus()
        {
            StringBuilder sb = new();
            sb.Append($"[{EInboundProtocol.socks}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)}]");
            sb.Append(" | ");
            sb.Append($"[{EInboundProtocol.http}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.http)}]");
            InboundDisplay = $"{ResUI.LabLocal}:{sb}";

            if (_config.inbound[0].allowLANConn)
            {
                if (_config.inbound[0].newPort4LAN)
                {
                    StringBuilder sb2 = new();
                    sb2.Append($"[{EInboundProtocol.socks}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks2)}]");
                    sb2.Append(" | ");
                    sb2.Append($"[{EInboundProtocol.http}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.http2)}]");
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

        public void UpdateStatistics(ServerSpeedItem update)
        {
            SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.proxyUp), Utils.HumanFy(update.proxyDown));
            SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.directUp), Utils.HumanFy(update.directDown));
        }

        #endregion UI
    }
}