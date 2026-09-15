namespace ServiceLib.Models.Dto;

[Serializable]
public partial class ClashProxyModel : ReactiveObject
{
    public required string Name { get; set; }

    public required string Type { get; set; }

    public string? Now { get; set; }

    [Reactive] public partial int Delay { get; set; }

    [Reactive] public partial string? DelayName { get; set; }

    /// <summary>
    /// Real-time speed text for the active proxy (e.g. "1.2MB/s"). Only the active
    /// node has a value; all others are null/empty because sing-box's /traffic
    /// endpoint reports aggregate traffic for the whole proxy chain, not per outbound.
    /// </summary>
    [Reactive] public partial string? SpeedName { get; set; }

    public bool IsActive { get; set; }
}
