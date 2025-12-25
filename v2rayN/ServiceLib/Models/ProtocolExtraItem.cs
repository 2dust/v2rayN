namespace ServiceLib.Models;

public class ProtocolExtraItem
{
    // vmess
    public string? AlterId { get; set; }

    // vless
    public string? Flow { get; set; }
    //public string? VisionSeed { get; set; }

    // shadowsocks
    //public string? PluginArgs { get; set; }

    // hysteria2
    public string? UpMbps { get; set; }
    public string? DownMbps { get; set; }
    public string? Ports { get; set; }
    public string? HopInterval { get; set; }

    // group profile
    public string? GroupType { get; set; }
    public string? ChildItems { get; set; }
    public string? SubChildItems { get; set; }
    public string? Filter { get; set; }
    public EMultipleLoad? MultipleLoad { get; set; }
}
