namespace ServiceLib.Models.Entities;

public record ProtocolExtraItem
{
    public bool? Uot { get; init; }
    public string? CongestionControl { get; init; }

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

    // wireguard
    public string? WgPublicKey { get; init; }
    public string? WgPresharedKey { get; init; }
    public string? WgInterfaceAddress { get; init; }
    public string? WgReserved { get; init; }
    public int? WgMtu { get; init; }

    // hysteria2
    public string? SalamanderPass { get; init; }
    public int? UpMbps { get; init; }
    public int? DownMbps { get; init; }
    public string? Ports { get; init; }
    public string? HopInterval { get; init; }
    // realm://<token>@<rendezvous-host>[:port]/<realm-name>?stun=<stun-host>[:port]&stun=<stun-host>[:port]...
    // example:
    // realm://public@realm.hy2.io/57f9be7c-2810-4f5b-8cb9-260bc84d6c90?stun=example.stun:3478&stun=example2.stun:3478
    public string? Hy2RealmUrl { get; init; }

    // naiveproxy
    public int? InsecureConcurrency { get; init; }
    public bool? NaiveQuic { get; init; }

    // group profile
    public string? GroupType { get; init; }
    public string? ChildItems { get; init; }
    public string? SubChildItems { get; init; }
    public string? Filter { get; init; }
    public EMultipleLoad? MultipleLoad { get; init; }
}
