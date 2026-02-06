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

    private static async Task<List<ProfileItem>> GetChildProfileItemsByProtocolExtra(ProtocolExtraItem? protocolExtra)
    {
        if (protocolExtra == null)
        {
            return new();
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
            return new();
        }
        var childProfiles = (await Task.WhenAll(
                (Utils.String2List(extra.ChildItems) ?? new())
                    .Where(p => !p.IsNullOrEmpty())
                    .Select(AppManager.Instance.GetProfileItem)
            ))
            .Where(p =>
                p != null &&
                p.IsValid() &&
                p.ConfigType != EConfigType.Custom
            )
            .ToList();
        return childProfiles ?? new();
    }

    private static async Task<List<ProfileItem>> GetSubChildProfileItems(ProtocolExtraItem? extra)
    {
        if (extra == null || extra.SubChildItems.IsNullOrEmpty())
        {
            return new();
        }
        var childProfiles = await AppManager.Instance.ProfileItems(extra.SubChildItems ?? string.Empty);

        return childProfiles?.Where(p =>
                p != null &&
                p.IsValid() &&
                !p.ConfigType.IsComplexType() &&
                (extra.Filter.IsNullOrEmpty() || Regex.IsMatch(p.Remarks, extra.Filter))
            )
            .ToList() ?? new();
    }

    public static async Task<HashSet<string>> GetAllChildDomainAddresses(ProfileItem profileItem)
    {
        var childAddresses = new HashSet<string>();
        var (childItems, _) = await GetChildProfileItems(profileItem);
        foreach (var child in childItems)
        {
            if (!child.IsComplex())
            {
                childAddresses.Add(child.Address);
            }
            else if (child.ConfigType.IsGroupType())
            {
                var subAddresses = await GetAllChildDomainAddresses(child);
                foreach (var addr in subAddresses)
                {
                    childAddresses.Add(addr);
                }
            }
        }
        return childAddresses;
    }

    public static async Task<HashSet<string>> GetAllChildEchQuerySni(ProfileItem profileItem)
    {
        var childAddresses = new HashSet<string>();
        var (childItems, _) = await GetChildProfileItems(profileItem);
        foreach (var childNode in childItems)
        {
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
                var subAddresses = await GetAllChildDomainAddresses(childNode);
                foreach (var addr in subAddresses)
                {
                    childAddresses.Add(addr);
                }
            }
        }
        return childAddresses;
    }
}
