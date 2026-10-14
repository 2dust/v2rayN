namespace ServiceLib.Models.Entities;

[Serializable]
public class ProfileExItem
{
    [PrimaryKey]
    public string IndexId { get; set; }

    public int Delay { get; set; }
    public decimal Speed { get; set; }
    public int Sort { get; set; }
    public string? Message { get; set; }
    public string? IpInfo { get; set; }

    /// <summary>
    /// Unix timestamp (seconds) of last real traffic.
    /// 0 = never used.
    /// </summary>
    public long LastUsed { get; set; }

    /// <summary>
    /// Number of sessions that actually transferred traffic.
    /// </summary>
    public int UseCount { get; set; }
}
