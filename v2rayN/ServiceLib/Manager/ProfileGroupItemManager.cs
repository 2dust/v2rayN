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
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileGroupItem where IndexId not in ( select indexId from ProfileItem )");

        var list = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().ToListAsync();
        _items = new ConcurrentDictionary<string, ProfileGroupItem>(list.Where(t => !string.IsNullOrEmpty(t.IndexId)).ToDictionary(t => t.IndexId!));
    }

    private ProfileGroupItem AddProfileGroupItem(string indexId)
    {
        var profileGroupItem = new ProfileGroupItem()
        {
            IndexId = indexId,
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
            var existsMap = lstExists.Where(t => !string.IsNullOrEmpty(t.IndexId)).ToDictionary(t => t.IndexId!);

            var lstInserts = new List<ProfileGroupItem>();
            var lstUpdates = new List<ProfileGroupItem>();

            foreach (var item in _items.Values)
            {
                if (string.IsNullOrEmpty(item.IndexId))
                {
                    continue;
                }

                if (existsMap.ContainsKey(item.IndexId))
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

        if (string.IsNullOrWhiteSpace(item.IndexId))
        {
            throw new ArgumentException("IndexId required", nameof(item));
        }

        _items[item.IndexId] = item;

        try
        {
            var lst = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().Where(t => t.IndexId == item.IndexId).ToListAsync();
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
        {
            return false;
        }

        if (stack.Contains(indexId))
        {
            return true;
        }

        if (visited.Contains(indexId))
        {
            return false;
        }

        visited.Add(indexId);
        stack.Add(indexId);

        try
        {
            Instance.TryGet(indexId, out var groupItem);

            if (groupItem == null || groupItem.ChildItems.IsNullOrEmpty())
            {
                return false;
            }

            var childIds = Utils.String2List(groupItem.ChildItems)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            if (childIds == null)
            {
                return false;
            }

            foreach (var child in childIds)
            {
                if (HasCycle(child, visited, stack))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            stack.Remove(indexId);
        }
    }

    public static async Task<(List<ProfileItem> Items, ProfileGroupItem? Group)> GetChildProfileItems(string? indexId)
    {
        Instance.TryGet(indexId, out var profileGroupItem);
        if (profileGroupItem == null || profileGroupItem.NotHasChild())
        {
            return (new List<ProfileItem>(), profileGroupItem);
        }
        var items = await GetChildProfileItems(profileGroupItem);
        var subItems = await GetSubChildProfileItems(profileGroupItem);
        items.AddRange(subItems);

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

    public static async Task<List<ProfileItem>> GetSubChildProfileItems(ProfileGroupItem? group)
    {
        if (group == null || group.SubChildItems.IsNullOrEmpty())
        {
            return new();
        }
        var childProfiles = await AppManager.Instance.ProfileItems(group.SubChildItems);

        return childProfiles.Where(p =>
                p != null &&
                p.IsValid() &&
                !p.ConfigType.IsComplexType() &&
                (group.Filter.IsNullOrEmpty() || Regex.IsMatch(p.Remarks, group.Filter))
            )
            .ToList();
    }

    public static async Task<HashSet<string>> GetAllChildDomainAddresses(string indexId)
    {
        // include grand children
        var childAddresses = new HashSet<string>();
        if (!Instance.TryGet(indexId, out var groupItem) || groupItem == null)
        {
            return childAddresses;
        }

        if (groupItem.SubChildItems.IsNotEmpty())
        {
            var subItems = await GetSubChildProfileItems(groupItem);
            subItems.ForEach(p => childAddresses.Add(p.Address));
        }

        var childIds = Utils.String2List(groupItem.ChildItems) ?? [];

        foreach (var childId in childIds)
        {
            var childNode = await AppManager.Instance.GetProfileItem(childId);
            if (childNode == null)
            {
                continue;
            }

            if (!childNode.IsComplex())
            {
                childAddresses.Add(childNode.Address);
            }
            else if (childNode.ConfigType.IsGroupType())
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

    public static async Task<HashSet<string>> GetAllChildEchQuerySni(string indexId)
    {
        // include grand children
        var childAddresses = new HashSet<string>();
        if (!Instance.TryGet(indexId, out var groupItem) || groupItem == null)
        {
            return childAddresses;
        }

        if (groupItem.SubChildItems.IsNotEmpty())
        {
            var subItems = await GetSubChildProfileItems(groupItem);
            foreach (var childNode in subItems)
            {
                if (childNode.EchConfigList.IsNullOrEmpty())
                {
                    continue;
                }
                if (childNode.StreamSecurity == Global.StreamSecurity
                    && childNode.EchConfigList?.Contains("://") == true)
                {
                    var idx = childNode.EchConfigList.IndexOf('+');
                    childAddresses.Add(idx > 0 ? childNode.EchConfigList[..idx] : childNode.Sni);
                }
                else
                {
                    childAddresses.Add(childNode.Sni);
                }
            }
        }

        var childIds = Utils.String2List(groupItem.ChildItems) ?? [];

        foreach (var childId in childIds)
        {
            var childNode = await AppManager.Instance.GetProfileItem(childId);
            if (childNode == null)
            {
                continue;
            }

            if (!childNode.IsComplex() && !childNode.EchConfigList.IsNullOrEmpty())
            {
                if (childNode.StreamSecurity == Global.StreamSecurity
                    && childNode.EchConfigList?.Contains("://") == true)
                {
                    var idx = childNode.EchConfigList.IndexOf('+');
                    childAddresses.Add(idx > 0 ? childNode.EchConfigList[..idx] : childNode.Sni);
                }
                else
                {
                    childAddresses.Add(childNode.Sni);
                }
            }
            else if (childNode.ConfigType.IsGroupType())
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
