namespace ServiceLib.ViewModels;

public partial class ClashProxiesViewModel : MyReactiveObject
{
    private readonly int _delayTimeout = 99999999;
    private ClashItem _clashItem = new();

    public ClashProxiesViewModel()
    {
        _config = AppManager.Instance.Config;

        ProxiesReloadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ProxiesReload();
        });
        ProxyDelayTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!string.IsNullOrEmpty(SelectedDetail?.Name))
            {
                await TestProxyDelay(SelectedDetail.Name);
            }
        });

        GroupProxiesDelayTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await TestGroupProxiesDelay();
        });
        ProxiesSelectActivityCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetActiveProxy();
        });

        AutoRefresh = _config.ClashUIItem.ProxiesAutoRefresh;
        SortingSelected = _config.ClashUIItem.ProxiesSorting;
        RuleModeSelected = nameof(ERuleMode.Rule);

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(x => x.SelectedGroup)
            .Where(y => y != null && y.Name.IsNotEmpty())
            .Subscribe(_ => RefreshProxyDetails());

        this.WhenAnyValue(x => x.RuleModeSelected)
            .Where(y => !string.IsNullOrEmpty(y))
            .Skip(1)
            .SubscribeAsync(async x => await SetRuleMode(x));

        this.WhenAnyValue(x => x.SortingSelected)
            .Where(y => y >= 0)
            .Subscribe(_ => DoSortingSelected());

        this.WhenAnyValue(x => x.AutoRefresh)
            .Where(y => y)
            .Subscribe(_ => { _config.ClashUIItem.ProxiesAutoRefresh = AutoRefresh; });

        #endregion WhenAnyValue && ReactiveCommand

        _ = Task.Factory.StartNew(
            async () => await GetClashProxiesTask(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public BulkObservableCollection<ClashProxyModel> ProxyGroups { get; } = [];
    public BulkObservableCollection<ClashProxyModel> ProxyDetails { get; } = [];

    public BulkObservableCollection<string> ClashModes { get; } = new(Enum.GetNames<ERuleMode>().ToList());

    [Reactive] public partial ClashProxyModel? SelectedGroup { get; set; }

    [Reactive] public partial ClashProxyModel? SelectedDetail { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> ProxiesReloadCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ProxyDelayTestCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GroupProxiesDelayTestCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ProxiesSelectActivityCmd { get; }

    [Reactive] public partial string RuleModeSelected { get; set; }

    [Reactive] public partial int SortingSelected { get; set; }

    [Reactive] public partial bool AutoRefresh { get; set; }

    private void DoSortingSelected()
    {
        if (SortingSelected != _config.ClashUIItem.ProxiesSorting)
        {
            _config.ClashUIItem.ProxiesSorting = SortingSelected;
        }

        RefreshProxyDetails();
    }

    public async Task ProxiesReload()
    {
        await GetClashProxies();
        await GetClashModes();
    }

    #region task

    public async Task GetClashProxiesTask()
    {
        var numOfExecuted = 1;
        while (true)
        {
            await Task.Delay(1000 * 60);
            numOfExecuted++;
            if (!(AutoRefresh && AppManager.Instance.ShowInTaskbar &&
                  AppManager.Instance.IsRunningCore(ECoreType.sing_box)))
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
            await GetClashProxies();
        }
    }

    #endregion task

    #region proxy function

    private async Task SetRuleMode(string mode)
    {
        await ClashApiManager.Instance.UpdateClashMode(mode);
    }

    private async Task GetClashProxies()
    {
        var ret = await ClashApiManager.Instance.GetProxies();
        if (ret?.IsEmpty() != false)
        {
            return;
        }
        _clashItem = ret;

        RxSchedulers.MainThreadScheduler.Schedule(() => _ = RefreshProxyGroups());
    }

    public async Task RefreshProxyGroups()
    {
        if (_clashItem.IsEmpty())
        {
            return;
        }

        var selectedName = SelectedGroup?.Name;

        var lstProxyGroups = new List<ClashProxyModel>();

        var globalName = "GLOBAL";
        foreach (var kv in _clashItem.Proxies)
        {
            if (!Global.allowSelectType.Contains(kv.Value.type?.ToLower()))
            {
                continue;
            }
            if (kv.Key == globalName)
            {
                continue;
            }
            var item = lstProxyGroups.FirstOrDefault(t => t.Name == kv.Key);
            if (item != null && item.Name.IsNotEmpty())
            {
                continue;
            }
            lstProxyGroups.Add(new ClashProxyModel
            {
                Now = kv.Value.now,
                Name = kv.Key,
                Type = kv.Value.type,
            });
        }
        if (_clashItem.Proxies.TryGetValue(globalName, out var globalProxy))
        {
            lstProxyGroups.Add(new ClashProxyModel
            {
                Now = globalProxy.now,
                Name = globalName,
                Type = globalProxy.type,
            });
        }

        ProxyGroups.ReplaceRange(lstProxyGroups);

        if (ProxyGroups is { Count: > 0 })
        {
            SelectedGroup = ProxyGroups.FirstOrDefault(t => t.Name == selectedName) ?? ProxyGroups.First();
        }
        else
        {
            SelectedGroup = null;
        }
        await Task.CompletedTask;
    }

    private void RefreshProxyDetails()
    {
        var name = SelectedGroup?.Name;
        if (name.IsNullOrEmpty())
        {
            return;
        }
        if (_clashItem.IsEmpty())
        {
            return;
        }

        _clashItem.Proxies.TryGetValue(name, out var proxy);
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

            lstDetails.Add(new ClashProxyModel
            {
                IsActive = item == proxy.now,
                Name = item,
                Type = proxy2.type,
                Delay = delay <= 0 ? _delayTimeout : delay,
                DelayName = delay <= 0 ? string.Empty : $"{delay}ms",
            });
        }
        // sort
        switch (SortingSelected)
        {
            case 0:
                lstDetails = lstDetails.OrderBy(t => t.Delay).ToList();
                break;

            case 1:
                lstDetails = lstDetails.OrderBy(t => t.Name).ToList();
                break;
        }
        ProxyDetails.ReplaceRange(lstDetails);
    }

    private ClashProxy? TryGetProxy(string? name)
    {
        if (name.IsNullOrEmpty())
        {
            return null;
        }
        _clashItem.Proxies.TryGetValue(name, out var proxy2);
        return proxy2;
    }

    public async Task SetActiveProxy()
    {
        if (SelectedGroup.Name.IsNullOrEmpty())
        {
            return;
        }
        if (SelectedDetail.Name.IsNullOrEmpty())
        {
            return;
        }
        var groupName = SelectedGroup.Name;
        if (groupName.IsNullOrEmpty())
        {
            return;
        }
        var nodeName = SelectedDetail.Name;
        if (nodeName.IsNullOrEmpty())
        {
            return;
        }
        var selectedProxy = TryGetProxy(groupName);
        if (selectedProxy is not { type: "Selector" })
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        await ClashApiManager.Instance.SetActiveProxy(groupName, nodeName);
        await GetClashProxies();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
    }

    private async Task GetClashModes()
    {
        var ret = await ClashApiManager.Instance.GetClashModes();
        if (ret is not { Count: > 0 })
        {
            return;
        }
        ClashModes.ReplaceRange(ret);
        var currentMode = await ClashApiManager.Instance.GetClashMode();
        if (currentMode.IsNullOrEmpty())
        {
            return;
        }
        RuleModeSelected = currentMode;
    }

    private async Task TestProxyDelay(string name)
    {
        var result = await ClashApiManager.Instance.TestDelay(name, _clashItem);
        var model = new SpeedTestResult
        {
            IndexId = name,
            Delay = result.ToString(),
        };
        await ProxiesDelayTestResult(model);
    }

    private async Task TestGroupProxiesDelay()
    {
        var groupProxy = TryGetProxy(SelectedGroup.Name);
        if (!Global.allowSelectType.Contains(groupProxy?.type))
        {
            return;
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
        };
        await Parallel.ForEachAsync(groupProxy?.all ?? [], options, async (name, _) =>
        {
            await TestProxyDelay(name);
        });
    }

    public async Task ProxiesDelayTestResult(SpeedTestResult result)
    {
        var detail = ProxyDetails.FirstOrDefault(it => it.Name == result.IndexId);
        if (detail == null)
        {
            return;
        }
        detail.Delay = Convert.ToInt32(result.Delay);
        detail.DelayName = $"{detail.Delay}ms";
        await Task.CompletedTask;
    }

    #endregion proxy function
}
