namespace ServiceLib.Models.Editors;

public partial class RoutingEditorModel : ReactiveObject
{
    private readonly RoutingItem _snapshot;

    private RoutingEditorModel(RoutingItem snapshot)
    {
        _snapshot = snapshot;
        Remarks = snapshot.Remarks;
        DomainStrategy = snapshot.DomainStrategy;
        DomainStrategy4Singbox = snapshot.DomainStrategy4Singbox;
        Url = snapshot.Url;
        CustomIcon = snapshot.CustomIcon;
        CustomRulesetPath4Singbox = snapshot.CustomRulesetPath4Singbox;
        Sort = snapshot.Sort;
    }

    [Reactive]
    public partial string Remarks { get; set; }

    [Reactive]
    public partial string DomainStrategy { get; set; }

    [Reactive]
    public partial string DomainStrategy4Singbox { get; set; }

    [Reactive]
    public partial string Url { get; set; }

    [Reactive]
    public partial string CustomIcon { get; set; }

    [Reactive]
    public partial string CustomRulesetPath4Singbox { get; set; }

    [Reactive]
    public partial int Sort { get; set; }

    public static RoutingEditorModel FromEntity(RoutingItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var snapshot = JsonUtils.DeepCopy(entity)
            ?? throw new InvalidOperationException("Could not copy RoutingItem for editing.");
        return new RoutingEditorModel(snapshot);
    }

    public RoutingItem ToEntity()
    {
        var entity = JsonUtils.DeepCopy(_snapshot)
            ?? throw new InvalidOperationException("Could not copy RoutingItem for saving.");
        entity.Remarks = Remarks;
        entity.DomainStrategy = DomainStrategy;
        entity.DomainStrategy4Singbox = DomainStrategy4Singbox;
        entity.Url = Url;
        entity.CustomIcon = CustomIcon;
        entity.CustomRulesetPath4Singbox = CustomRulesetPath4Singbox;
        entity.Sort = Sort;
        return entity;
    }
}
