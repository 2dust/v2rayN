namespace ServiceLib.Manager;

public class GroupProfileManager
{
    public static async Task<bool> HasCycle(ProfileItem item)
    {
        return await HasCycle(item.IndexId, item.GetProtocolExtra());
    }

    public static async Task<bool> HasCycle(string? indexId, ProtocolExtraItem? extraInfo)
    {
        return await HasCycle(indexId, extraInfo, new HashSet<string>(), new HashSet<string>());
    }

    private static async Task<bool> HasCycle(string? indexId, ProtocolExtraItem? extraInfo, HashSet<string> visited, HashSet<string> stack)
    {
        if (indexId.IsNullOrEmpty() || extraInfo == null)
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
            if (extraInfo.GroupType.IsNullOrEmpty())
            {
                return false;
            }

            if (extraInfo.ChildItems.IsNullOrEmpty())
            {
                return false;
            }

            var childIds = Utils.String2List(extraInfo.ChildItems)
                ?.Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            if (childIds == null)
            {
                return false;
            }

            foreach (var child in childIds)
            {
                var childItem = await AppManager.Instance.GetProfileItem(child);
                if (await HasCycle(child, childItem?.GetProtocolExtra(), visited, stack))
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

    public static async Task<(List<ProfileItem> Items, ProtocolExtraItem? Extra)> GetChildProfileItems(ProfileItem profileItem)
    {
        var protocolExtra = profileItem?.GetProtocolExtra();
        return (await GetChildProfileItemsByProtocolExtra(protocolExtra), protocolExtra);
    }

    public static async Task<List<ProfileItem>> GetChildProfileItemsByProtocolExtra(ProtocolExtraItem? protocolExtra)
    {
        if (protocolExtra == null)
        {
            return [];
        }

        var items = new List<ProfileItem>();
        items.AddRange(await GetSubChildProfileItems(protocolExtra));
        items.AddRange(await GetSelectedChildProfileItems(protocolExtra));

        return items;
    }

    private static async Task<List<ProfileItem>> GetSelectedChildProfileItems(ProtocolExtraItem? extra)
    {
        if (extra == null || extra.ChildItems.IsNullOrEmpty())
        {
            return [];
        }
        var childProfileIds = Utils.String2List(extra.ChildItems)
            ?.Where(p => !string.IsNullOrEmpty(p))
            .ToList() ?? [];
        if (childProfileIds.Count == 0)
        {
            return [];
        }

        var profileMap = await AppManager.Instance.GetProfileItemsByIndexIdsAsMap(childProfileIds);

        var ordered = new List<ProfileItem>(childProfileIds.Count);
        foreach (var id in childProfileIds)
        {
            if (id != null && profileMap.TryGetValue(id, out var item) && item != null)
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    private static async Task<List<ProfileItem>> GetSubChildProfileItems(ProtocolExtraItem? extra)
    {
        if (extra == null || extra.SubChildItems.IsNullOrEmpty())
        {
            return [];
        }
        var childProfiles = await AppManager.Instance.ProfileItems(extra.SubChildItems ?? string.Empty);

        return childProfiles?.Where(p =>
                p != null &&
                p.IsValid() &&
                !p.ConfigType.IsComplexType() &&
                (extra.Filter.IsNullOrEmpty() || Regex.IsMatch(p.Remarks, extra.Filter))
            )
            .ToList() ?? [];
    }

    public static async Task<Dictionary<string, ProfileItem>> GetAllChildProfileItems(ProfileItem profileItem)
    {
        var itemMap = new Dictionary<string, ProfileItem>();
        var visited = new HashSet<string>();

        await CollectChildItems(profileItem, itemMap, visited);

        return itemMap;
    }

    private static async Task CollectChildItems(ProfileItem profileItem, Dictionary<string, ProfileItem> itemMap,
        HashSet<string> visited)
    {
        var (childItems, _) = await GetChildProfileItems(profileItem);
        foreach (var child in childItems.Where(child => visited.Add(child.IndexId)))
        {
            itemMap[child.IndexId] = child;

            if (child.ConfigType.IsGroupType())
            {
                await CollectChildItems(child, itemMap, visited);
            }
        }
    }
}
