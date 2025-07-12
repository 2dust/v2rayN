using SQLite;

namespace ServiceLib.Models;

[Serializable]
public class CustomConfigItem
{
    [PrimaryKey]
    public string Id { get; set; }

    public string Remarks { get; set; }
    public bool Enabled { get; set; } = false;
    public ECoreType CoreType { get; set; }
    public string? Config { get; set; }
    public string? TunConfig { get; set; }
}
