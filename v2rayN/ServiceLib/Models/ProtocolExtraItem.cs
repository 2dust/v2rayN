namespace ServiceLib.Models;

public class ProtocolExtraItem
{
    // vmess
    public string? AlterId { get; set; }
    public string? VmessSecurity { get; set; }

    // vless
    public string? Flow { get; set; }
    public string? VlessEncryption { get; set; }
    //public string? VisionSeed { get; set; }

    // shadowsocks
    //public string? PluginArgs { get; set; }
    public string? SsMethod { get; set; }

    // socks and http
    public string? Username { get; set; }

    // wireguard
    public string? WgPublicKey { get; set; }
    public string? WgPresharedKey { get; set; }
    public string? WgInterfaceAddress { get; set; }
    public string? WgReserved { get; set; }
    public int? WgMtu { get; set; }

    // hysteria2
    public int? UpMbps { get; set; }
    public int? DownMbps { get; set; }
    public string? Ports { get; set; }
    public int? HopInterval { get; set; }

    // group profile
    public string? GroupType { get; set; }
    public string? ChildItems { get; set; }
    public string? SubChildItems { get; set; }
    public string? Filter { get; set; }
    public EMultipleLoad? MultipleLoad { get; set; }
}
