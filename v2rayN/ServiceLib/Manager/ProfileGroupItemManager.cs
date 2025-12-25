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

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileGroupItem where IndexId not in ( select indexId from ProfileItem )");

        var list = await SQLiteHelper.Instance.TableAsync<ProfileGroupItem>().ToListAsync();
        _items = new ConcurrentDictionary<string, ProfileGroupItem>(list.Where(t => !string.IsNullOrEmpty(t.IndexId)).ToDictionary(t => t.IndexId!));
    }

    public async Task ClearAll()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileGroupItem ");
        _items.Clear();
    }
}
