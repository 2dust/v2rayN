using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using static ServiceLib.Models.ClashProviders;
using static ServiceLib.Models.ClashProxies;

namespace ServiceLib.ViewModels
{
    public class ClashProxiesViewModel : MyReactiveObject
    {
        private Dictionary<string, ProxiesItem>? _proxies;
        private Dictionary<string, ProvidersItem>? _providers;
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

        public ClashProxiesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            ProxiesReloadCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ProxiesReload();
            });
            ProxiesDelaytestCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ProxiesDelayTest(true);
            });

            ProxiesDelaytestPartCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ProxiesDelayTest(false);
            });
            ProxiesSelectActivityCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetActiveProxy();
            });

            SelectedGroup = new();
            SelectedDetail = new();
            AutoRefresh = _config.ClashUIItem.ProxiesAutoRefresh;
            SortingSelected = _config.ClashUIItem.ProxiesSorting;
            RuleModeSelected = (int)_config.ClashUIItem.RuleMode;

            this.WhenAnyValue(
               x => x.SelectedGroup,
               y => y != null && y.Name.IsNotEmpty())
                   .Subscribe(c => RefreshProxyDetails(c));

            this.WhenAnyValue(
               x => x.RuleModeSelected,
               y => y >= 0)
                   .Subscribe(async c => await DoRulemodeSelected(c));

            this.WhenAnyValue(
               x => x.SortingSelected,
               y => y >= 0)
                  .Subscribe(c => DoSortingSelected(c));

            this.WhenAnyValue(
            x => x.AutoRefresh,
            y => y == true)
                .Subscribe(c => { _config.ClashUIItem.ProxiesAutoRefresh = AutoRefresh; });

            _ = Init();
        }

        private async Task Init()
        {
            _ = DelayTestTask();
        }

        private async Task DoRulemodeSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (_config.ClashUIItem.RuleMode == (ERuleMode)RuleModeSelected)
            {
                return;
            }
            await SetRuleModeCheck((ERuleMode)RuleModeSelected);
        }

        public async Task SetRuleModeCheck(ERuleMode mode)
        {
            if (_config.ClashUIItem.RuleMode == mode)
            {
                return;
            }
            await SetRuleMode(mode);
        }

        private void DoSortingSelected(bool c)
        {
            if (!c)
            {
                return;
            }
            if (SortingSelected != _config.ClashUIItem.ProxiesSorting)
            {
                _config.ClashUIItem.ProxiesSorting = SortingSelected;
            }

            RefreshProxyDetails(c);
        }

        public async Task ProxiesReload()
        {
            await GetClashProxies(true);
            await ProxiesDelayTest();
        }

        #region proxy function

        private async Task SetRuleMode(ERuleMode mode)
        {
            _config.ClashUIItem.RuleMode = mode;

            if (mode != ERuleMode.Unchanged)
            {
                Dictionary<string, string> headers = new()
                {
                    { "mode", mode.ToString().ToLower() }
                };
                await ClashApiHandler.Instance.ClashConfigUpdate(headers);
            }
        }

        private async Task GetClashProxies(bool refreshUI)
        {
            var ret = await ClashApiHandler.Instance.GetClashProxiesAsync(_config);
            if (ret?.Item1 == null || ret.Item2 == null)
            {
                return;
            }
            _proxies = ret.Item1.proxies;
            _providers = ret?.Item2.providers;

            if (refreshUI)
            {
                _updateView?.Invoke(EViewAction.DispatcherRefreshProxyGroups, null);
            }
        }

        public void RefreshProxyGroups()
        {
            var selectedName = SelectedGroup?.Name;
            _proxyGroups.Clear();

            var proxyGroups = ClashApiHandler.Instance.GetClashProxyGroups();
            if (proxyGroups != null && proxyGroups.Count > 0)
            {
                foreach (var it in proxyGroups)
                {
                    if (it.name.IsNullOrEmpty() || !_proxies.ContainsKey(it.name))
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
                        Now = item.now,
                        Name = item.name,
                        Type = item.type
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
                var item = _proxyGroups.FirstOrDefault(t => t.Name == kv.Key);
                if (item != null && item.Name.IsNotEmpty())
                {
                    continue;
                }
                _proxyGroups.Add(new ClashProxyModel()
                {
                    Now = kv.Value.now,
                    Name = kv.Key,
                    Type = kv.Value.type
                });
            }

            if (_proxyGroups != null && _proxyGroups.Count > 0)
            {
                if (selectedName != null && _proxyGroups.Any(t => t.Name == selectedName))
                {
                    SelectedGroup = _proxyGroups.FirstOrDefault(t => t.Name == selectedName);
                }
                else
                {
                    SelectedGroup = _proxyGroups.First();
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
            var name = SelectedGroup?.Name;
            if (name.IsNullOrEmpty())
            {
                return;
            }
            if (_proxies == null)
            {
                return;
            }

            _proxies.TryGetValue(name, out var proxy);
            if (proxy == null || proxy.all == null)
            {
                return;
            }
            var lstDetails = new List<ClashProxyModel>();
            foreach (var item in proxy.all)
            {
                var IsActive = item == proxy.now;

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
                    IsActive = IsActive,
                    Name = item,
                    Type = proxy2.type,
                    Delay = delay <= 0 ? _delayTimeout : delay,
                    DelayName = delay <= 0 ? string.Empty : $"{delay}ms",
                });
            }
            //sort
            switch (SortingSelected)
            {
                case 0:
                    lstDetails = lstDetails.OrderBy(t => t.Delay).ToList();
                    break;

                case 1:
                    lstDetails = lstDetails.OrderBy(t => t.Name).ToList();
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
            _proxies.TryGetValue(name, out var proxy2);
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

        public async Task SetActiveProxy()
        {
            if (SelectedGroup == null || SelectedGroup.Name.IsNullOrEmpty())
            {
                return;
            }
            if (SelectedDetail == null || SelectedDetail.Name.IsNullOrEmpty())
            {
                return;
            }
            var name = SelectedGroup.Name;
            if (name.IsNullOrEmpty())
            {
                return;
            }
            var nameNode = SelectedDetail.Name;
            if (nameNode.IsNullOrEmpty())
            {
                return;
            }
            var selectedProxy = TryGetProxy(name);
            if (selectedProxy == null || selectedProxy.type != "Selector")
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }

            await ClashApiHandler.Instance.ClashSetActiveProxy(name, nameNode);

            selectedProxy.now = nameNode;
            var group = _proxyGroups.FirstOrDefault(it => it.Name == SelectedGroup.Name);
            if (group != null)
            {
                group.Now = nameNode;
                var group2 = JsonUtils.DeepCopy(group);
                _proxyGroups.Replace(group, group2);

                SelectedGroup = group2;
            }
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
        }

        private async Task ProxiesDelayTest(bool blAll = true)
        {
            ClashApiHandler.Instance.ClashProxiesDelayTest(blAll, _proxyDetails.ToList(), async (item, result) =>
            {
                if (item == null)
                {
                    await GetClashProxies(true);
                    return;
                }
                if (result.IsNullOrEmpty())
                {
                    return;
                }

                _updateView?.Invoke(EViewAction.DispatcherProxiesDelayTest, new SpeedTestResult() { IndexId = item.Name, Delay = result });
            });
            await Task.CompletedTask;
        }

        public void ProxiesDelayTestResult(SpeedTestResult result)
        {
            //UpdateHandler(false, $"{item.name}={result}");
            var detail = _proxyDetails.FirstOrDefault(it => it.Name == result.IndexId);
            if (detail != null)
            {
                var dicResult = JsonUtils.Deserialize<Dictionary<string, object>>(result.Delay);
                if (dicResult != null && dicResult.TryGetValue("delay", out var value))
                {
                    detail.Delay = Convert.ToInt32(value.ToString());
                    detail.DelayName = $"{detail.Delay}ms";
                }
                else if (dicResult != null && dicResult.TryGetValue("message", out var value1))
                {
                    detail.Delay = _delayTimeout;
                    detail.DelayName = $"{value1}";
                }
                else
                {
                    detail.Delay = _delayTimeout;
                    detail.DelayName = string.Empty;
                }
                _proxyDetails.Replace(detail, JsonUtils.DeepCopy(detail));
            }
        }

        #endregion proxy function

        #region task

        public async Task DelayTestTask()
        {
            _ = Task.Run(async () =>
              {
                  var numOfExecuted = 1;
                  while (true)
                  {
                      await Task.Delay(1000 * 60);
                      numOfExecuted++;
                      if (!(AutoRefresh && _config.UiItem.ShowInTaskbar && _config.IsRunningCore(ECoreType.sing_box)))
                      {
                          continue;
                      }
                      if (_config.ClashUIItem.ProxiesAutoDelayTestInterval <= 0)
                      {
                          continue;
                      }
                      if (numOfExecuted % _config.ClashUIItem.ProxiesAutoDelayTestInterval != 0)
                      {
                          continue;
                      }
                      await ProxiesDelayTest();
                  }
              });
            await Task.CompletedTask;
        }

        #endregion task
    }
}
