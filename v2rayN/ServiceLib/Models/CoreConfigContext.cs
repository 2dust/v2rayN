namespace ServiceLib.Models;

public record CoreConfigContext
{
    public required ProfileItem Node { get; init; }
    public RoutingItem? RoutingItem { get; init; }
    public DNSItem? RawDnsItem { get; init; }
    public SimpleDNSItem SimpleDnsItem { get; init; } = new();
    public Dictionary<string, ProfileItem> AllProxiesMap { get; init; } = new();
    public Config AppConfig { get; init; } = new();
    public FullConfigTemplateItem? FullConfigTemplate { get; init; } = new();

    // TUN Compatibility
    public bool IsTunEnabled { get; init; } = false;
    public HashSet<string> ProtectDomainList { get; init; } = new();
    public int ProtectSocksPort { get; init; } = 0;
}
