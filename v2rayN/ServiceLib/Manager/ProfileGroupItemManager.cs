using System.Collections.Concurrent;

namespace ServiceLib.Manager;

public class ProfileGroupItemManager
{
    private static readonly Lazy<ProfileGroupItemManager> _instance = new(() => new());
    private ConcurrentDictionary<string, ProfileGroupItem> _items = new();

    public static ProfileGroupItemManager Instance => _instance.Value;
    private static readonly string _tag = "ProfileGroupItemManager";

    private ProfileGroupItemManager()
    {
    }

    public async Task Init()
    {
        await InitData();
    }

    // Read-only getters: do not create or mark dirty
    public bool TryGet(string indexId, out ProfileGroupItem? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(indexId))
        {
            return false;
        }

        return _items.TryGetValue(indexId, out item);
    }

    public ProfileGroupItem? GetOrDefault(string indexId)
    {
        return string.IsNullOrWhiteSpace(indexId) ? null : (_items.TryGetValue(indexId, out var v) ? v : null);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileGroupItem where parentIndexId not in ( select indexId from ProfileItem )");

        var list = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().ToListAsync();
        _items = new ConcurrentDictionary<string, ProfileGroupItem>(list.Where(t => !string.IsNullOrEmpty(t.ParentIndexId)).ToDictionary(t => t.ParentIndexId!));
    }

    private ProfileGroupItem AddProfileGroupItem(string indexId)
    {
        var profileGroupItem = new ProfileGroupItem()
        {
            ParentIndexId = indexId,
            ChildItems = string.Empty,
            MultipleLoad = EMultipleLoad.LeastPing
        };

        _items[indexId] = profileGroupItem;
        return profileGroupItem;
    }

    private ProfileGroupItem GetProfileGroupItem(string indexId)
    {
        if (string.IsNullOrEmpty(indexId))
        {
            indexId = Utils.GetGuid(false);
        }

        return _items.GetOrAdd(indexId, AddProfileGroupItem);
    }

    public async Task ClearAll()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileGroupItem ");
        _items.Clear();
    }

    public async Task SaveTo()
    {
        try
        {
            var lstExists = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().ToListAsync();
            var existsMap = lstExists.Where(t => !string.IsNullOrEmpty(t.ParentIndexId)).ToDictionary(t => t.ParentIndexId!);

            var lstInserts = new List<ProfileGroupItem>();
            var lstUpdates = new List<ProfileGroupItem>();

            foreach (var item in _items.Values)
            {
                if (string.IsNullOrEmpty(item.ParentIndexId))
                {
                    continue;
                }

                if (existsMap.ContainsKey(item.ParentIndexId))
                {
                    lstUpdates.Add(item);
                }
                else
                {
                    lstInserts.Add(item);
                }
            }

            try
            {
                if (lstInserts.Count > 0)
                {
                    await SQLiteHelper.Instance.InsertAllAsync(lstInserts);
                }

                if (lstUpdates.Count > 0)
                {
                    await SQLiteHelper.Instance.UpdateAllAsync(lstUpdates);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public ProfileGroupItem GetOrCreateAndMarkDirty(string indexId)
    {
        return GetProfileGroupItem(indexId);
    }

    public async ValueTask DisposeAsync()
    {
        await SaveTo();
    }

    public async Task SaveItemAsync(ProfileGroupItem item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.ParentIndexId))
        {
            throw new ArgumentException("ParentIndexId required", nameof(item));
        }

        _items[item.ParentIndexId] = item;

        try
        {
            var lst = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().Where(t => t.ParentIndexId == item.ParentIndexId).ToListAsync();
            if (lst != null && lst.Count > 0)
            {
                await SQLiteHelper.Instance.UpdateAllAsync(new List<ProfileGroupItem> { item });
            }
            else
            {
                await SQLiteHelper.Instance.InsertAllAsync(new List<ProfileGroupItem> { item });
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
