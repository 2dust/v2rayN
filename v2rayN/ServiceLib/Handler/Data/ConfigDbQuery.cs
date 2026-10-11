namespace ServiceLib.Handler.Data;

public class ConfigDbQuery
{
    public static Task<List<SubItem>?> SubItems()
    {
        return SQLiteHelper.Instance.FetchOrderedAsync<SubItem>(t => t.Sort);
    }

    public static Task<SubItem?> GetSubItem(string? subid)
    {
        return SQLiteHelper.Instance.FirstOrDefaultAsync<SubItem>(t => t.Id == subid);
    }

    public static Task<List<ProfileItem>?> ProfileItems(string subid)
    {
        if (subid.IsNullOrEmpty())
        {
            return SQLiteHelper.Instance.FetchAllAsync<ProfileItem>();
        }
        else
        {
            return SQLiteHelper.Instance.FetchPagedAsync<ProfileItem>(t => t.Subid == subid);
        }
    }

    public static async Task<List<string>?> ProfileItemIndexes(string subid)
    {
        return (await ProfileItems(subid))?.Select(t => t.IndexId)?.ToList();
    }

    public static async Task<List<ProfileItemModel>?> ProfileModels(string subid, string filter)
    {
        return await SQLiteHelper.Instance.FetchProfileModelsAsync(subid, filter);
    }

    public static Task<ProfileItem?> GetProfileItem(string indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return Task.FromResult<ProfileItem?>(null);
        }
        return SQLiteHelper.Instance.FirstOrDefaultAsync<ProfileItem>(it => it.IndexId == indexId);
    }

    public static async Task<List<ProfileItem>> GetProfileItemsByIndexIds(IEnumerable<string> indexIds)
    {
        var ids = indexIds.Where(id => !id.IsNullOrEmpty()).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        if (ids.Count <= Global.SqliteMaxBatchSize)
        {
            return await SQLiteHelper.Instance.FetchAsync<ProfileItem>(it => ids.Contains(it.IndexId));
        }

        var items = new List<ProfileItem>();
        for (var size = 0; size < ids.Count; size += Global.SqliteMaxBatchSize)
        {
            var chunk = ids.Skip(size).Take(Global.SqliteMaxBatchSize).ToList();
            var chunkItems = await SQLiteHelper.Instance.FetchAsync<ProfileItem>(it => chunk.Contains(it.IndexId));

            items.AddRange(chunkItems);
        }

        return items;
    }

    public static async Task<Dictionary<string, ProfileItem>> GetProfileItemsByIndexIdsAsMap(IEnumerable<string> indexIds)
    {
        var items = await GetProfileItemsByIndexIds(indexIds);
        return items.ToDictionary(it => it.IndexId);
    }

    public static async Task<List<ProfileItem>> GetProfileItemsOrderedByIndexIds(IEnumerable<string> indexIds)
    {
        var ids = indexIds.Where(id => !id.IsNullOrEmpty()).Distinct().ToList();
        var items = await GetProfileItemsByIndexIds(ids);
        var itemMap = items.ToDictionary(it => it.IndexId);

        return ids.Select(itemMap.GetValueOrDefault)
            .Where(item => item != null)
            .ToList();
    }

    public static Task<ProfileItem?> GetProfileItemViaRemarks(string? remarks)
    {
        if (remarks.IsNullOrEmpty())
        {
            return Task.FromResult<ProfileItem?>(null);
        }
        return SQLiteHelper.Instance.FirstOrDefaultAsync<ProfileItem>(it => it.Remarks == remarks);
    }

    public static Task<List<RoutingItem>?> RoutingItems()
    {
        return SQLiteHelper.Instance.FetchOrderedAsync<RoutingItem>(t => t.Sort);
    }

    public static Task<RoutingItem?> GetRoutingItem(string id)
    {
        return SQLiteHelper.Instance.FirstOrDefaultAsync<RoutingItem>(it => it.Id == id);
    }

    public static Task<List<DNSItem>?> DNSItems()
    {
        return SQLiteHelper.Instance.FetchAllAsync<DNSItem>();
    }

    public static Task<DNSItem?> GetDNSItem(ECoreType eCoreType)
    {
        return SQLiteHelper.Instance.FirstOrDefaultAsync<DNSItem>(it => it.CoreType == eCoreType);
    }

    public static Task<List<FullConfigTemplateItem>?> FullConfigTemplateItem()
    {
        return SQLiteHelper.Instance.FetchAllAsync<FullConfigTemplateItem>();
    }

    public static Task<FullConfigTemplateItem?> GetFullConfigTemplateItem(ECoreType eCoreType)
    {
        return SQLiteHelper.Instance.FirstOrDefaultAsync<FullConfigTemplateItem>(it => it.CoreType == eCoreType);
    }
}
