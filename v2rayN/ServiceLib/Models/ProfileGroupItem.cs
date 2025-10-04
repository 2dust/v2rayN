using SQLite;

namespace ServiceLib.Models;

[Serializable]
public class ProfileGroupItem
{
    [PrimaryKey]
    public string ParentIndexId { get; set; }

    public string ChildItems { get; set; }

    public EMultipleLoad MultipleLoad { get; set; } = EMultipleLoad.LeastPing;
}
