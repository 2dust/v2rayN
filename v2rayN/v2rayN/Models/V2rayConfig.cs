using System.Text.Json.Serialization;

namespace v2rayN.Models
{
    /// <summary>
    /// v2ray配置文件实体类 例子SampleConfig.txt
    /// </summary>
    public class V2rayConfig
    {
        /// <summary>
        /// Properties that do not belong to Ray
        /// </summary>
        public string? remarks { get; set; }

        /// <summary>
        /// 日志配置
        /// </summary>
        public Log4Ray log { get; set; }

        /// <summary>
        /// 传入连接配置
        /// </summary>
        public List<Inbounds4Ray> inbounds { get; set; }

        /// <summary>
        /// 传出连接配置
        /// </summary>
        public List<Outbounds4Ray> outbounds { get; set; }

        /// <summary>
        /// 统计需要， 空对象
        /// </summary>
        public Stats4Ray stats { get; set; }

        /// </summary>
        public API4Ray api { get; set; }

        /// </summary>
        public Policy4Ray policy { get; set; }

        /// <summary>
        /// DNS 配置
        /// </summary>
        public object dns { get; set; }

        /// <summary>
        /// 路由配置
        /// </summary>
        public Routing4Ray routing { get; set; }
    }

    public class Stats4Ray
    { };

    public class API4Ray
    {
        public string tag { get; set; }
        public List<string> services { get; set; }
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
        /// <summary>
        ///
        /// </summary>
        public string access { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string error { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string loglevel { get; set; }
    }

    public class Inbounds4Ray
    {
        public string tag { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int port { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string listen { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string protocol { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Sniffing4Ray sniffing { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Inboundsettings4Ray settings { get; set; }

        /// <summary>
        ///
        /// </summary>
        public StreamSettings4Ray streamSettings { get; set; }
    }

    public class Inboundsettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string auth { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool udp { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string ip { get; set; }

        /// <summary>
        /// api 使用
        /// </summary>
        public string address { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<UsersItem4Ray> clients { get; set; }

        /// <summary>
        /// VLESS
        /// </summary>
        public string decryption { get; set; }

        public bool allowTransparent { get; set; }

        public List<AccountsItem4Ray> accounts { get; set; }
    }

    public class UsersItem4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string id { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int alterId { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string email { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string security { get; set; }

        /// <summary>
        /// VLESS
        /// </summary>
        public string encryption { get; set; }

        /// <summary>
        /// VLESS
        /// </summary>
        public string? flow { get; set; }
    }

    public class Sniffing4Ray
    {
        public bool enabled { get; set; }
        public List<string> destOverride { get; set; }
        public bool routeOnly { get; set; }
    }

    public class Outbounds4Ray
    {
        /// <summary>
        /// 默认值agentout
        /// </summary>
        public string tag { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string protocol { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Outboundsettings4Ray settings { get; set; }

        /// <summary>
        ///
        /// </summary>
        public StreamSettings4Ray streamSettings { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Mux4Ray mux { get; set; }
    }

    public class Outboundsettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public List<VnextItem4Ray>? vnext { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<ServersItem4Ray> servers { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Response4Ray response { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string domainStrategy { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int? userLevel { get; set; }
    }

    public class VnextItem4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string address { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int port { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<UsersItem4Ray> users { get; set; }
    }

    public class ServersItem4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string email { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string address { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? method { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool? ota { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? password { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int port { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int? level { get; set; }

        /// <summary>
        /// trojan
        /// </summary>
        public string flow { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<SocksUsersItem4Ray> users { get; set; }
    }

    public class SocksUsersItem4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string user { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string pass { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int? level { get; set; }
    }

    public class Mux4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public bool enabled { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int concurrency { get; set; }
    }

    public class Response4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string type { get; set; }
    }

    public class Dns4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public List<string> servers { get; set; }
    }

    public class Routing4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string domainStrategy { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string domainMatcher { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<RulesItem4Ray> rules { get; set; }
    }

    [Serializable]
    public class RulesItem4Ray
    {
        public string? type { get; set; }

        public string? port { get; set; }

        public List<string>? inboundTag { get; set; }

        public string? outboundTag { get; set; }

        public List<string>? ip { get; set; }

        public List<string>? domain { get; set; }

        public List<string>? protocol { get; set; }
    }

    public class StreamSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string network { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string security { get; set; }

        /// <summary>
        ///
        /// </summary>
        public TlsSettings4Ray tlsSettings { get; set; }

        /// <summary>
        /// Tcp传输额外设置
        /// </summary>
        public TcpSettings4Ray tcpSettings { get; set; }

        /// <summary>
        /// Kcp传输额外设置
        /// </summary>
        public KcpSettings4Ray kcpSettings { get; set; }

        /// <summary>
        /// ws传输额外设置
        /// </summary>
        public WsSettings4Ray wsSettings { get; set; }

        /// <summary>
        ///
        /// </summary>
        public HttpupgradeSettings4Ray? httpupgradeSettings { get; set; }

        /// <summary>
        /// h2传输额外设置
        /// </summary>
        public HttpSettings4Ray httpSettings { get; set; }

        /// <summary>
        /// QUIC
        /// </summary>
        public QuicSettings4Ray quicSettings { get; set; }

        /// <summary>
        /// VLESS only
        /// </summary>
        public TlsSettings4Ray realitySettings { get; set; }

        /// <summary>
        /// grpc
        /// </summary>
        public GrpcSettings4Ray grpcSettings { get; set; }

        /// <summary>
        /// sockopt
        /// </summary>
        public Sockopt4Ray? sockopt { get; set; }
    }

    public class TlsSettings4Ray
    {
        /// <summary>
        /// 是否允许不安全连接（用于客户端）
        /// </summary>
        public bool? allowInsecure { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? serverName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<string>? alpn { get; set; }

        public string? fingerprint { get; set; }

        public bool? show { get; set; } = false;
        public string? publicKey { get; set; }
        public string? shortId { get; set; }
        public string? spiderX { get; set; }
    }

    public class TcpSettings4Ray
    {
        /// <summary>
        /// 数据包头部伪装设置
        /// </summary>
        public Header4Ray header { get; set; }
    }

    public class Header4Ray
    {
        /// <summary>
        /// 伪装
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// 结构复杂，直接存起来
        /// </summary>
        public object request { get; set; }

        /// <summary>
        /// 结构复杂，直接存起来
        /// </summary>
        public object response { get; set; }
    }

    public class KcpSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public int mtu { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int tti { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int uplinkCapacity { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int downlinkCapacity { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool congestion { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int readBufferSize { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int writeBufferSize { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Header4Ray header { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string seed { get; set; }
    }

    public class WsSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string path { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Headers4Ray headers { get; set; }
    }

    public class Headers4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 用户代理
        /// </summary>
        [JsonPropertyName("User-Agent")]
        public string UserAgent { get; set; }
    }

    public class HttpupgradeSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string? path { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string? host { get; set; }
    }

    public class HttpSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string path { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<string> host { get; set; }
    }

    public class QuicSettings4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string security { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string key { get; set; }

        /// <summary>
        ///
        /// </summary>
        public Header4Ray header { get; set; }
    }

    public class GrpcSettings4Ray
    {
        public string? authority { get; set; }
        public string? serviceName { get; set; }
        public bool multiMode { get; set; }
        public int idle_timeout { get; set; }
        public int health_check_timeout { get; set; }
        public bool permit_without_stream { get; set; }
        public int initial_windows_size { get; set; }
    }

    public class AccountsItem4Ray
    {
        /// <summary>
        ///
        /// </summary>
        public string user { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string pass { get; set; }
    }

    public class Sockopt4Ray
    {
        public string? dialerProxy { get; set; }
    }
}