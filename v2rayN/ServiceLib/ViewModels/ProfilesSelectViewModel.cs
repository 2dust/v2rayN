using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class ProfilesSelectViewModel : MyReactiveObject
{
    #region private prop

    private string _serverFilter = string.Empty;
    private Dictionary<string, bool> _dicHeaderSort = new();
    private string _subIndexId = string.Empty;

    // ConfigType filter state: default include-mode with all types selected
    private List<EConfigType> _filterConfigTypes = new();

    private bool _filterExclude = false;

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

    // Include/Exclude filter for ConfigType
    public List<EConfigType> FilterConfigTypes
    {
        get => _filterConfigTypes;
        set => this.RaiseAndSetIfChanged(ref _filterConfigTypes, value);
    }

    [Reactive]
    public bool FilterExclude
    {
        get => _filterExclude;
        set => this.RaiseAndSetIfChanged(ref _filterExclude, value);
    }

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

        // React to ConfigType filter changes
        this.WhenAnyValue(x => x.FilterExclude)
            .Skip(1)
            .Subscribe(async _ => await RefreshServersBiz());

        this.WhenAnyValue(x => x.FilterConfigTypes)
            .Skip(1)
            .Subscribe(async _ => await RefreshServersBiz());

        #endregion WhenAnyValue && ReactiveCommand

        _ = Init();
    }

    private async Task Init()
    {
        SelectedProfile = new();
        SelectedSub = new();

        // Default: include mode with all ConfigTypes selected
        try
        {
            FilterExclude = false;
            FilterConfigTypes = Enum.GetValues(typeof(EConfigType)).Cast<EConfigType>().ToList();
        }
        catch
        {
            FilterConfigTypes = new();
        }

        await RefreshSubscriptions();
        await RefreshServers();
    }

    #endregion Init

    #region Actions

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
        lstModel = (from t in lstModel
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
                    }).OrderBy(t => t.Sort).ToList();

        // Apply ConfigType filter (include or exclude)
        if (FilterConfigTypes != null && FilterConfigTypes.Count > 0)
        {
            if (FilterExclude)
            {
                lstModel = lstModel.Where(t => !FilterConfigTypes.Contains(t.ConfigType)).ToList();
            }
            else
            {
                lstModel = lstModel.Where(t => FilterConfigTypes.Contains(t.ConfigType)).ToList();
            }
        }

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

    public async Task<List<ProfileItem>?> GetProfileItems()
    {
        if (SelectedProfiles == null || SelectedProfiles.Count == 0)
        {
            return null;
        }
        var lst = new List<ProfileItem>();
        foreach (var sp in SelectedProfiles)
        {
            if (string.IsNullOrEmpty(sp?.IndexId))
            {
                continue;
            }
            var item = await AppManager.Instance.GetProfileItem(sp.IndexId);
            if (item != null)
            {
                lst.Add(item);
            }
        }
        if (lst.Count == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return null;
        }
        return lst;
    }

    public void SortServer(string colName)
    {
        if (colName.IsNullOrEmpty())
        {
            return;
        }

        var prop = typeof(ProfileItemModel).GetProperty(colName);
        if (prop == null)
        {
            return;
        }

        _dicHeaderSort.TryAdd(colName, true);
        var asc = _dicHeaderSort[colName];

        var comparer = Comparer<object?>.Create((a, b) =>
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }
            if (a is null)
            {
                return -1;
            }
            if (b is null)
            {
                return 1;
            }
            if (a.GetType() == b.GetType() && a is IComparable ca)
            {
                return ca.CompareTo(b);
            }
            return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        });

        object? KeySelector(ProfileItemModel x)
        {
            return prop.GetValue(x);
        }

        IEnumerable<ProfileItemModel> sorted = asc
            ? ProfileItems.OrderBy(KeySelector, comparer)
            : ProfileItems.OrderByDescending(KeySelector, comparer);

        var list = sorted.ToList();
        ProfileItems.Clear();
        ProfileItems.AddRange(list);

        _dicHeaderSort[colName] = !asc;

        return;
    }

    #endregion Servers && Groups

    #region Public API

    // External setter for ConfigType filter
    public void SetConfigTypeFilter(IEnumerable<EConfigType> types, bool exclude = false)
    {
        FilterConfigTypes = types?.Distinct().ToList() ?? new List<EConfigType>();
        FilterExclude = exclude;
    }

    #endregion Public API
}
