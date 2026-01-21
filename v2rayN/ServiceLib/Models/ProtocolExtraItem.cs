namespace ServiceLib.Models;

public record ProtocolExtraItem
{
    // vmess
    public string? AlterId { get; init; }
    public string? VmessSecurity { get; init; }

    // vless
    public string? Flow { get; init; }
    public string? VlessEncryption { get; init; }
    //public string? VisionSeed { get; init; }

    // shadowsocks
    //public string? PluginArgs { get; init; }
    public string? SsMethod { get; init; }

    // socks and http
    public string? Username { get; init; }

    // wireguard
    public string? WgPublicKey { get; init; }
    public string? WgPresharedKey { get; init; }
    public string? WgInterfaceAddress { get; init; }
    public string? WgReserved { get; init; }
    public int? WgMtu { get; init; }

    // hysteria2
    public int? UpMbps { get; init; }
    public int? DownMbps { get; init; }
    public string? Ports { get; init; }
    public int? HopInterval { get; init; }

    // group profile
    public string? GroupType { get; init; }
    public string? ChildItems { get; init; }
    public string? SubChildItems { get; init; }
    public string? Filter { get; init; }
    public EMultipleLoad? MultipleLoad { get; init; }
}
