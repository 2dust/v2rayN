using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Reactive.Linq;
using v2rayN.Base;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.Resx;
using static v2rayN.Models.ClashProviders;
using static v2rayN.Models.ClashProxies;

namespace v2rayN.ViewModels
{
    public class ClashProxiesViewModel : MyReactiveObject
    {
        private Dictionary<String, ProxiesItem>? _proxies;
        private Dictionary<String, ProvidersItem>? _providers;
        private int _delayTimeout = 99999999;

        private IObservableCollection<ClashProxyModel> _proxyGroups = new ObservableCollectionExtended<ClashProxyModel>();
        private IObservableCollection<ClashProxyModel> _proxyDetails = new ObservableCollectionExtended<ClashProxyModel>();

        public IObservableCollection<ClashProxyModel> ProxyGroups => _proxyGroups;
        public IObservableCollection<ClashProxyModel> ProxyDetails => _proxyDetails;

        [Reactive]
        public ClashProxyModel SelectedGroup { get; set; }

        [Reactive]
        public ClashProxyModel SelectedDetail { get; set; }

        public ReactiveCommand<Unit, Unit> ProxiesReloadCmd { get; }
        public ReactiveCommand<Unit, Unit> ProxiesDelaytestCmd { get; }
        public ReactiveCommand<Unit, Unit> ProxiesDelaytestPartCmd { get; }
        public ReactiveCommand<Unit, Unit> ProxiesSelectActivityCmd { get; }

        [Reactive]
        public int RuleModeSelected { get; set; }

        [Reactive]
        public int SortingSelected { get; set; }

        [Reactive]
        public bool AutoRefresh { get; set; }

        public ClashProxiesViewModel(Func<EViewAction, object?, bool>? updateView)
        {
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _config = LazyConfig.Instance.Config;
            _updateView = updateView;

            SelectedGroup = new();
            SelectedDetail = new();

            AutoRefresh = _config.clashUIItem.proxiesAutoRefresh;
            SortingSelected = _config.clashUIItem.proxiesSorting;
            RuleModeSelected = (int)_config.clashUIItem.ruleMode;

            this.WhenAnyValue(
               x => x.SelectedGroup,
               y => y != null && !string.IsNullOrEmpty(y.name))
                   .Subscribe(c => RefreshProxyDetails(c));

            this.WhenAnyValue(
               x => x.RuleModeSelected,
               y => y >= 0)
                   .Subscribe(c => DoRulemodeSelected(c));

            this.WhenAnyValue(
               x => x.SortingSelected,
               y => y >= 0)
                  .Subscribe(c => DoSortingSelected(c));

            this.WhenAnyValue(
            x => x.AutoRefresh,
            y => y == true)
                .Subscribe(c => { _config.clashUIItem.proxiesAutoRefresh = AutoRefresh; });

            ProxiesReloadCmd = ReactiveCommand.Create(() =>
            {
                ProxiesReload();
            });
            ProxiesDelaytestCmd = ReactiveCommand.Create(() =>
            {
                ProxiesDelayTest(true);
            });

            ProxiesDelaytestPartCmd = ReactiveCommand.Create(() =>
            {
                ProxiesDelayTest(false);
            });
            ProxiesSelectActivityCmd = ReactiveCommand.Create(() =>
            {
                SetActiveProxy();
            });

            ProxiesReload();
            DelayTestTask();
        }

        private void DoRulemodeSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (_config.clashUIItem.ruleMode == (ERuleMode)RuleModeSelected)
            {
                return;
            }
            SetRuleModeCheck((ERuleMode)RuleModeSelected);
        }

        public void SetRuleModeCheck(ERuleMode mode)
        {
            if (_config.clashUIItem.ruleMode == mode)
            {
                return;
            }
            SetRuleMode(mode);
        }

        private void DoSortingSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (SortingSelected != _config.clashUIItem.proxiesSorting)
            {
                _config.clashUIItem.proxiesSorting = SortingSelected;
            }

            RefreshProxyDetails(c);
        }

        private void UpdateHandler(bool notify, string msg)
        {
            _noticeHandler?.SendMessage(msg, true);
        }

        public void ProxiesReload()
        {
            GetClashProxies(true);
            ProxiesDelayTest();
        }

        public void ProxiesDelayTest()
        {
            ProxiesDelayTest(true);
        }

        #region proxy function

        private void SetRuleMode(ERuleMode mode)
        {
            _config.clashUIItem.ruleMode = mode;

            if (mode != ERuleMode.Unchanged)
            {
                Dictionary<string, string> headers = new()
                {
                    { "mode", mode.ToString().ToLower() }
                };
                ClashApiHandler.Instance.ClashConfigUpdate(headers);
            }
        }

        private void GetClashProxies(bool refreshUI)
        {
            ClashApiHandler.Instance.GetClashProxies(_config, (it, it2) =>
            {
                //UpdateHandler(false, "Refresh Clash Proxies");
                _proxies = it?.proxies;
                _providers = it2?.providers;

                if (_proxies == null)
                {
                    return;
                }
                if (refreshUI)
                {
                    _updateView?.Invoke(EViewAction.DispatcherRefreshProxyGroups, null);
                }
            });
        }

        public void RefreshProxyGroups()
        {
            var selectedName = SelectedGroup?.name;
            _proxyGroups.Clear();

            var proxyGroups = ClashApiHandler.Instance.GetClashProxyGroups();
            if (proxyGroups != null && proxyGroups.Count > 0)
            {
                foreach (var it in proxyGroups)
                {
                    if (string.IsNullOrEmpty(it.name) || !_proxies.ContainsKey(it.name))
                    {
                        continue;
                    }
                    var item = _proxies[it.name];
                    if (!Global.allowSelectType.Contains(item.type.ToLower()))
                    {
                        continue;
                    }
                    _proxyGroups.Add(new ClashProxyModel()
                    {
                        now = item.now,
                        name = item.name,
                        type = item.type
                    });
                }
            }

            //from api
            foreach (KeyValuePair<string, ProxiesItem> kv in _proxies)
            {
                if (!Global.allowSelectType.Contains(kv.Value.type.ToLower()))
                {
                    continue;
                }
                var item = _proxyGroups.Where(t => t.name == kv.Key).FirstOrDefault();
                if (item != null && !string.IsNullOrEmpty(item.name))
                {
                    continue;
                }
                _proxyGroups.Add(new ClashProxyModel()
                {
                    now = kv.Value.now,
                    name = kv.Key,
                    type = kv.Value.type
                });
            }

            if (_proxyGroups != null && _proxyGroups.Count > 0)
            {
                if (selectedName != null && _proxyGroups.Any(t => t.name == selectedName))
                {
                    SelectedGroup = _proxyGroups.FirstOrDefault(t => t.name == selectedName);
                }
                else
                {
                    SelectedGroup = _proxyGroups[0];
                }
            }
            else
            {
                SelectedGroup = new();
            }
        }

        private void RefreshProxyDetails(bool c)
        {
            _proxyDetails.Clear();
            if (!c)
            {
                return;
            }
            var name = SelectedGroup?.name;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_proxies == null)
            {
                return;
            }

            _proxies.TryGetValue(name, out ProxiesItem proxy);
            if (proxy == null || proxy.all == null)
            {
                return;
            }
            var lstDetails = new List<ClashProxyModel>();
            foreach (var item in proxy.all)
            {
                var isActive = item == proxy.now;

                var proxy2 = TryGetProxy(item);
                if (proxy2 == null)
                {
                    continue;
                }
                int delay = -1;
                if (proxy2.history.Count > 0)
                {
                    delay = proxy2.history[proxy2.history.Count - 1].delay;
                }

                lstDetails.Add(new ClashProxyModel()
                {
                    isActive = isActive,
                    name = item,
                    type = proxy2.type,
                    delay = delay <= 0 ? _delayTimeout : delay,
                    delayName = delay <= 0 ? string.Empty : $"{delay}ms",
                });
            }
            //sort
            switch (SortingSelected)
            {
                case 0:
                    lstDetails = lstDetails.OrderBy(t => t.delay).ToList();
                    break;

                case 1:
                    lstDetails = lstDetails.OrderBy(t => t.name).ToList();
                    break;

                default:
                    break;
            }
            _proxyDetails.AddRange(lstDetails);
        }

        private ProxiesItem? TryGetProxy(string name)
        {
            if (_proxies is null)
                return null;
            _proxies.TryGetValue(name, out ProxiesItem proxy2);
            if (proxy2 != null)
            {
                return proxy2;
            }
            //from providers
            if (_providers != null)
            {
                foreach (KeyValuePair<string, ProvidersItem> kv in _providers)
                {
                    if (Global.proxyVehicleType.Contains(kv.Value.vehicleType.ToLower()))
                    {
                        var proxy3 = kv.Value.proxies.FirstOrDefault(t => t.name == name);
                        if (proxy3 != null)
                        {
                            return proxy3;
                        }
                    }
                }
            }
            return null;
        }

        public void SetActiveProxy()
        {
            if (SelectedGroup == null || string.IsNullOrEmpty(SelectedGroup.name))
            {
                return;
            }
            if (SelectedDetail == null || string.IsNullOrEmpty(SelectedDetail.name))
            {
                return;
            }
            var name = SelectedGroup.name;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            var nameNode = SelectedDetail.name;
            if (string.IsNullOrEmpty(nameNode))
            {
                return;
            }
            var selectedProxy = TryGetProxy(name);
            if (selectedProxy == null || selectedProxy.type != "Selector")
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
                return;
            }

            ClashApiHandler.Instance.ClashSetActiveProxy(name, nameNode);

            selectedProxy.now = nameNode;
            var group = _proxyGroups.Where(it => it.name == SelectedGroup.name).FirstOrDefault();
            if (group != null)
            {
                group.now = nameNode;
                var group2 = JsonUtils.DeepCopy(group);
                _proxyGroups.Replace(group, group2);

                SelectedGroup = group2;
                 
            }
            _noticeHandler?.Enqueue(ResUI.OperationSuccess);
             
        }

        private void ProxiesDelayTest(bool blAll)
        {
            //UpdateHandler(false, "Clash Proxies Latency Test");

            ClashApiHandler.Instance.ClashProxiesDelayTest(blAll, _proxyDetails.ToList(), (item, result) =>
            {
                if (item == null)
                {
                    GetClashProxies(true);
                    return;
                }
                if (string.IsNullOrEmpty(result))
                {
                    return;
                }

                _updateView?.Invoke(EViewAction.DispatcherProxiesDelayTest, new SpeedTestResult() { IndexId = item.name, Delay = result });
            });
        }

        public void ProxiesDelayTestResult(SpeedTestResult result)
        {
            //UpdateHandler(false, $"{item.name}={result}");
            var detail = _proxyDetails.Where(it => it.name == result.IndexId).FirstOrDefault();
            if (detail != null)
            {
                var dicResult = JsonUtils.Deserialize<Dictionary<string, object>>(result.Delay);
                if (dicResult != null && dicResult.ContainsKey("delay"))
                {
                    detail.delay = Convert.ToInt32(dicResult["delay"].ToString());
                    detail.delayName = $"{detail.delay}ms";
                }
                else if (dicResult != null && dicResult.ContainsKey("message"))
                {
                    detail.delay = _delayTimeout;
                    detail.delayName = $"{dicResult["message"]}";
                }
                else
                {
                    detail.delay = _delayTimeout;
                    detail.delayName = String.Empty;
                }
                _proxyDetails.Replace(detail, JsonUtils.DeepCopy(detail));
            }
        }

        #endregion proxy function

        #region task

        public void DelayTestTask()
        {
            var lastTime = DateTime.Now;

            Observable.Interval(TimeSpan.FromSeconds(60))
              .Subscribe(x =>
              {
                  if (!(AutoRefresh && _config.uiItem.showInTaskbar && _config.IsRunningCore(ECoreType.clash)))
                  {
                      return;
                  }
                  var dtNow = DateTime.Now;
                  if (_config.clashUIItem.proxiesAutoDelayTestInterval > 0)
                  {
                      if ((dtNow - lastTime).Minutes % _config.clashUIItem.proxiesAutoDelayTestInterval == 0)
                      {
                          ProxiesDelayTest();
                          lastTime = dtNow;
                      }
                      Task.Delay(1000).Wait();
                  }
              });
        }

        #endregion task
    }
}