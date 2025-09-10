using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.Models;

[Serializable]
public class ClashProxyModel : ReactiveObject
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? Now { get; set; }

    [Reactive] public int Delay { get; set; }

    [Reactive] public string? DelayName { get; set; }

    public bool IsActive { get; set; }
}
