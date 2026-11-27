namespace ServiceLib.Models.Dto;

public partial class ClashProxyModel : ReactiveObject
{
    public string Name { get; set; }

    public string Type { get; set; }

    public string? Now { get; set; }

    [Reactive] public partial int Delay { get; set; }

    [Reactive] public partial string? DelayName { get; set; }

    public bool IsActive { get; set; }

    public override string ToString() => string.Join(" ", new[] { Name, Type, Now, DelayName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
