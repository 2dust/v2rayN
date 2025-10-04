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

    #region Helper

    public static bool HasCycle(string? indexId)
    {
        return HasCycle(indexId, new HashSet<string>(), new HashSet<string>());
    }

    public static bool HasCycle(string? indexId, HashSet<string> visited, HashSet<string> stack)
    {
        if (indexId.IsNullOrEmpty())
            return false;

        if (stack.Contains(indexId))
            return true;

        if (visited.Contains(indexId))
            return false;

        visited.Add(indexId);
        stack.Add(indexId);

        Instance.TryGet(indexId, out var groupItem);

        if (groupItem == null || groupItem.ChildItems.IsNullOrEmpty())
        {
            return false;
        }

        var childIds = Utils.String2List(groupItem.ChildItems)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        foreach (var child in childIds)
        {
            if (HasCycle(child, visited, stack))
            {
                return true;
            }
        }

        stack.Remove(indexId);
        return false;
    }

    public static async Task<(List<ProfileItem> Items, ProfileGroupItem? Group)> GetChildProfileItems(string? indexId)
    {
        Instance.TryGet(indexId, out var profileGroupItem);
        if (profileGroupItem == null || profileGroupItem.ChildItems.IsNullOrEmpty())
        {
            return (new List<ProfileItem>(), profileGroupItem);
        }
        var items = await GetChildProfileItems(profileGroupItem);
        return (items, profileGroupItem);
    }

    public static async Task<List<ProfileItem>> GetChildProfileItems(ProfileGroupItem? group)
    {
        if (group == null || group.ChildItems.IsNullOrEmpty())
        {
            return new();
        }
        var childProfiles = (await Task.WhenAll(
                Utils.String2List(group.ChildItems)
                    .Where(p => !p.IsNullOrEmpty())
                    .Select(AppManager.Instance.GetProfileItem)
            ))
            .Where(p =>
                p != null &&
                p.IsValid() &&
                p.ConfigType != EConfigType.Custom
            )
            .ToList();
        return childProfiles;
    }

    public static async Task<HashSet<string>> GetAllChildDomainAddresses(string parentIndexId)
    {
        // include grand children
        var childAddresses = new HashSet<string>();
        if (!Instance.TryGet(parentIndexId, out var groupItem) || groupItem.ChildItems.IsNullOrEmpty())
            return childAddresses;

        var childIds = Utils.String2List(groupItem.ChildItems);

        foreach (var childId in childIds)
        {
            var childNode = await AppManager.Instance.GetProfileItem(childId);
            if (childNode == null)
                continue;

            if (!childNode.IsComplex())
            {
                childAddresses.Add(childNode.Address);
            }
            else if (childNode.ConfigType > EConfigType.Group)
            {
                var subAddresses = await GetAllChildDomainAddresses(childNode.IndexId);
                foreach (var addr in subAddresses)
                {
                    childAddresses.Add(addr);
                }
            }
        }

        return childAddresses;
    }

    #endregion Helper
}
