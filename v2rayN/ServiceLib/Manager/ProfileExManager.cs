namespace ServiceLib.Manager;

public class ProfileExManager
{
    private static readonly Lazy<ProfileExManager> _instance = new(() => new());
    private ConcurrentDictionary<string, ProfileExItem> _dicProfileEx = new();
    private readonly ConcurrentQueue<string> _queIndexIds = new();
    private readonly Lock _profileExLock = new();
    private int _maxSort;
    public static ProfileExManager Instance => _instance.Value;
    private static readonly string _tag = "ProfileExHandler";

    public ProfileExManager()
    {
        //Init();
    }

    public async Task Init()
    {
        await InitData();
    }

    public async Task<ICollection<ProfileExItem>> GetProfileExs()
    {
        return await Task.FromResult(_dicProfileEx.Values);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

        var dicProfileEx = new ConcurrentDictionary<string, ProfileExItem>();
        foreach (var item in await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync())
        {
            dicProfileEx[item.IndexId] = item;
        }
        lock (_profileExLock)
        {
            _dicProfileEx = dicProfileEx;
            _maxSort = dicProfileEx.Values.Select(t => t.Sort).DefaultIfEmpty(0).Max();
        }
    }

    private void IndexIdEnqueue(string indexId)
    {
        if (indexId.IsNotEmpty())
        {
            //Speed tests touch this from many threads; the save loop deduplicates IDs.
            _queIndexIds.Enqueue(indexId);
        }
    }

    private async Task SaveQueueIndexIds()
    {
        var lstIds = new HashSet<string>();
        while (_queIndexIds.TryDequeue(out var id))
        {
            lstIds.Add(id);
        }

        if (lstIds.Count > 0)
        {
            List<ProfileExItem> lstSaves = [];

            foreach (var id in lstIds)
            {
                if (!_dicProfileEx.TryGetValue(id, out var itemNew))
                {
                    continue;
                }

                lstSaves.Add(itemNew);
            }

            try
            {
                if (lstSaves.Count > 0)
                {
                    //Another save may have inserted these rows since the queue was drained.
                    await SQLiteHelper.Instance.ReplaceAllAsync(lstSaves);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private ProfileExItem AddProfileEx(string indexId)
    {
        var profileEx = new ProfileExItem()
        {
            IndexId = indexId,
            Delay = 0,
            Speed = 0,
            Sort = 0,
            Message = string.Empty
        };
        _dicProfileEx[indexId] = profileEx;
        IndexIdEnqueue(indexId);
        return profileEx;
    }

    private ProfileExItem GetProfileExItem(string? indexId)
    {
        indexId ??= string.Empty;
        if (_dicProfileEx.TryGetValue(indexId, out var profileEx))
        {
            return profileEx;
        }

        lock (_profileExLock)
        {
            return _dicProfileEx.TryGetValue(indexId, out profileEx) ? profileEx : AddProfileEx(indexId);
        }
    }

    public async Task ClearAll()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem ");
        lock (_profileExLock)
        {
            _dicProfileEx = new();
            _maxSort = 0;
        }
    }

    public async Task SaveTo()
    {
        try
        {
            await SaveQueueIndexIds();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public void SetTestDelay(string indexId, int delay)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Delay = delay;
        IndexIdEnqueue(indexId);
    }

    public void SetTestSpeed(string indexId, decimal speed)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Speed = speed;
        IndexIdEnqueue(indexId);
    }

    public void SetTestMessage(string indexId, string message)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Message = message;
        IndexIdEnqueue(indexId);
    }

    public void SetTestIpInfo(string indexId, string ipInfo)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.IpInfo = ipInfo;
        IndexIdEnqueue(indexId);
    }

    public void SetSort(string indexId, int sort)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Sort = sort;
        IndexIdEnqueue(indexId);
        lock (_profileExLock)
        {
            _maxSort = Math.Max(_maxSort, sort);
        }
    }

    public int GetSort(string indexId)
    {
        return _dicProfileEx.TryGetValue(indexId, out var profileEx) ? profileEx.Sort : 0;
    }

    public int GetMaxSort()
    {
        return _maxSort;
    }
}
