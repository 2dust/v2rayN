namespace v2rayN.Mode;

[Serializable]
public class RulesItemModel : RulesItem
{
    public string inboundTags { get; set; }

    public string ips { get; set; }

    public string domains { get; set; }

    public string protocols { get; set; }
}