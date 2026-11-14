namespace ServiceLib.Handler.Data;

public class ConfigDbQuery
{
    public static async Task<List<SubItem>?> SubItems()
    {
        return await SQLiteHelper.Instance.TableAsync<SubItem>().OrderBy(t => t.Sort).ToListAsync();
    }

    public static async Task<SubItem?> GetSubItem(string? subid)
    {
        return await SQLiteHelper.Instance.TableAsync<SubItem>().FirstOrDefaultAsync(t => t.Id == subid);
    }

    public static async Task<List<ProfileItem>?> ProfileItems(string subid)
    {
        if (subid.IsNullOrEmpty())
        {
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().ToListAsync();
        }
        else
        {
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.Subid == subid).ToListAsync();
        }
    }

    public static async Task<List<string>?> ProfileItemIndexes(string subid)
    {
        return (await ProfileItems(subid))?.Select(t => t.IndexId)?.ToList();
    }

    public static async Task<List<ProfileItemModel>?> ProfileModels(string subid, string filter)
    {
        var sql = @$"select a.IndexId
                           ,a.ConfigType
                           ,a.Remarks
                           ,a.Address
                           ,a.Port
                           ,a.Network
                           ,a.StreamSecurity
                           ,a.Subid
                           ,b.remarks as subRemarks
                        from ProfileItem a
                        left join SubItem b on a.subid = b.id
                        where 1=1 ";
        if (subid.IsNotEmpty())
        {
            sql += $" and a.subid = '{subid}'";
        }
        if (filter.IsNotEmpty())
        {
            if (filter.Contains('\''))
            {
                filter = filter.Replace("'", "");
            }
            sql += string.Format(" and (a.remarks like '%{0}%' or a.address like '%{0}%') ", filter);
        }

        return await SQLiteHelper.Instance.QueryAsync<ProfileItemModel>(sql);
    }

    public static async Task<ProfileItem?> GetProfileItem(string indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return null;
        }
        return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.IndexId == indexId);
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
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>()
                .Where(it => ids.Contains(it.IndexId))
                .ToListAsync();
        }

        var items = new List<ProfileItem>();
        for (var size = 0; size < ids.Count; size += Global.SqliteMaxBatchSize)
        {
            var chunk = ids.Skip(size).Take(Global.SqliteMaxBatchSize).ToList();
            var chunkItems = await SQLiteHelper.Instance.TableAsync<ProfileItem>()
                .Where(it => chunk.Contains(it.IndexId))
                .ToListAsync();

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

    public static async Task<ProfileItem?> GetProfileItemViaRemarks(string? remarks)
    {
        if (remarks.IsNullOrEmpty())
        {
            return null;
        }
        return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.Remarks == remarks);
    }

    public static async Task<List<RoutingItem>?> RoutingItems()
    {
        return await SQLiteHelper.Instance.TableAsync<RoutingItem>().OrderBy(t => t.Sort).ToListAsync();
    }

    public static async Task<RoutingItem?> GetRoutingItem(string id)
    {
        return await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(it => it.Id == id);
    }

    public static async Task<List<DNSItem>?> DNSItems()
    {
        return await SQLiteHelper.Instance.TableAsync<DNSItem>().ToListAsync();
    }

    public static async Task<DNSItem?> GetDNSItem(ECoreType eCoreType)
    {
        return await SQLiteHelper.Instance.TableAsync<DNSItem>().FirstOrDefaultAsync(it => it.CoreType == eCoreType);
    }

    public static async Task<List<FullConfigTemplateItem>?> FullConfigTemplateItem()
    {
        return await SQLiteHelper.Instance.TableAsync<FullConfigTemplateItem>().ToListAsync();
    }

    public static async Task<FullConfigTemplateItem?> GetFullConfigTemplateItem(ECoreType eCoreType)
    {
        return await SQLiteHelper.Instance.TableAsync<FullConfigTemplateItem>().FirstOrDefaultAsync(it => it.CoreType == eCoreType);
    }
}
