namespace ServiceLib.Models;

[Obsolete("Use ProtocolExtraItem instead.")]
[Serializable]
public class ProfileGroupItem
{
    [PrimaryKey]
    public string IndexId { get; set; }

    public string ChildItems { get; set; }

    public string? SubChildItems { get; set; }

    public string? Filter { get; set; }

    public EMultipleLoad MultipleLoad { get; set; } = EMultipleLoad.LeastPing;

    public bool NotHasChild()
    {
        return string.IsNullOrWhiteSpace(ChildItems) && string.IsNullOrWhiteSpace(SubChildItems);
    }
}
