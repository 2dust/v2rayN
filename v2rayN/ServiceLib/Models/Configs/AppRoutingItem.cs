namespace ServiceLib.Models.Configs;

// Keep the persisted values stable when adding choices to the editor.
public enum AppRouteKind
{
    Profile = 0, Socks5 = 1, Interface = 2, ActiveProfile = 3
}

public class AppRoutingItem
{
    public bool Enabled
    {
        get; set;
    }
    public List<AppRouteRule> Rules { get; set; } = [];
}

public class AppRouteRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string ExecutablePath { get; set; } = "";
    public bool MatchByName
    {
        get; set;
    }
    public bool IncludeChildProcesses
    {
        get; set;
    }
    public AppRouteKind Kind
    {
        get; set;
    }
    public string ProfileId { get; set; } = "";
    public bool ApplyBlockingRules { get; set; }
    public string InterfaceId { get; set; } = "";
    public string SocksHost { get; set; } = "127.0.0.1";
    public int SocksPort { get; set; } = 10808;
    public string SocksUsername { get; set; } = "";
    public string SocksPassword { get; set; } = "";
}
