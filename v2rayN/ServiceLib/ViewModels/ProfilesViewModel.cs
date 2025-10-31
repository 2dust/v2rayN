namespace ServiceLib.ViewModels;

public class ProfilesViewModel : MyReactiveObject
{
    #region private prop

    private List<ProfileItem> _lstProfile;
    private string _serverFilter = string.Empty;
    private Dictionary<string, bool> _dicHeaderSort = new();
    private SpeedtestService? _speedtestService;

    #endregion private prop

    #region ObservableCollection

    public IObservableCollection<ProfileItemModel> ProfileItems { get; } = new ObservableCollectionExtended<ProfileItemModel>();

    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    [Reactive]
    public ProfileItemModel SelectedProfile { get; set; }

    public IList<ProfileItemModel> SelectedProfiles { get; set; }

    [Reactive]
    public SubItem SelectedSub { get; set; }

    [Reactive]
    public SubItem SelectedMoveToGroup { get; set; }

    [Reactive]
    public string ServerFilter { get; set; }

    #endregion ObservableCollection

    #region Menu

    //servers delete
    public ReactiveCommand<Unit, Unit> EditServerCmd { get; }

    public ReactiveCommand<Unit, Unit> RemoveServerCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoveDuplicateServerCmd { get; }
    public ReactiveCommand<Unit, Unit> CopyServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SetDefaultServerCmd { get; }
    public ReactiveCommand<Unit, Unit> ShareServerCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayRandomCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayRoundRobinCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayLeastPingCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayLeastLoadCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayFallbackCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerSingBoxLeastPingCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerSingBoxFallbackCmd { get; }

    //servers move
    public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }

    public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; }

    //servers ping
    public ReactiveCommand<Unit, Unit> MixedTestServerCmd { get; }

    public ReactiveCommand<Unit, Unit> TcpingServerCmd { get; }
    public ReactiveCommand<Unit, Unit> RealPingServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SpeedServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SortServerResultCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoveInvalidServerResultCmd { get; }
    public ReactiveCommand<Unit, Unit> FastRealPingCmd { get; }

    //servers export
    public ReactiveCommand<Unit, Unit> Export2ClientConfigCmd { get; }

    public ReactiveCommand<Unit, Unit> Export2ClientConfigClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> Export2ShareUrlCmd { get; }
    public ReactiveCommand<Unit, Unit> Export2ShareUrlBase64Cmd { get; }

    public ReactiveCommand<Unit, Unit> AddSubCmd { get; }
    public ReactiveCommand<Unit, Unit> EditSubCmd { get; }

    #endregion Menu

    #region Init

    public ProfilesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        #region WhenAnyValue && ReactiveCommand

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedProfile,
           selectedSource => selectedSource != null && !selectedSource.IndexId.IsNullOrEmpty());

        this.WhenAnyValue(
            x => x.SelectedSub,
            y => y != null && !y.Remarks.IsNullOrEmpty() && _config.SubIndexId != y.Id)
                .Subscribe(async c => await SubSelectedChangedAsync(c));
        this.WhenAnyValue(
             x => x.SelectedMoveToGroup,
             y => y != null && !y.Remarks.IsNullOrEmpty())
                 .Subscribe(async c => await MoveToGroup(c));

        this.WhenAnyValue(
          x => x.ServerFilter,
          y => y != null && _serverFilter != y)
              .Subscribe(async c => await ServerFilterChanged(c));

        //servers delete
        EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditServerAsync(EConfigType.Custom);
        }, canEditRemove);
        RemoveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveServerAsync();
        }, canEditRemove);
        RemoveDuplicateServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveDuplicateServer();
        });
        CopyServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyServer();
        }, canEditRemove);
        SetDefaultServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetDefaultServer();
        }, canEditRemove);
        ShareServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareServerAsync();
        }, canEditRemove);
        GenGroupMultipleServerXrayRandomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.Random);
        }, canEditRemove);
        GenGroupMultipleServerXrayRoundRobinCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.RoundRobin);
        }, canEditRemove);
        GenGroupMultipleServerXrayLeastPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.LeastPing);
        }, canEditRemove);
        GenGroupMultipleServerXrayLeastLoadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.LeastLoad);
        }, canEditRemove);
        GenGroupMultipleServerXrayFallbackCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.Fallback);
        }, canEditRemove);
        GenGroupMultipleServerSingBoxLeastPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.sing_box, EMultipleLoad.LeastPing);
        }, canEditRemove);
        GenGroupMultipleServerSingBoxFallbackCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.sing_box, EMultipleLoad.Fallback);
        }, canEditRemove);

        //servers move
        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Top);
        }, canEditRemove);
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Up);
        }, canEditRemove);
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Down);
        }, canEditRemove);
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Bottom);
        }, canEditRemove);

        //servers ping
        FastRealPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.FastRealping);
        });
        MixedTestServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Mixedtest);
        });
        TcpingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Tcping);
        }, canEditRemove);
        RealPingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Realping);
        }, canEditRemove);
        SpeedServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Speedtest);
        }, canEditRemove);
        SortServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SortServer(EServerColName.DelayVal.ToString());
        });
        RemoveInvalidServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveInvalidServerResult();
        });
        //servers export
        Export2ClientConfigCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(false);
        }, canEditRemove);
        Export2ClientConfigClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(true);
        }, canEditRemove);
        Export2ShareUrlCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(false);
        }, canEditRemove);
        Export2ShareUrlBase64Cmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(true);
        }, canEditRemove);

        //Subscription
        AddSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        EditSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.ProfilesRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshServersBiz());

        AppEvents.SubscriptionsRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshSubscriptions());

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async result => await UpdateStatistics(result));

        AppEvents.SetDefaultServerRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async indexId => await SetDefaultServer(indexId));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        SelectedProfile = new();
        SelectedSub = new();
        SelectedMoveToGroup = new();

        await RefreshSubscriptions();
        //await RefreshServers();
    }

    #endregion Init

    #region Actions

    private void Reload()
    {
        AppEvents.ReloadRequested.Publish();
    }

    public async Task SetSpeedTestResult(SpeedTestResult result)
    {
        if (result.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.SendMessageEx(result.Delay);
            NoticeManager.Instance.Enqueue(result.Delay);
            return;
        }
        var item = ProfileItems.FirstOrDefault(it => it.IndexId == result.IndexId);
        if (item == null)
        {
            return;
        }

        if (result.Delay.IsNotEmpty())
        {
            int.TryParse(result.Delay, out var temp);
            item.Delay = temp;
            item.DelayVal = result.Delay ?? string.Empty;
        }
        if (result.Speed.IsNotEmpty())
        {
            item.SpeedVal = result.Speed ?? string.Empty;
        }
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!_config.GuiItem.EnableStatistics
            || (update.ProxyUp + update.ProxyDown) <= 0
            || DateTime.Now.Second % 3 != 0)
        {
            return;
        }

        try
        {
            var item = ProfileItems.FirstOrDefault(it => it.IndexId == update.IndexId);
            if (item != null)
            {
                item.TodayDown = Utils.HumanFy(update.TodayDown);
                item.TodayUp = Utils.HumanFy(update.TodayUp);
                item.TotalDown = Utils.HumanFy(update.TotalDown);
                item.TotalUp = Utils.HumanFy(update.TotalUp);
            }
        }
        catch
        {
        }
    }

    #endregion Actions

    #region Servers && Groups

    private async Task SubSelectedChangedAsync(bool c)
    {
        if (!c)
        {
            return;
        }
        _config.SubIndexId = SelectedSub?.Id;

        await RefreshServers();

        await _updateView?.Invoke(EViewAction.ProfilesFocus, null);
    }

    private async Task ServerFilterChanged(bool c)
    {
        if (!c)
        {
            return;
        }
        _serverFilter = ServerFilter;
        if (_serverFilter.IsNullOrEmpty())
        {
            await RefreshServers();
        }
    }

    public async Task RefreshServers()
    {
        AppEvents.ProfilesRefreshRequested.Publish();

        await Task.Delay(200);
    }

    private async Task RefreshServersBiz()
    {
        var lstModel = await GetProfileItemsEx(_config.SubIndexId, _serverFilter);
        _lstProfile = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(lstModel)) ?? [];

        ProfileItems.Clear();
        ProfileItems.AddRange(lstModel);
        if (lstModel.Count > 0)
        {
            var selected = lstModel.FirstOrDefault(t => t.IndexId == _config.IndexId);
            if (selected != null)
            {
                SelectedProfile = selected;
            }
            else
            {
                SelectedProfile = lstModel.First();
            }
        }

        await _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null);
    }

    private async Task RefreshSubscriptions()
    {
        SubItems.Clear();

        SubItems.Add(new SubItem { Remarks = ResUI.AllGroupServers });

        foreach (var item in await AppManager.Instance.SubItems())
        {
            SubItems.Add(item);
        }
        if (_config.SubIndexId != null && SubItems.FirstOrDefault(t => t.Id == _config.SubIndexId) != null)
        {
            SelectedSub = SubItems.FirstOrDefault(t => t.Id == _config.SubIndexId);
        }
        else
        {
            SelectedSub = SubItems.First();
        }
    }

    private async Task<List<ProfileItemModel>?> GetProfileItemsEx(string subid, string filter)
    {
        var lstModel = await AppManager.Instance.ProfileItems(_config.SubIndexId, filter);

        await ConfigHandler.SetDefaultServer(_config, lstModel);

        var lstServerStat = (_config.GuiItem.EnableStatistics ? StatisticsManager.Instance.ServerStat : null) ?? [];
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        lstModel = (from t in lstModel
                    join t2 in lstServerStat on t.IndexId equals t2.IndexId into t2b
                    from t22 in t2b.DefaultIfEmpty()
                    join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                    from t33 in t3b.DefaultIfEmpty()
                    select new ProfileItemModel
                    {
                        IndexId = t.IndexId,
                        ConfigType = t.ConfigType,
                        Remarks = t.Remarks,
                        Address = t.Address,
                        Port = t.Port,
                        Security = t.Security,
                        Network = t.Network,
                        StreamSecurity = t.StreamSecurity,
                        Subid = t.Subid,
                        SubRemarks = t.SubRemarks,
                        IsActive = t.IndexId == _config.IndexId,
                        Sort = t33?.Sort ?? 0,
                        Delay = t33?.Delay ?? 0,
                        Speed = t33?.Speed ?? 0,
                        DelayVal = t33?.Delay != 0 ? $"{t33?.Delay}" : string.Empty,
                        SpeedVal = t33?.Speed > 0 ? $"{t33?.Speed}" : t33?.Message ?? string.Empty,
                        TodayDown = t22 == null ? "" : Utils.HumanFy(t22.TodayDown),
                        TodayUp = t22 == null ? "" : Utils.HumanFy(t22.TodayUp),
                        TotalDown = t22 == null ? "" : Utils.HumanFy(t22.TotalDown),
                        TotalUp = t22 == null ? "" : Utils.HumanFy(t22.TotalUp)
                    }).OrderBy(t => t.Sort).ToList();

        return lstModel;
    }

    #endregion Servers && Groups

    #region Add Servers

    private async Task<List<ProfileItem>?> GetProfileItems(bool latest)
    {
        var lstSelected = new List<ProfileItem>();
        if (SelectedProfiles == null || SelectedProfiles.Count <= 0)
        {
            return null;
        }

        var orderProfiles = SelectedProfiles?.OrderBy(t => t.Sort);
        if (latest)
        {
            foreach (var profile in orderProfiles)
            {
                var item = await AppManager.Instance.GetProfileItem(profile.IndexId);
                if (item is not null)
                {
                    lstSelected.Add(item);
                }
            }
        }
        else
        {
            lstSelected = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(orderProfiles));
        }

        return lstSelected;
    }

    public async Task EditServerAsync(EConfigType eConfigType)
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        eConfigType = item.ConfigType;

        bool? ret = false;
        if (eConfigType == EConfigType.Custom)
        {
            ret = await _updateView?.Invoke(EViewAction.AddServer2Window, item);
        }
        else if (eConfigType.IsGroupType())
        {
            ret = await _updateView?.Invoke(EViewAction.AddGroupServerWindow, item);
        }
        else
        {
            ret = await _updateView?.Invoke(EViewAction.AddServerWindow, item);
        }
        if (ret == true)
        {
            await RefreshServers();
            if (item.IndexId == _config.IndexId)
            {
                Reload();
            }
        }
    }

    public async Task RemoveServerAsync()
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }
        if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
        {
            return;
        }
        var exists = lstSelected.Exists(t => t.IndexId == _config.IndexId);

        await ConfigHandler.RemoveServers(_config, lstSelected);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        if (lstSelected.Count == ProfileItems.Count)
        {
            ProfileItems.Clear();
        }
        await RefreshServers();
        if (exists)
        {
            Reload();
        }
    }

    private async Task RemoveDuplicateServer()
    {
        var tuple = await ConfigHandler.DedupServerList(_config, _config.SubIndexId);
        if (tuple.Item1 > 0 || tuple.Item2 > 0)
        {
            await RefreshServers();
            Reload();
        }
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
    }

    private async Task CopyServer()
    {
        var lstSelected = await GetProfileItems(false);
        if (lstSelected == null)
        {
            return;
        }
        if (await ConfigHandler.CopyServer(_config, lstSelected) == 0)
        {
            await RefreshServers();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    public async Task SetDefaultServer()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        await SetDefaultServer(SelectedProfile.IndexId);
    }

    private async Task SetDefaultServer(string? indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return;
        }
        if (indexId == _config.IndexId)
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(indexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        if (await ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
        {
            await RefreshServers();
            Reload();
        }
    }

    public async Task ShareServerAsync()
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var url = FmtHandler.GetShareUri(item);
        if (url.IsNullOrEmpty())
        {
            return;
        }

        await _updateView?.Invoke(EViewAction.ShareServer, url);
    }

    private async Task GenGroupMultipleServer(ECoreType coreType, EMultipleLoad multipleLoad)
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        var ret = await ConfigHandler.AddGroupServer4Multiple(_config, lstSelected, coreType, multipleLoad, SelectedSub?.Id);
        if (ret.Success != true)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        if (ret?.Data?.ToString() == _config.IndexId)
        {
            await RefreshServers();
            Reload();
        }
        else
        {
            await SetDefaultServer(ret?.Data?.ToString());
        }
    }

    public async Task SortServer(string colName)
    {
        if (colName.IsNullOrEmpty())
        {
            return;
        }

        _dicHeaderSort.TryAdd(colName, true);
        _dicHeaderSort.TryGetValue(colName, out var asc);
        if (await ConfigHandler.SortServers(_config, _config.SubIndexId, colName, asc) != 0)
        {
            return;
        }
        _dicHeaderSort[colName] = !asc;
        await RefreshServers();
    }

    public async Task RemoveInvalidServerResult()
    {
        var count = await ConfigHandler.RemoveInvalidServerResult(_config, _config.SubIndexId);
        await RefreshServers();
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
    }

    //move server
    private async Task MoveToGroup(bool c)
    {
        if (!c)
        {
            return;
        }

        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        await ConfigHandler.MoveToGroup(_config, lstSelected, SelectedMoveToGroup.Id);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);

        await RefreshServers();
        SelectedMoveToGroup = null;
        SelectedMoveToGroup = new();
    }

    public async Task MoveServer(EMove eMove)
    {
        var item = _lstProfile.FirstOrDefault(t => t.IndexId == SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        var index = _lstProfile.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        if (await ConfigHandler.MoveServer(_config, _lstProfile, index, eMove) == 0)
        {
            await RefreshServers();
        }
    }

    public async Task MoveServerTo(int startIndex, ProfileItemModel targetItem)
    {
        var targetIndex = ProfileItems.IndexOf(targetItem);
        if (startIndex >= 0 && targetIndex >= 0 && startIndex != targetIndex)
        {
            if (await ConfigHandler.MoveServer(_config, _lstProfile, startIndex, EMove.Position, targetIndex) == 0)
            {
                await RefreshServers();
            }
        }
    }

    public async Task ServerSpeedtest(ESpeedActionType actionType)
    {
        if (actionType == ESpeedActionType.Mixedtest)
        {
            SelectedProfiles = ProfileItems;
        }
        else if (actionType == ESpeedActionType.FastRealping)
        {
            SelectedProfiles = ProfileItems;
            actionType = ESpeedActionType.Realping;
        }

        var lstSelected = await GetProfileItems(false);
        if (lstSelected == null)
        {
            return;
        }

        _speedtestService ??= new SpeedtestService(_config, async (SpeedTestResult result) =>
        {
            RxApp.MainThreadScheduler.Schedule(result, (scheduler, result) =>
            {
                _ = SetSpeedTestResult(result);
                return Disposable.Empty;
            });
        });
        _speedtestService?.RunLoop(actionType, lstSelected);
    }

    public void ServerSpeedtestStop()
    {
        _speedtestService?.ExitLoop();
    }

    private async Task Export2ClientConfigAsync(bool blClipboard)
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        var msgs = await ActionPrecheckManager.Instance.Check(item);
        if (msgs.Count > 0)
        {
            foreach (var msg in msgs)
            {
                NoticeManager.Instance.SendMessage(msg);
            }
            NoticeManager.Instance.Enqueue(Utils.List2String(msgs.Take(10).ToList(), true));
            return;
        }

        if (blClipboard)
        {
            var result = await CoreConfigHandler.GenerateClientConfig(item, null);
            if (result.Success != true)
            {
                NoticeManager.Instance.Enqueue(result.Msg);
            }
            else
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, result.Data);
                NoticeManager.Instance.SendMessage(ResUI.OperationSuccess);
            }
        }
        else
        {
            await _updateView?.Invoke(EViewAction.SaveFileDialog, item);
        }
    }

    public async Task Export2ClientConfigResult(string fileName, ProfileItem item)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        var result = await CoreConfigHandler.GenerateClientConfig(item, fileName);
        if (result.Success != true)
        {
            NoticeManager.Instance.Enqueue(result.Msg);
        }
        else
        {
            NoticeManager.Instance.SendMessageAndEnqueue(string.Format(ResUI.SaveClientConfigurationIn, fileName));
        }
    }

    public async Task Export2ShareUrlAsync(bool blEncode)
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        StringBuilder sb = new();
        foreach (var it in lstSelected)
        {
            var url = FmtHandler.GetShareUri(it);
            if (url.IsNullOrEmpty())
            {
                continue;
            }
            sb.Append(url);
            sb.AppendLine();
        }
        if (sb.Length > 0)
        {
            if (blEncode)
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, Utils.Base64Encode(sb.ToString()));
            }
            else
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
            }
            NoticeManager.Instance.SendMessage(ResUI.BatchExportURLSuccessfully);
        }
    }

    #endregion Add Servers

    #region Subscription

    private async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(_config.SubIndexId);
            if (item is null)
            {
                return;
            }
        }
        if (await _updateView?.Invoke(EViewAction.SubEditWindow, item) == true)
        {
            await RefreshSubscriptions();
            await SubSelectedChangedAsync(true);
        }
    }

    #endregion Subscription
}
