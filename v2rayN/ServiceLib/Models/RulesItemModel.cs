namespace ServiceLib.Models;

[Serializable]
public class RulesItemModel : RulesItem
{
    public string InboundTags { get; set; }
    public string Ips { get; set; }
    public string Domains { get; set; }
    public string Protocols { get; set; }
    public string RuleTypeName { get; set; }
}
