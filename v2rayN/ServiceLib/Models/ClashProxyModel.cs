namespace ServiceLib.Models;

[Serializable]
public partial class ClashProxyModel : ReactiveObject
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? Now { get; set; }

    [Reactive] private int _delay;

    [Reactive] private string? _delayName;

    public bool IsActive { get; set; }
}
