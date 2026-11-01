namespace ServiceLib.Models.Entities;

public class RoutingItem : NotifyPropertyChangedBase
{
    [PrimaryKey]
    public string Id { get; set => SetField(ref field, value); }

    public string Remarks { get; set => SetField(ref field, value); }
    public string Url { get; set => SetField(ref field, value); }
    public string RuleSet { get; set => SetField(ref field, value); }
    public int RuleNum { get; set => SetField(ref field, value); }
    public bool Enabled { get; set => SetField(ref field, value); } = true;
    public bool Locked { get; set => SetField(ref field, value); }
    public string CustomIcon { get; set => SetField(ref field, value); }
    public string CustomRulesetPath4Singbox { get; set => SetField(ref field, value); }
    public string DomainStrategy { get; set => SetField(ref field, value); }
    public string DomainStrategy4Singbox { get; set => SetField(ref field, value); }
    public int Sort { get; set => SetField(ref field, value); }
    public bool IsActive { get; set => SetField(ref field, value); }
}
