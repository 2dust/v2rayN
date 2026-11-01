namespace ServiceLib.Models.Entities;

public class SubItem : NotifyPropertyChangedBase
{
    [PrimaryKey]
    public string Id { get; set => SetField(ref field, value); }

    public string Remarks { get; set => SetField(ref field, value); }

    public string Url { get; set => SetField(ref field, value); }

    public string MoreUrl { get; set => SetField(ref field, value); }

    public bool Enabled { get; set => SetField(ref field, value); } = true;

    public string UserAgent { get; set => SetField(ref field, value); } = string.Empty;

    public string? RequestHeaders { get; set => SetField(ref field, value); }

    public int Sort { get; set => SetField(ref field, value); }

    public string? Filter { get; set => SetField(ref field, value); }

    public int AutoUpdateInterval { get; set => SetField(ref field, value); }

    public long UpdateTime { get; set => SetField(ref field, value); }

    public string? ConvertTarget { get; set => SetField(ref field, value); }

    public string? PrevProfile { get; set => SetField(ref field, value); }

    public string? NextProfile { get; set => SetField(ref field, value); }

    public int? PreSocksPort { get; set => SetField(ref field, value); }

    public string? Memo { get; set => SetField(ref field, value); }

    public ECoreType? CustomCoreType { get; set => SetField(ref field, value); }
}
