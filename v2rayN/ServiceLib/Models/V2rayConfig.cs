namespace ServiceLib.Models;

public class V2rayConfig
{
    public Log4Ray log { get; set; }
    public Dns4Ray dns { get; set; }
    public List<Inbounds4Ray> inbounds { get; set; }
    public List<Outbounds4Ray> outbounds { get; set; }
    public Routing4Ray routing { get; set; }
    public Metrics4Ray? metrics { get; set; }
    public Policy4Ray? policy { get; set; }
    public Stats4Ray? stats { get; set; }
    public Observatory4Ray? observatory { get; set; }
    public BurstObservatory4Ray? burstObservatory { get; set; }
    public string? remarks { get; set; }
}

public class Stats4Ray
{ }

public class Metrics4Ray
{
    public string tag { get; set; }
}

public class Policy4Ray
{
    public SystemPolicy4Ray system { get; set; }
}

public class SystemPolicy4Ray
{
    public bool statsOutboundUplink { get; set; }
    public bool statsOutboundDownlink { get; set; }
}

public class Log4Ray
{
    public string? access { get; set; }

    public string? error { get; set; }

    public string? loglevel { get; set; }
}

public class Inbounds4Ray
{
    public string tag { get; set; }

    public int port { get; set; }

    public string listen { get; set; }

    public string protocol { get; set; }

    public Sniffing4Ray sniffing { get; set; }

    public Inboundsettings4Ray settings { get; set; }
}

public class Inboundsettings4Ray
{
    public string? auth { get; set; }

    public bool? udp { get; set; }

    public string? ip { get; set; }

    public string? address { get; set; }

    public List<UsersItem4Ray>? clients { get; set; }

    public string? decryption { get; set; }

    public bool? allowTransparent { get; set; }

    public List<AccountsItem4Ray>? accounts { get; set; }
}

public class UsersItem4Ray
{
    public string? id { get; set; }

    public int? alterId { get; set; }

    public string? email { get; set; }

    public string? security { get; set; }

    public string? encryption { get; set; }

    public string? flow { get; set; }
}

public class Sniffing4Ray
{
    public bool enabled { get; set; }
    public List<string>? destOverride { get; set; }
    public bool routeOnly { get; set; }
}

public class Outbounds4Ray
{
    public string tag { get; set; }

    public string protocol { get; set; }

    public Outboundsettings4Ray settings { get; set; }

    public StreamSettings4Ray streamSettings { get; set; }

    public Mux4Ray mux { get; set; }
}

public class Outboundsettings4Ray
{
    public List<VnextItem4Ray>? vnext { get; set; }

    public List<ServersItem4Ray>? servers { get; set; }

    public Response4Ray? response { get; set; }

    public string domainStrategy { get; set; }

    public int? userLevel { get; set; }

    public FragmentItem4Ray? fragment { get; set; }

    public string? secretKey { get; set; }

    public List<string>? address { get; set; }

    public List<WireguardPeer4Ray>? peers { get; set; }

    public bool? noKernelTun { get; set; }

    public int? mtu { get; set; }

    public List<int>? reserved { get; set; }

    public int? workers { get; set; }
}

public class WireguardPeer4Ray
{
    public string endpoint { get; set; }
    public string publicKey { get; set; }
}

public class VnextItem4Ray
{
    public string address { get; set; }

    public int port { get; set; }

    public List<UsersItem4Ray> users { get; set; }
}

public class ServersItem4Ray
{
    public string email { get; set; }

    public string address { get; set; }

    public string? method { get; set; }

    public bool? ota { get; set; }

    public string? password { get; set; }

    public int port { get; set; }

    public int? level { get; set; }

    public string flow { get; set; }

    public List<SocksUsersItem4Ray> users { get; set; }
}

public class SocksUsersItem4Ray
{
    public string user { get; set; }

    public string pass { get; set; }

    public int? level { get; set; }
}

public class Mux4Ray
{
    public bool enabled { get; set; }
    public int? concurrency { get; set; }
    public int? xudpConcurrency { get; set; }
    public string? xudpProxyUDP443 { get; set; }
}

public class Response4Ray
{
    public string type { get; set; }
}

public class Dns4Ray
{
    public Dictionary<string, object>? hosts { get; set; }
    public List<object> servers { get; set; }
    public string? clientIp { get; set; }
    public string? queryStrategy { get; set; }
    public bool? disableCache { get; set; }
    public bool? disableFallback { get; set; }
    public bool? disableFallbackIfMatch { get; set; }
    public bool? useSystemHosts { get; set; }
    public string? tag { get; set; }
}

public class DnsServer4Ray
{
    public string? address { get; set; }
    public int? port { get; set; }
    public List<string>? domains { get; set; }
    public bool? skipFallback { get; set; }
    public List<string>? expectedIPs { get; set; }
    public List<string>? unexpectedIPs { get; set; }
    public string? clientIp { get; set; }
    public string? queryStrategy { get; set; }
    public int? timeoutMs { get; set; }
    public bool? disableCache { get; set; }
    public bool? finalQuery { get; set; }
    public string? tag { get; set; }
}

public class Routing4Ray
{
    public string domainStrategy { get; set; }

    public List<RulesItem4Ray> rules { get; set; }

    public List<BalancersItem4Ray>? balancers { get; set; }
}

[Serializable]
public class RulesItem4Ray
{
    public string? type { get; set; }

    public string? port { get; set; }
    public string? network { get; set; }

    public List<string>? inboundTag { get; set; }

    public string? outboundTag { get; set; }

    public string? balancerTag { get; set; }

    public List<string>? ip { get; set; }

    public List<string>? domain { get; set; }

    public List<string>? protocol { get; set; }
}

public class BalancersItem4Ray
{
    public List<string>? selector { get; set; }
    public BalancersStrategy4Ray? strategy { get; set; }
    public string? tag { get; set; }
}

public class BalancersStrategy4Ray
{
    public string? type { get; set; }
    public BalancersStrategySettings4Ray? settings { get; set; }
}

public class BalancersStrategySettings4Ray
{
    public int? expected { get; set; }
    public string? maxRTT { get; set; }
    public float? tolerance { get; set; }
    public List<string>? baselines { get; set; }
    public List<BalancersStrategySettingsCosts4Ray>? costs { get; set; }
}

public class BalancersStrategySettingsCosts4Ray
{
    public bool? regexp { get; set; }
    public string? match { get; set; }
    public float? value { get; set; }
}

public class Observatory4Ray
{
    public List<string>? subjectSelector { get; set; }
    public string? probeUrl { get; set; }
    public string? probeInterval { get; set; }
    public bool? enableConcurrency { get; set; }
}

public class BurstObservatory4Ray
{
    public List<string>? subjectSelector { get; set; }
    public BurstObservatoryPingConfig4Ray? pingConfig { get; set; }
}

public class BurstObservatoryPingConfig4Ray
{
    public string? destination { get; set; }
    public string? connectivity { get; set; }
    public string? interval { get; set; }
    public int? sampling { get; set; }
    public string? timeout { get; set; }
}

public class StreamSettings4Ray
{
    public string network { get; set; }

    public string security { get; set; }

    public TlsSettings4Ray? tlsSettings { get; set; }

    public TcpSettings4Ray? tcpSettings { get; set; }

    public KcpSettings4Ray? kcpSettings { get; set; }

    public WsSettings4Ray? wsSettings { get; set; }

    public HttpupgradeSettings4Ray? httpupgradeSettings { get; set; }

    public XhttpSettings4Ray? xhttpSettings { get; set; }

    public HttpSettings4Ray? httpSettings { get; set; }

    public QuicSettings4Ray? quicSettings { get; set; }

    public TlsSettings4Ray? realitySettings { get; set; }

    public GrpcSettings4Ray? grpcSettings { get; set; }

    public Sockopt4Ray? sockopt { get; set; }
}

public class TlsSettings4Ray
{
    public bool? allowInsecure { get; set; }

    public string? serverName { get; set; }

    public List<string>? alpn { get; set; }

    public string? fingerprint { get; set; }

    public bool? show { get; set; }
    public string? publicKey { get; set; }
    public string? shortId { get; set; }
    public string? spiderX { get; set; }
    public string? mldsa65Verify { get; set; }
    public List<CertificateSettings4Ray>? certificates { get; set; }
    public string? pinnedPeerCertSha256 { get; set; }
    public bool? disableSystemRoot { get; set; }
    public string? echConfigList { get; set; }
    public string? echForceQuery { get; set; }
}

public class CertificateSettings4Ray
{
    public List<string>? certificate { get; set; }
    public string? usage { get; set; }
}

public class TcpSettings4Ray
{
    public Header4Ray header { get; set; }
}

public class Header4Ray
{
    public string type { get; set; }

    public object request { get; set; }

    public object response { get; set; }

    public string? domain { get; set; }
}

public class KcpSettings4Ray
{
    public int mtu { get; set; }

    public int tti { get; set; }

    public int uplinkCapacity { get; set; }

    public int downlinkCapacity { get; set; }

    public bool congestion { get; set; }

    public int readBufferSize { get; set; }

    public int writeBufferSize { get; set; }

    public Header4Ray header { get; set; }

    public string seed { get; set; }
}

public class WsSettings4Ray
{
    public string? path { get; set; }
    public string? host { get; set; }

    public Headers4Ray headers { get; set; }
}

public class Headers4Ray
{
    [JsonPropertyName("User-Agent")]
    public string UserAgent { get; set; }
}

public class HttpupgradeSettings4Ray
{
    public string? path { get; set; }

    public string? host { get; set; }
}

public class XhttpSettings4Ray
{
    public string? path { get; set; }
    public string? host { get; set; }
    public string? mode { get; set; }
    public object? extra { get; set; }
}

public class HttpSettings4Ray
{
    public string? path { get; set; }

    public List<string>? host { get; set; }
}

public class QuicSettings4Ray
{
    public string security { get; set; }

    public string key { get; set; }

    public Header4Ray header { get; set; }
}

public class GrpcSettings4Ray
{
    public string? authority { get; set; }
    public string? serviceName { get; set; }
    public bool multiMode { get; set; }
    public int? idle_timeout { get; set; }
    public int? health_check_timeout { get; set; }
    public bool? permit_without_stream { get; set; }
    public int? initial_windows_size { get; set; }
}

public class AccountsItem4Ray
{
    public string user { get; set; }

    public string pass { get; set; }
}

public class Sockopt4Ray
{
    public string? dialerProxy { get; set; }
}

public class FragmentItem4Ray
{
    public string? packets { get; set; }
    public string? length { get; set; }
    public string? interval { get; set; }
}
