namespace ServiceLib.Models;

[Serializable]
public partial class ClashProxyModel : ReactiveObject
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? Now { get; set; }

    [Reactive] public partial int Delay { get; set; }

    [Reactive] public partial string? DelayName { get; set; }

    public bool IsActive { get; set; }
}
