namespace ServiceLib.ViewModels;

public partial class ClashProxiesViewModel : MyReactiveObject
{
    private readonly int _delayTimeout = 99999999;
    private ClashItem _clashItem = new();

    // Per-tag cumulative traffic snapshot from /connections, used to compute speed
    // deltas between polls. Key = outbound tag (e.g. "proxy-1-日本节点").
    private Dictionary<string, ulong> _lastConnTraffic = [];
    private DateTime _lastConnTime = DateTime.MinValue;

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
        ProxyRemoveFromGroupCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveProxyFromGroup();
        });
        ProxyAddChildCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddProxyToGroup();
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

        #region AppEvents

        // sing-box /traffic reports aggregate proxy speed (ProxyUp/ProxyDown in KB/s).
        // Only the active node can display a speed; all others stay empty.
        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async update => await UpdateProxySpeed(update));

        #endregion AppEvents

        _ = Task.Factory.StartNew(
            async () => await GetClashProxiesTask(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

        // Background poller: call /connections every 2s, compute per-tag speed deltas.
        // This covers non-active nodes that /traffic cannot split. The active node
        // also receives speed from DispatcherStatisticsRequested (1 Hz, more precise);
        // both sources update the same SpeedName property.
        _ = Task.Factory.StartNew(
            async () => await GetConnectionSpeedTask(),
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
    public ReactiveCommand<RxVoid, RxVoid> ProxyRemoveFromGroupCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ProxyAddChildCmd { get; }

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

    /// <summary>
    /// Background poller that calls the Clash /connections endpoint every 2 seconds,
    /// groups each connection's cumulative upload+download by the outbound tags in its
    /// <c>chains</c> list, and computes per-tag speed as delta / elapsed. This covers
    /// non-active nodes that the /traffic WebSocket cannot split.
    /// </summary>
    private async Task GetConnectionSpeedTask()
    {
        while (true)
        {
            await Task.Delay(2000);
            if (!AppManager.Instance.ShowInTaskbar
                || !AppManager.Instance.IsRunningCore(ECoreType.sing_box))
            {
                continue;
            }
            try
            {
                await UpdateProxySpeedFromConnections();
            }
            catch
            {
            }
        }
    }

    private async Task UpdateProxySpeedFromConnections()
    {
        var ret = await ClashApiManager.Instance.GetConnections();
        if (ret?.connections == null)
        {
            return;
        }

        // Accumulate cumulative traffic per outbound tag (upload + download).
        // We collect ALL tags from chains, not just "proxy-N-xxx" leaf tags,
        // because sing-box urltest groups report chains as
        //   ["proxy-auto", "proxy"]
        // without the leaf "proxy-N-Remarks" tag.  In that case the only way to
        // attribute traffic is through the group tag ("proxy-auto").
        var current = new Dictionary<string, ulong>();
        foreach (var conn in ret.connections)
        {
            if (conn.chains == null || conn.chains.Count == 0)
            {
                continue;
            }
            var total = conn.upload + conn.download;
            foreach (var tag in conn.chains)
            {
                if (tag.IsNullOrEmpty())
                {
                    continue;
                }
                // Include group-level tags ("proxy", "proxy-auto") and leaf tags
                // ("proxy-1-日本节点").  Exclude system tags ("direct", "block",
                // "dns", "ntp") to avoid noise.
                if (!tag.StartsWith(Global.ProxyTag))
                {
                    continue;
                }
                current.TryGetValue(tag, out var existing);
                current[tag] = existing + total;
            }
        }

        // Build a reverse map: group tag -> the leaf node currently selected by that
        // group (urltest: the fastest node; selector: the manually chosen node).
        // When chains only contain the group tag, we attribute traffic to the
        // selected leaf so the speed shows on the right card.
        var groupToActiveLeaf = new Dictionary<string, string>();
        if (_clashItem.Proxies != null)
        {
            foreach (var kv in _clashItem.Proxies)
            {
                var proxy = kv.Value;
                if (proxy?.all == null || proxy.now.IsNullOrEmpty())
                {
                    continue;
                }
                // proxy.now is the currently selected child tag
                if (proxy.all.Contains(proxy.now))
                {
                    groupToActiveLeaf[kv.Key] = proxy.now;
                }
            }
        }

        var now = DateTime.Now;
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            // Compute speed delta per tag and update the matching ClashProxyModel.
            var hasPrev = _lastConnTime != DateTime.MinValue && _lastConnTraffic.Count > 0;
            var elapsedSec = hasPrev ? (now - _lastConnTime).TotalSeconds : 0;

            // Build a map from leaf detail.Name -> cumulative traffic, resolving
            // group tags to their selected leaf via groupToActiveLeaf.
            var leafTraffic = new Dictionary<string, ulong>();
            foreach (var (tag, cumulative) in current)
            {
                if (leafTraffic.ContainsKey(tag))
                {
                    leafTraffic[tag] += cumulative;
                }
                else
                {
                    leafTraffic[tag] = cumulative;
                }
                // If this tag is a group (urltest/selector), attribute its traffic
                // to the currently selected leaf as well.
                if (groupToActiveLeaf.TryGetValue(tag, out var leaf))
                {
                    leafTraffic.TryGetValue(leaf, out var leafExisting);
                    leafTraffic[leaf] = leafExisting + cumulative;
                }
            }

            foreach (var detail in ProxyDetails)
            {
                if (detail?.Name.IsNullOrEmpty() != false)
                {
                    continue;
                }
                // The active node is handled by the /traffic path (more precise) when
                // DisplayRealTimeSpeed is on — that's the only condition under which
                // UpdateProxySpeed actually sets SpeedName.  Skip it then to avoid
                // format conflicts ("up/s | down/s" from /traffic vs "total/s" from
                // /connections).  When DisplayRealTimeSpeed is off, /traffic never
                // updates the active node, so /connections must include it.
                if (detail.IsActive && _config.GuiItem.DisplayRealTimeSpeed)
                {
                    continue;
                }
                if (!leafTraffic.TryGetValue(detail.Name, out var cumulative) || cumulative == 0)
                {
                    // No traffic attributed to this tag — clear the speed text so
                    // idle nodes don't show stale values.
                    if (hasPrev)
                    {
                        detail.SpeedName = string.Empty;
                    }
                    continue;
                }
                if (hasPrev && elapsedSec > 0
                    && _lastConnTraffic.TryGetValue(detail.Name, out var prevCumulative))
                {
                    var delta = (long)(cumulative - prevCumulative);
                    var speed = (long)(Math.Max(delta, 0) / elapsedSec);
                    detail.SpeedName = speed > 0 ? $"{Utils.HumanFy(speed)}/s" : "0B/s";
                }
                else
                {
                    // First poll: no delta yet, show total traffic so far.
                    detail.SpeedName = Utils.HumanFy((long)cumulative);
                }
            }

            // Store the leaf-resolved traffic so next cycle's delta lookup works.
            _lastConnTraffic = leafTraffic;
            _lastConnTime = now;
        });
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

    /// <summary>
    /// Updates the real-time speed text on the active proxy card. sing-box's
    /// /traffic endpoint gives aggregate ProxyUp/ProxyDown (KB/s) for the whole
    /// tunnel, so only the node marked IsActive gets a value; all others are
    /// cleared.  Called from the DispatcherStatisticsRequested event channel,
    /// which fires roughly once per second when DisplayRealTimeSpeed is on.
    /// </summary>
    private async Task UpdateProxySpeed(ServerSpeedItem update)
    {
        if (!_config.GuiItem.DisplayRealTimeSpeed)
        {
            return;
        }
        var active = ProxyDetails.FirstOrDefault(t => t is { IsActive: true });
        if (active != null)
        {
            active.SpeedName = (update.ProxyUp > 0 || update.ProxyDown > 0)
                ? $"{Utils.HumanFy(update.ProxyUp)}/s | {Utils.HumanFy(update.ProxyDown)}/s"
                : string.Empty;
        }
        await Task.CompletedTask;
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

    /// <summary>
    /// Removes the selected proxy from its policy group. The proxy name in the Clash API
    /// is the sing-box outbound tag (format: "{prefix}-{index}-{Remarks}"), so the Remarks
    /// part is extracted by removing the first two "-"-delimited segments and matching
    /// against ProfileItem.Remarks in the database. The matched item's IndexId is then
    /// removed from the active policy group's ChildItems.
    /// </summary>
    public async Task RemoveProxyFromGroup()
    {
        if (SelectedDetail?.Name.IsNullOrEmpty() != false)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var proxyName = SelectedDetail.Name;

        // Find the matching ProfileItem by checking if Remarks is a suffix of the proxy tag.
        // sing-box outbound tags are formatted as "{prefix}-{index}-{Remarks}", but Remarks
        // itself may contain dashes, so a simple Split would be unreliable. Instead we look
        // for the ProfileItem whose Remarks appears after the second dash in the tag.
        var allItems = await AppManager.Instance.ProfileItems(null);
        ProfileItem? matched = null;
        // Try exact suffix match: find the item whose Remarks equals the part after the 2nd dash
        var dashIndex = proxyName.IndexOf('-');
        if (dashIndex >= 0)
        {
            var secondDash = proxyName.IndexOf('-', dashIndex + 1);
            if (secondDash >= 0)
            {
                var remarks = proxyName[(secondDash + 1)..];
                matched = allItems?.FirstOrDefault(t => t != null && t.Remarks == remarks);
            }
        }
        // Fallback: try Contains match (less precise but catches encoding/whitespace differences)
        matched ??= allItems?.FirstOrDefault(t => t != null && !t.Remarks.IsNullOrEmpty() && proxyName.Contains(t.Remarks));
        if (matched == null || matched.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        // Find all policy group items and check if any has this node in its ChildItems
        var groupItems = allItems?.Where(t => t != null && t.ConfigType.IsGroupType()).ToList() ?? [];
        var groupItem = groupItems.FirstOrDefault(t =>
        {
            var childIds = Utils.String2List(t.GetProtocolExtra().ChildItems);
            return childIds?.Contains(matched.IndexId) == true;
        });
        if (groupItem == null || groupItem.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        var extra = groupItem.GetProtocolExtra();
        var childIds = Utils.String2List(extra.ChildItems) ?? [];
        childIds.Remove(matched.IndexId);
        extra = extra with { ChildItems = Utils.List2String(childIds) };
        groupItem.SetProtocolExtra(extra);
        await ConfigHandler.AddServerCommon(_config, groupItem);

        NoticeManager.Instance.SendMessage(ResUI.OperationSuccess);

        // Remove from the UI model and the cached Clash data without reloading the core:
        // the database is already updated above, and the next core reload will regenerate
        // the sing-box config without this node.
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            // 1. Remove from the currently displayed details list
            var detail = ProxyDetails.FirstOrDefault(t => t?.Name == proxyName);
            if (detail != null)
            {
                ProxyDetails.Remove(detail);
            }
            // 2. Remove the proxy entry itself from the cached Clash data
            if (_clashItem.Proxies.ContainsKey(proxyName))
            {
                _clashItem.Proxies.Remove(proxyName);
            }
            // 3. Remove the name from every group's member list so a later refresh
            //    of this in-memory data won't resurrect it
            foreach (var proxy in _clashItem.Proxies.Values)
            {
                proxy.all?.Remove(proxyName);
            }
        });
    }

    /// <summary>
    /// Opens the profile select dialog (same one used by AddGroupServerViewModel.AddChildAsync)
    /// so the user can pick nodes to add to the current policy group. The selected items'
    /// IndexIds are appended to the group's ChildItems, the database is updated, and the
    /// proxy list is refreshed in-memory without reloading the core.
    /// </summary>
    public async Task AddProxyToGroup()
    {
        // Determine which policy group the currently selected proxy group belongs to
        var groupName = SelectedGroup?.Name;
        if (groupName.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        // Find the policy group that owns the currently displayed proxy list.
        // The SelectedGroup.Name is the sing-box selector tag (e.g. "proxy" or "proxy-auto"),
        // which maps to the active profile's IndexId. We search all group items for one
        // whose ChildItems already contains at least one of the currently displayed proxies.
        var allItems = await AppManager.Instance.ProfileItems(null);
        var groupItems = allItems?.Where(t => t != null && t.ConfigType.IsGroupType()).ToList() ?? [];

        // Match by checking which group's child proxies match the current ProxyDetails
        var currentProxyNames = ProxyDetails.Select(t => t?.Name).Where(n => n.IsNotEmpty()).ToHashSet();
        ProfileItem? groupItem = null;
        foreach (var gi in groupItems)
        {
            var giChildIds = Utils.String2List(gi.GetProtocolExtra().ChildItems) ?? [];
            foreach (var cid in giChildIds)
            {
                var child = allItems?.FirstOrDefault(t => t?.IndexId == cid);
                if (child != null && child.Remarks.IsNotEmpty() && currentProxyNames.Any(n => n!.Contains(child.Remarks)))
                {
                    groupItem = gi;
                    break;
                }
            }
            if (groupItem != null) break;
        }
        // Fallback: if no match found, try the active profile itself
        groupItem ??= groupItems.FirstOrDefault(t => t.IndexId == _config.IndexId);
        if (groupItem == null || groupItem.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        // Open the profile select dialog (multi-select, exclude groups to prevent cycles)
        var profileSelectViewModel = new ProfilesSelectViewModel();
        profileSelectViewModel.SetConfigTypeFilter([EConfigType.Custom, EConfigType.PolicyGroup], exclude: true);
        // Hide nodes that are already in this policy group so the user only sees
        // nodes they can actually add.
        profileSelectViewModel.SetExcludeIndexIds(Utils.String2List(groupItem.GetProtocolExtra().ChildItems));
        profileSelectViewModel.MultiSelect = true;
        var result = await AppManager.Instance.WindowDialog.ShowDialogAsync(profileSelectViewModel);
        if (result != true)
        {
            return;
        }
        var profiles = await profileSelectViewModel.GetProfileItems() ?? [];
        if (profiles.Count == 0)
        {
            return;
        }

        // Append the new child IndexIds (skip duplicates)
        var extra = groupItem.GetProtocolExtra();
        var childIds = Utils.String2List(extra.ChildItems) ?? [];
        var existing = childIds.ToHashSet();
        var added = 0;
        foreach (var p in profiles.Where(t => t != null && t.IndexId.IsNotEmpty()))
        {
            if (existing.Add(p.IndexId))
            {
                childIds.Add(p.IndexId);
                added++;
            }
        }
        if (added == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        extra = extra with { ChildItems = Utils.List2String(childIds) };
        groupItem.SetProtocolExtra(extra);
        await ConfigHandler.AddServerCommon(_config, groupItem);

        NoticeManager.Instance.SendMessage(ResUI.OperationSuccess);

        // Add the new proxies to the UI model and cached Clash data without reloading
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            var newDetails = new List<ClashProxyModel>();
            for (var i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                if (p == null || p.IndexId.IsNullOrEmpty() || p.Remarks.IsNullOrEmpty())
                    continue;
                // Skip if already displayed
                var tag = $"{Global.ProxyTag}-{childIds.IndexOf(p.IndexId) + 1}-{p.Remarks}";
                if (ProxyDetails.Any(t => t?.Name == tag))
                    continue;
                newDetails.Add(new ClashProxyModel
                {
                    Name = tag,
                    Type = p.ConfigType.ToString(),
                    Delay = _delayTimeout,
                    DelayName = string.Empty,
                });
            }
            if (newDetails.Count > 0)
            {
                ProxyDetails.AddRange(newDetails);
            }
        });
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
