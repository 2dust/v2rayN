namespace v2rayN
{
    internal class Global
    {
        #region const

        public const string githubUrl = "https://github.com";
        public const string githubApiUrl = "https://api.github.com/repos";
        public const string v2rayWebsiteUrl = @"https://www.v2fly.org/";
        public const string AboutUrl = @"https://github.com/2dust/v2rayN";
        public const string UpdateUrl = AboutUrl + @"/releases";
        public const string v2flyCoreUrl = "https://github.com/v2fly/v2ray-core/releases";
        public const string xrayCoreUrl = "https://github.com/XTLS/Xray-core/releases";
        public const string SagerNetCoreUrl = "https://github.com/SagerNet/v2ray-core/releases";
        public const string NUrl = @"https://github.com/2dust/v2rayN/releases";
        public const string clashCoreUrl = "https://github.com/Dreamacro/clash/releases";
        public const string clashMetaCoreUrl = "https://github.com/MetaCubeX/Clash.Meta/releases";
        public const string hysteriaCoreUrl = "https://github.com/apernet/hysteria/releases";
        public const string naiveproxyCoreUrl = "https://github.com/klzgrad/naiveproxy/releases";
        public const string tuicCoreUrl = "https://github.com/EAimTY/tuic/releases";
        public const string singboxCoreUrl = "https://github.com/SagerNet/sing-box/releases";
        public const string geoUrl = "https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/{0}.dat";
        public const string singboxGeoUrl = "https://github.com/soffchen/sing-{0}/releases/latest/download/{0}.db";
        public const string SpeedPingTestUrl = @"https://www.google.com/generate_204";
        public const string juicityCoreUrl = "https://github.com/juicity/juicity/releases";
        public const string CustomRoutingListUrl = @"https://raw.githubusercontent.com/2dust/v2rayCustomRoutingList/master/";

        public const string PromotionUrl = @"aHR0cHM6Ly85LjIzNDQ1Ni54eXovYWJjLmh0bWw=";
        public const string ConfigFileName = "guiNConfig.json";
        public const string ConfigDB = "guiNDB.db";
        public const string coreConfigFileName = "config.json";
        public const string corePreConfigFileName = "configPre.json";

        public const string v2raySampleClient = "v2rayN.Sample.SampleClientConfig";
        public const string SingboxSampleClient = "v2rayN.Sample.SingboxSampleClientConfig";
        public const string v2raySampleHttprequestFileName = "v2rayN.Sample.SampleHttprequest";
        public const string v2raySampleHttpresponseFileName = "v2rayN.Sample.SampleHttpresponse";
        public const string v2raySampleInbound = "v2rayN.Sample.SampleInbound";
        public const string CustomRoutingFileName = "v2rayN.Sample.custom_routing_";

        public const string TunSingboxDNSFileName = "v2rayN.Sample.tun_singbox_dns";
        public const string TunSingboxInboundFileName = "v2rayN.Sample.tun_singbox_inbound";
        public const string TunSingboxRulesFileName = "v2rayN.Sample.tun_singbox_rules";

        public const string DNSV2rayNormalFileName = "v2rayN.Sample.dns_v2ray_normal";
        public const string DNSSingboxNormalFileName = "v2rayN.Sample.dns_singbox_normal";

        public const string DefaultSecurity = "auto";
        public const string DefaultNetwork = "tcp";
        public const string TcpHeaderHttp = "http";
        public const string None = "none";
        public const string agentTag = "proxy";
        public const string directTag = "direct";
        public const string blockTag = "block";
        public const string StreamSecurity = "tls";
        public const string StreamSecurityReality = "reality";
        public const string InboundSocks = "socks";
        public const string InboundHttp = "http";
        public const string InboundSocks2 = "socks2";
        public const string InboundHttp2 = "http2";
        public const string Loopback = "127.0.0.1";
        public const string InboundAPITagName = "api";
        public const string InboundAPIProtocal = "dokodemo-door";

        public const string vmessProtocol = "vmess://";
        public const string vmessProtocolLite = "vmess";
        public const string ssProtocol = "ss://";
        public const string ssProtocolLite = "shadowsocks";
        public const string socksProtocol = "socks://";
        public const string socksProtocolLite = "socks";
        public const string httpProtocol = "http://";
        public const string httpsProtocol = "https://";
        public const string vlessProtocol = "vless://";
        public const string vlessProtocolLite = "vless";
        public const string trojanProtocol = "trojan://";
        public const string trojanProtocolLite = "trojan";

        public const string userEMail = "t@t.tt";
        public const string MyRegPath = "Software\\v2rayNGUI";
        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "v2rayNAutoRun";
        public const string MyRegKeyLanguage = "CurrentLanguage";
        public const string CustomIconName = "v2rayN.ico";
        public const string IEProxyExceptions = "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*";
        public const string RoutingRuleComma = "<COMMA>";
        public const string GrpcgunMode = "gun";
        public const string GrpcmultiMode = "multi";
        public const int MaxPort = 65536;
        public const string CommandClearMsg = "CommandClearMsg";
        public const string DelayUnit = "";
        public const string SpeedUnit = "";
        public const int MinFontSize = 10;
        public const string RebootAs = "rebootas";

        public static readonly List<string> IEProxyProtocols = new() {
                        "{ip}:{http_port}",
                        "socks={ip}:{socks_port}",
                        "http={ip}:{http_port};https={ip}:{http_port};ftp={ip}:{http_port};socks={ip}:{socks_port}",
                        "http=http://{ip}:{http_port};https=http://{ip}:{http_port}",
                        ""
                    };

        public static readonly List<string> SubConvertUrls = new List<string> {
                @"https://sub.xeton.dev/sub?url={0}",
                @"https://api.dler.io/sub?url={0}",
                @"http://127.0.0.1:25500/sub?url={0}",
                ""
            };

        public static readonly List<string> SubConvertConfig = new List<string> {
                @"https://raw.githubusercontent.com/ACL4SSR/ACL4SSR/master/Clash/config/ACL4SSR_Online.ini"
            };

        public static readonly List<string> SubConvertTargets = new List<string> {
                "",
                "mixed",
                "v2ray",
                "clash",
                "ss",
            };

        public static readonly List<string> SpeedTestUrls = new() {
            @"https://speed.cloudflare.com/__down?bytes=100000000",
            @"https://speed.cloudflare.com/__down?bytes=10000000",
            @"http://cachefly.cachefly.net/50mb.test",
            @"http://cachefly.cachefly.net/100mb.test",
            @"http://cachefly.cachefly.net/10mb.test"
        };

        public static readonly Dictionary<string, string> userAgentTxt = new()
        {
            {"chrome","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36" },
            {"firefox","Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0" },
            {"safari","Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15" },
            {"edge","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.70" },
            {"none",""}
        };

        public static readonly List<string> vmessSecuritys = new() { "aes-128-gcm", "chacha20-poly1305", "auto", "none", "zero" };
        public static readonly List<string> ssSecuritys = new() { "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305", "chacha20-ietf-poly1305", "none", "plain" };
        public static readonly List<string> ssSecuritysInSagerNet = new() { "none", "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305", "aes-128-gcm", "aes-192-gcm", "aes-256-gcm", "chacha20-ietf-poly1305", "xchacha20-ietf-poly1305", "rc4", "rc4-md5", "aes-128-ctr", "aes-192-ctr", "aes-256-ctr", "aes-128-cfb", "aes-192-cfb", "aes-256-cfb", "aes-128-cfb8", "aes-192-cfb8", "aes-256-cfb8", "aes-128-ofb", "aes-192-ofb", "aes-256-ofb", "bf-cfb", "cast5-cfb", "des-cfb", "idea-cfb", "rc2-cfb", "seed-cfb", "camellia-128-cfb", "camellia-192-cfb", "camellia-256-cfb", "camellia-128-cfb8", "camellia-192-cfb8", "camellia-256-cfb8", "salsa20", "chacha20", "chacha20-ietf", "xchacha20" };
        public static readonly List<string> ssSecuritysInXray = new() { "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305", "chacha20-ietf-poly1305", "xchacha20-poly1305", "xchacha20-ietf-poly1305", "none", "plain", "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305" };
        public static readonly List<string> flows = new() { "", "xtls-rprx-vision", "xtls-rprx-vision-udp443" };
        public static readonly List<string> networks = new() { "tcp", "kcp", "ws", "h2", "quic", "grpc" };
        public static readonly List<string> kcpHeaderTypes = new() { "srtp", "utp", "wechat-video", "dtls", "wireguard" };
        public static readonly List<string> coreTypes = new() { "v2fly", "SagerNet", "Xray", "sing_box" };
        public static readonly List<string> coreTypes4VLESS = new() { "Xray", "sing_box" };
        public static readonly List<string> domainStrategys = new() { "AsIs", "IPIfNonMatch", "IPOnDemand" };
        public static readonly List<string> domainStrategys4Singbox = new() { "ipv4_only", "ipv6_only", "prefer_ipv4", "prefer_ipv6", "" };
        public static readonly List<string> domainMatchers = new() { "linear", "mph", "" };
        public static readonly List<string> fingerprints = new() { "chrome", "firefox", "safari", "ios", "android", "edge", "360", "qq", "random", "randomized", "" };
        public static readonly List<string> userAgent = new() { "chrome", "firefox", "safari", "edge", "none" };

        public static readonly List<string> allowInsecures = new() { "true", "false", "" };
        public static readonly List<string> domainStrategy4Freedoms = new() { "AsIs", "UseIP", "UseIPv4", "UseIPv6", "" };
        public static readonly List<string> Languages = new() { "zh-Hans", "zh-Hant", "en", "fa-Ir", "ru" };
        public static readonly List<string> alpns = new() { "h2", "http/1.1", "h2,http/1.1", "" };
        public static readonly List<string> LogLevel = new() { "debug", "info", "warning", "error", "none" };
        public static readonly List<string> InboundTags = new() { "socks", "http", "socks2", "http2" };
        public static readonly List<string> Protocols = new() { "http", "tls", "bittorrent" };
        public static readonly List<string> TunMtus = new() { "9000", "1500" };
        public static readonly List<string> TunStacks = new() { "gvisor", "system" };
        public static readonly List<string> PresetMsgFilters = new() { "proxy", "direct", "block", "" };
        public static readonly List<string> SingboxMuxs = new() { "h2mux", "smux", "yamux", "" };

        #endregion const

        #region global variable

        public static int statePort { get; set; }
        public static Job processJob { get; set; }
        public static bool ShowInTaskbar { get; set; }
        public static string ExePathKey { get; set; }

        #endregion global variable
    }
}
