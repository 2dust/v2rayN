using System.Reactive.Concurrency;
using static ServiceLib.Models.ClashProviders;
using static ServiceLib.Models.ClashProxies;

namespace ServiceLib.ViewModels;

public class ClashProxiesViewModel : MyReactiveObject
{
    private Dictionary<string, ProxiesItem>? _proxies;
    private Dictionary<string, ProvidersItem>? _providers;
    private readonly int _delayTimeout = 99999999;

    public IObservableCollection<ClashProxyModel> ProxyGroups { get; } = new ObservableCollectionExtended<ClashProxyModel>();
    public IObservableCollection<ClashProxyModel> ProxyDetails { get; } = new ObservableCollectionExtended<ClashProxyModel>();

    [Reactive]
    public ClashProxyModel SelectedGroup { get; set; }

    [Reactive]
    public ClashProxyModel SelectedDetail { get; set; }

    public ReactiveCommand<Unit, Unit> ProxiesReloadCmd { get; }
    public ReactiveCommand<Unit, Unit> ProxiesDelayTestCmd { get; }
    public ReactiveCommand<Unit, Unit> ProxiesDelayTestPartCmd { get; }
    public ReactiveCommand<Unit, Unit> ProxiesSelectActivityCmd { get; }

    [Reactive]
    public int RuleModeSelected { get; set; }

    [Reactive]
    public int SortingSelected { get; set; }

    [Reactive]
    public bool AutoRefresh { get; set; }

    public ClashProxiesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        ProxiesReloadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ProxiesReload();
        });
        ProxiesDelayTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ProxiesDelayTest(true);
        });

        ProxiesDelayTestPartCmd = ReactiveCommand.CreateFromTask(async () =>
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

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(
           x => x.SelectedGroup,
           y => y != null && y.Name.IsNotEmpty())
               .Subscribe(c => RefreshProxyDetails(c));

        this.WhenAnyValue(
           x => x.RuleModeSelected,
           y => y >= 0)
               .Subscribe(async c => await DoRuleModeSelected(c));

        this.WhenAnyValue(
           x => x.SortingSelected,
           y => y >= 0)
              .Subscribe(c => DoSortingSelected(c));

        this.WhenAnyValue(
        x => x.AutoRefresh,
        y => y == true)
            .Subscribe(c => { _config.ClashUIItem.ProxiesAutoRefresh = AutoRefresh; });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.ProxiesReloadRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await ProxiesReload());

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        await DelayTestTask();
    }

    private async Task DoRuleModeSelected(bool c)
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
            await ClashApiManager.Instance.ClashConfigUpdate(headers);
        }
    }

    private async Task GetClashProxies(bool refreshUI)
    {
        var ret = await ClashApiManager.Instance.GetClashProxiesAsync();
        if (ret?.Item1 == null || ret.Item2 == null)
        {
            return;
        }
        _proxies = ret.Item1.proxies;
        _providers = ret?.Item2.providers;

        if (refreshUI)
        {
            RxApp.MainThreadScheduler.Schedule(() => _ = RefreshProxyGroups());
        }
    }

    public async Task RefreshProxyGroups()
    {
        if (_proxies == null)
        {
            return;
        }

        var selectedName = SelectedGroup?.Name;
        ProxyGroups.Clear();

        var proxyGroups = ClashApiManager.Instance.GetClashProxyGroups();
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
                ProxyGroups.Add(new ClashProxyModel()
                {
                    Now = item.now,
                    Name = item.name,
                    Type = item.type
                });
            }
        }

        //from api
        foreach (var kv in _proxies)
        {
            if (!Global.allowSelectType.Contains(kv.Value.type.ToLower()))
            {
                continue;
            }
            var item = ProxyGroups.FirstOrDefault(t => t.Name == kv.Key);
            if (item != null && item.Name.IsNotEmpty())
            {
                continue;
            }
            ProxyGroups.Add(new ClashProxyModel()
            {
                Now = kv.Value.now,
                Name = kv.Key,
                Type = kv.Value.type
            });
        }

        if (ProxyGroups != null && ProxyGroups.Count > 0)
        {
            if (selectedName != null && ProxyGroups.Any(t => t.Name == selectedName))
            {
                SelectedGroup = ProxyGroups.FirstOrDefault(t => t.Name == selectedName);
            }
            else
            {
                SelectedGroup = ProxyGroups.First();
            }
        }
        else
        {
            SelectedGroup = new();
        }
        await Task.CompletedTask;
    }

    private void RefreshProxyDetails(bool c)
    {
        ProxyDetails.Clear();
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
        if (proxy?.all == null)
        {
            return;
        }
        var lstDetails = new List<ClashProxyModel>();
        foreach (var item in proxy.all)
        {
            var proxy2 = TryGetProxy(item);
            if (proxy2 == null)
            {
                continue;
            }
            var delay = proxy2.history?.Count > 0 ? proxy2.history.Last().delay : -1;

            lstDetails.Add(new ClashProxyModel()
            {
                IsActive = item == proxy.now,
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
        ProxyDetails.AddRange(lstDetails);
    }

    private ProxiesItem? TryGetProxy(string name)
    {
        if (_proxies == null)
        {
            return null;
        }
        _proxies.TryGetValue(name, out var proxy2);
        if (proxy2 != null)
        {
            return proxy2;
        }
        //from providers
        if (_providers != null)
        {
            foreach (var kv in _providers)
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
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        await ClashApiManager.Instance.ClashSetActiveProxy(name, nameNode);

        selectedProxy.now = nameNode;
        var group = ProxyGroups.FirstOrDefault(it => it.Name == SelectedGroup.Name);
        if (group != null)
        {
            group.Now = nameNode;
            var group2 = JsonUtils.DeepCopy(group);
            ProxyGroups.Replace(group, group2);

            SelectedGroup = group2;
        }
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
    }

    private async Task ProxiesDelayTest(bool blAll = true)
    {
        ClashApiManager.Instance.ClashProxiesDelayTest(blAll, ProxyDetails.ToList(), async (item, result) =>
        {
            if (item == null || result.IsNullOrEmpty())
            {
                return;
            }

            var model = new SpeedTestResult() { IndexId = item.Name, Delay = result };
            RxApp.MainThreadScheduler.Schedule(model, (scheduler, model) =>
            {
                _ = ProxiesDelayTestResult(model);
                return Disposable.Empty;
            });
            await Task.CompletedTask;
        });
        await Task.CompletedTask;
    }

    public async Task ProxiesDelayTestResult(SpeedTestResult result)
    {
        var detail = ProxyDetails.FirstOrDefault(it => it.Name == result.IndexId);
        if (detail == null)
        {
            return;
        }

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
        await Task.CompletedTask;
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
                  if (!(AutoRefresh && AppManager.Instance.ShowInTaskbar && AppManager.Instance.IsRunningCore(ECoreType.sing_box)))
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
