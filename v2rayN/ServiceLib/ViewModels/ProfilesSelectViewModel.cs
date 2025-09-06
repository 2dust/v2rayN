using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace ServiceLib.ViewModels;
public class ProfilesSelectViewModel : MyReactiveObject
{
    #region private prop

    private List<ProfileItem> _lstProfile;
    private string _serverFilter = string.Empty;
    private Dictionary<string, bool> _dicHeaderSort = new();
    private string _subIndexId = string.Empty;

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
    public string ServerFilter { get; set; }

    #endregion ObservableCollection

    #region Init

    public ProfilesSelectViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        _subIndexId = _config.SubIndexId ?? string.Empty;
        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(
            x => x.SelectedSub,
            y => y != null && !y.Remarks.IsNullOrEmpty() && _subIndexId != y.Id)
                .Subscribe(async c => await SubSelectedChangedAsync(c));

        this.WhenAnyValue(
          x => x.ServerFilter,
          y => y != null && _serverFilter != y)
              .Subscribe(async c => await ServerFilterChanged(c));

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.ProfilesRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshServersBiz());

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(UpdateStatistics);

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        SelectedProfile = new();
        SelectedSub = new();

        await RefreshSubscriptions();
        await RefreshServers();
    }

    #endregion Init

    #region Actions

    public void UpdateStatistics(ServerSpeedItem update)
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

                //if (SelectedProfile?.IndexId == item.IndexId)
                //{
                //    var temp = JsonUtils.DeepCopy(item);
                //    _profileItems.Replace(item, temp);
                //    SelectedProfile = temp;
                //}
                //else
                //{
                //    _profileItems.Replace(item, JsonUtils.DeepCopy(item));
                //}
            }
        }
        catch
        {
        }
    }

    public bool CanOk()
    {
        return SelectedProfile != null && !SelectedProfile.IndexId.IsNullOrEmpty();
    }

    public bool SelectFinish()
    {
        if (!CanOk())
        {
            return false;
        }
        _updateView?.Invoke(EViewAction.CloseWindow, null);
        return true;
    }
    #endregion Actions

    #region Servers && Groups

    private async Task SubSelectedChangedAsync(bool c)
    {
        if (!c)
        {
            return;
        }
        _subIndexId = SelectedSub?.Id;

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
        await RefreshServersBiz();
    }

    private async Task RefreshServersBiz()
    {
        var lstModel = await GetProfileItemsEx(_subIndexId, _serverFilter);
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

    public async Task RefreshSubscriptions()
    {
        SubItems.Clear();

        SubItems.Add(new SubItem { Remarks = ResUI.AllGroupServers });

        foreach (var item in await AppManager.Instance.SubItems())
        {
            SubItems.Add(item);
        }
        if (_subIndexId != null && SubItems.FirstOrDefault(t => t.Id == _subIndexId) != null)
        {
            SelectedSub = SubItems.FirstOrDefault(t => t.Id == _subIndexId);
        }
        else
        {
            SelectedSub = SubItems.First();
        }
    }

    private async Task<List<ProfileItemModel>?> GetProfileItemsEx(string subid, string filter)
    {
        var lstModel = await AppManager.Instance.ProfileItems(_subIndexId, filter);

        //await ConfigHandler.SetDefaultServer(_config, lstModel);

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

    public async Task<ProfileItem?> GetProfileItem()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return null;
        }
        var indexId = SelectedProfile.IndexId;
        var item = await AppManager.Instance.GetProfileItem(indexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return null;
        }
        return item;
    }

    public async Task SortServer(string colName)
    {
        //if (colName.IsNullOrEmpty())
        //{
        //    return;
        //}

        //_dicHeaderSort.TryAdd(colName, true);
        //_dicHeaderSort.TryGetValue(colName, out bool asc);
        //if (await ConfigHandler.SortServers(_config, _config.SubIndexId, colName, asc) != 0)
        //{
        //    return;
        //}
        //_dicHeaderSort[colName] = !asc;
        //await RefreshServers();
    }

    #endregion Servers && Groups
}
