namespace ServiceLib.ViewModels;

public partial class ProfilesSelectViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    public Interaction<RxVoid, RxVoid> ProfilesFocusInteraction { get; } = new();

    #region private prop

    private string _serverFilter = string.Empty;
    private readonly Dictionary<string, bool> _dicHeaderSort = new();
    private string _subIndexId = string.Empty;

    // ConfigType filter state: default include-mode with all types selected

    #endregion private prop

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    #region ObservableCollection

    public BulkObservableCollection<ProfileItemModel> ProfileItems { get; } = [];

    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    [Reactive]
    public partial ProfileItemModel SelectedProfile { get; set; }

    public IList<ProfileItemModel> SelectedProfiles { get; set; }

    [Reactive]
    public partial SubItem SelectedSub { get; set; }

    [Reactive]
    public partial string ServerFilter { get; set; }

    // Include/Exclude filter for ConfigType
    [Reactive]
    public partial List<EConfigType> FilterConfigTypes { get; set; }

    [Reactive]
    public partial bool FilterExclude { get; set; }

    [Reactive]
    public partial bool MultiSelect { get; set; }

    #endregion ObservableCollection

    #region Init

    public ProfilesSelectViewModel()
    {
        _config = AppManager.Instance.Config;
        _subIndexId = _config.SubIndexId ?? string.Empty;

        #region WhenAnyValue && ReactiveCommand

        SaveCmd = ReactiveCommand.Create(() =>
        {
            SelectFinish();
        });

        this.WhenAnyValue(x => x.SelectedSub)
            .Where(y => y != null && !y.Remarks.IsNullOrEmpty() && _subIndexId != y.Id)
            .SubscribeAsync(async _ => await SubSelectedChangedAsync());

        this.WhenAnyValue(x => x.ServerFilter)
            .Where(y => y != null && _serverFilter != y)
            .SubscribeAsync(async _ => await ServerFilterChanged());

        // React to ConfigType filter changes
        this.WhenAnyValue(x => x.FilterExclude)
            .Skip(1)
            .SubscribeAsync(async _ => await RefreshServersBiz());

        this.WhenAnyValue(x => x.FilterConfigTypes)
            .Skip(1)
            .SubscribeAsync(async _ => await RefreshServersBiz());

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
            FilterConfigTypes = Enum.GetValues<EConfigType>().ToList();
        }
        catch
        {
            FilterConfigTypes = [];
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
        RequestClose?.Invoke(this, EventArgs.Empty);
        return true;
    }

    #endregion Actions

    #region Servers && Groups

    private async Task SubSelectedChangedAsync()
    {
        _subIndexId = SelectedSub?.Id;

        await RefreshServers();

        await ProfilesFocusInteraction.HandleSafe(RxVoid.Default);
    }

    private async Task ServerFilterChanged()
    {
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

        ProfileItems.ReplaceRange(lstModel);
        if (lstModel.Count > 0)
        {
            var selected = lstModel.FirstOrDefault(t => t.IndexId == _config.IndexId);
            SelectedProfile = selected ?? lstModel.First();
        }
    }

    private async Task RefreshSubscriptions()
    {
        var subItems = await AppManager.Instance.SubItems();
        subItems.Insert(0, new SubItem { Remarks = ResUI.AllGroupServers });

        SubItems.ReplaceRange(subItems);

        SelectedSub = (_config.SubIndexId.IsNotEmpty()
                        ? subItems.FirstOrDefault(t => t.Id == _config.SubIndexId)
                        : null) ?? subItems.FirstOrDefault();
    }

    private async Task<List<ProfileItemModel>?> GetProfileItemsEx(string subid, string filter)
    {
        var lstModel = await AppManager.Instance.ProfileModels(_subIndexId, filter);
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        lstModel = (from t in lstModel
                    join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                    from t33 in t3b.DefaultIfEmpty()
                    select new ProfileItemModel
                    {
                        IndexId = t.IndexId,
                        ConfigType = t.ConfigType,
                        Remarks = t.Remarks,
                        Address = t.Address,
                        Port = t.Port,
                        //Security = t.Security,
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
                        IpInfo = t33?.IpInfo ?? string.Empty,
                    }).OrderBy(t => t.Sort).ToList();

        // Apply ConfigType filter (include or exclude)
        if (FilterConfigTypes is { Count: > 0 })
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
        var lst = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(SelectedProfiles.Select(sp => sp?.IndexId));
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
        ProfileItems.ReplaceRange(list);

        _dicHeaderSort[colName] = !asc;

        return;
    }

    #endregion Servers && Groups

    #region Public API

    // External setter for ConfigType filter
    public void SetConfigTypeFilter(IEnumerable<EConfigType> types, bool exclude = false)
    {
        FilterConfigTypes = types?.Distinct().ToList() ?? [];
        FilterExclude = exclude;
    }

    #endregion Public API
}
