namespace ServiceLib.Models.Dto;

public class RulesItemModel : RulesItem
{
    public string InboundTags { get; set; }
    public string Ips { get; set; }
    public string Domains { get; set; }
    public string Protocols { get; set; }
    public string RuleTypeName { get; set; }

    public override string ToString()
    {
        var parts = new[] { Remarks, RuleTypeName, OutboundTag, Port, Protocols, InboundTags, Network, Domains, Ips }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(", ", parts);
    }
}
