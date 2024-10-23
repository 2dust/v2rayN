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

            if (updateView != null)
            {
                InitUpdateView(updateView);
            }
            Init();
        }

        private async Task Init()
        {
            SelectedRouting = new();
            SelectedServer = new();

            if (_config.TunModeItem.EnableTun && AppHandler.Instance.IsAdministrator)
            {
                EnableTun = true;
            }
            else
            {
                _config.TunModeItem.EnableTun = EnableTun = false;
            }

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

        private async Task AddServerViaClipboard()
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.AddServerViaClipboardAsync(null);
        }

        private async Task AddServerViaScan()
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.AddServerViaScanAsync();
        }

        private async Task UpdateSubscriptionProcess(bool blProxy)
        {
            var service = Locator.Current.GetService<MainWindowViewModel>();
            if (service != null) await service.UpdateSubscriptionProcess("", blProxy);
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
            if (Utils.IsNullOrEmpty(SelectedServer.ID))
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
            if (_config.SystemProxyItem.SysProxyType == type)
            {
                return;
            }
            _config.SystemProxyItem.SysProxyType = type;
            await ChangeSystemProxyAsync(type, true);
            NoticeHandler.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {_config.SystemProxyItem.SysProxyType.ToString()}");

            SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
            await ConfigHandler.SaveConfig(_config, false);
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

        public async Task RefreshRoutingsMenu()
        {
            _routingItems.Clear();
            if (!_config.RoutingBasicItem.EnableRoutingAdvanced)
            {
                BlRouting = false;
                return;
            }

            BlRouting = true;
            var routings = await AppHandler.Instance.RoutingItems();
            foreach (var item in routings)
            {
                _routingItems.Add(item);
                if (item.Id == _config.RoutingBasicItem.RoutingIndexId)
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
            if (_config.RoutingBasicItem.RoutingIndexId == item.Id)
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
            if (_config.TunModeItem.EnableTun != EnableTun)
            {
                _config.TunModeItem.EnableTun = EnableTun;
                // When running as a non-administrator, reboot to administrator mode
                if (EnableTun && !AppHandler.Instance.IsAdministrator)
                {
                    _config.TunModeItem.EnableTun = false;
                    Locator.Current.GetService<MainWindowViewModel>()?.RebootAsAdmin();
                    return;
                }
                await ConfigHandler.SaveConfig(_config);
                Locator.Current.GetService<MainWindowViewModel>()?.Reload();
            }
        }

        #endregion System proxy and Routings

        #region UI

        public async Task InboundDisplayStatus()
        {
            StringBuilder sb = new();
            sb.Append($"[{EInboundProtocol.socks}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)}]");
            sb.Append(" | ");
            sb.Append($"[{EInboundProtocol.http}:{AppHandler.Instance.GetLocalPort(EInboundProtocol.http)}]");
            InboundDisplay = $"{ResUI.LabLocal}:{sb}";

            if (_config.Inbound[0].AllowLANConn)
            {
                if (_config.Inbound[0].NewPort4LAN)
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
            SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
            SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.DirectUp), Utils.HumanFy(update.DirectDown));
        }

        #endregion UI
    }
}