namespace ServiceLib.Models.Dto;

[Serializable]
public partial class ClashProxyModel : ReactiveObject
{
    public required string Name { get; set; }

    public required string Type { get; set; }

    public string? Now { get; set; }

    [Reactive] public partial int Delay { get; set; }

    [Reactive] public partial string? DelayName { get; set; }

    public bool IsActive { get; set; }
}
