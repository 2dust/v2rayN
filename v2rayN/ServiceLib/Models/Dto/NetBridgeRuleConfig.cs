namespace ServiceLib.Models.Dto;

public sealed class NetBridgeRuleConfig
{
    public uint RuleId { get; set; }
    public string ProcessName { get; set; }
    public string TargetHosts { get; set; }
    public string TargetPorts { get; set; }
    public string Protocol { get; set; }
    public string Action { get; set; }
    public uint ProxyConfigId { get; set; }
}
