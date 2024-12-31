namespace ServiceLib
{
    public class Global
    {
        #region const

        public const string AppName = "v2rayN";
        public const string GithubUrl = "https://github.com";
        public const string GithubApiUrl = "https://api.github.com/repos";
        public const string V2flyCoreUrl = "https://github.com/v2fly/v2ray-core/releases";
        public const string XrayCoreUrl = "https://github.com/XTLS/Xray-core/releases";
        public const string NUrl = @"https://github.com/2dust/v2rayN/releases";
        public const string MihomoCoreUrl = "https://github.com/MetaCubeX/mihomo/releases";
        public const string HysteriaCoreUrl = "https://github.com/apernet/hysteria/releases";
        public const string NaiveproxyCoreUrl = "https://github.com/klzgrad/naiveproxy/releases";
        public const string TuicCoreUrl = "https://github.com/EAimTY/tuic/releases";
        public const string SingboxCoreUrl = "https://github.com/SagerNet/sing-box/releases";
        public const string GeoUrl = "https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/{0}.dat";
        public const string SpeedPingTestUrl = @"https://www.google.com/generate_204";
        public const string JuicityCoreUrl = "https://github.com/juicity/juicity/releases";
        public const string CustomRoutingListUrl = @"https://raw.githubusercontent.com/2dust/v2rayCustomRoutingList/master/";
        public const string SingboxRulesetUrl = @"https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-{0}/{1}.srs";
        public const string IPAPIUrl = "https://api.ip.sb/geoip";

        public const string PromotionUrl = @"aHR0cHM6Ly85LjIzNDQ1Ni54eXovYWJjLmh0bWw=";
        public const string ConfigFileName = "guiNConfig.json";
        public const string CoreConfigFileName = "config.json";
        public const string CorePreConfigFileName = "configPre.json";
        public const string CoreSpeedtestConfigFileName = "configSpeedtest.json";
        public const string CoreMultipleLoadConfigFileName = "configMultipleLoad.json";
        public const string ClashMixinConfigFileName = "Mixin.yaml";

        public const string NamespaceSample = "ServiceLib.Sample.";
        public const string V2raySampleClient = NamespaceSample + "SampleClientConfig";
        public const string SingboxSampleClient = NamespaceSample + "SingboxSampleClientConfig";
        public const string V2raySampleHttpRequestFileName = NamespaceSample + "SampleHttpRequest";
        public const string V2raySampleHttpResponseFileName = NamespaceSample + "SampleHttpResponse";
        public const string V2raySampleInbound = NamespaceSample + "SampleInbound";
        public const string V2raySampleOutbound = NamespaceSample + "SampleOutbound";
        public const string SingboxSampleOutbound = NamespaceSample + "SingboxSampleOutbound";
        public const string CustomRoutingFileName = NamespaceSample + "custom_routing_";
        public const string TunSingboxDNSFileName = NamespaceSample + "tun_singbox_dns";
        public const string TunSingboxInboundFileName = NamespaceSample + "tun_singbox_inbound";
        public const string TunSingboxRulesFileName = NamespaceSample + "tun_singbox_rules";
        public const string DNSV2rayNormalFileName = NamespaceSample + "dns_v2ray_normal";
        public const string DNSSingboxNormalFileName = NamespaceSample + "dns_singbox_normal";
        public const string ClashMixinYaml = NamespaceSample + "clash_mixin_yaml";
        public const string ClashTunYaml = NamespaceSample + "clash_tun_yaml";
        public const string LinuxAutostartConfig = NamespaceSample + "linux_autostart_config";
        public const string PacFileName = NamespaceSample + "pac";

        public const string DefaultSecurity = "auto";
        public const string DefaultNetwork = "tcp";
        public const string TcpHeaderHttp = "http";
        public const string None = "none";
        public const string ProxyTag = "proxy";
        public const string DirectTag = "direct";
        public const string BlockTag = "block";
        public const string StreamSecurity = "tls";
        public const string StreamSecurityReality = "reality";
        public const string Loopback = "127.0.0.1";
        public const string InboundAPIProtocol = "dokodemo-door";
        public const string HttpProtocol = "http://";
        public const string HttpsProtocol = "https://";
        public const string SocksProtocol = "socks://";

        public const string UserEMail = "t@t.tt";
        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "v2rayNAutoRun";
        public const string CustomIconName = "v2rayN.ico";
        public const string SystemProxyExceptionsWindows = "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*";
        public const string SystemProxyExceptionsLinux = "localhost, 127.0.0.0/8, ::1";
        public const string RoutingRuleComma = "<COMMA>";
        public const string GrpcGunMode = "gun";
        public const string GrpcMultiMode = "multi";
        public const int MaxPort = 65536;
        public const string DelayUnit = "";
        public const string SpeedUnit = "";
        public const int MinFontSize = 8;
        public const string RebootAs = "rebootas";
        public const string AvaAssets = "avares://v2rayN/Assets/";

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
            @"https://speed.cloudflare.com/__down?bytes=50000000",
            @"https://speed.cloudflare.com/__down?bytes=10000000",
            @"https://cachefly.cachefly.net/50mb.test",
        };

        public static readonly List<string> SpeedPingTestUrls = new() {
            @"https://www.google.com/generate_204",
            @"https://www.gstatic.com/generate_204",
            @"https://www.apple.com/library/test/success.html",
            @"http://www.msftconnecttest.com/connecttest.txt",
        };

        public static readonly List<string> GeoFilesSources = new() {
            "",
            @"https://github.com/runetfreedom/russia-v2ray-rules-dat/releases/latest/download/{0}.dat",
            @"https://cdn.jsdelivr.net/gh/chocolate4u/Iran-v2ray-rules@release/{0}.dat",
        };

        public static readonly List<string> SingboxRulesetSources = new() {
            "",
            @"https://cdn.jsdelivr.net/gh/runetfreedom/russia-v2ray-rules-dat@release/sing-box/rule-set-{0}/{1}.srs",
            @"https://cdn.jsdelivr.net/gh/chocolate4u/Iran-sing-box-rules@rule-set/{1}.srs",
        };

        public static readonly List<string> RoutingRulesSources = new() {
            "",
            @"https://cdn.jsdelivr.net/gh/runetfreedom/russia-v2ray-custom-routing-list@main/v2rayN/template.json",
            @"https://cdn.jsdelivr.net/gh/Chocolate4U/Iran-v2ray-rules@main/v2rayN/template.json",
        };

        public static readonly List<string> DNSTemplateSources = new() {
            "",
            @"https://cdn.jsdelivr.net/gh/runetfreedom/russia-v2ray-custom-routing-list@main/v2rayN/",
            @"https://cdn.jsdelivr.net/gh/Chocolate4U/Iran-v2ray-rules@main/v2rayN/",
        };

        public static readonly Dictionary<string, string> UserAgentTexts = new()
        {
            {"chrome","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36" },
            {"firefox","Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0" },
            {"safari","Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15" },
            {"edge","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.70" },
            {"none",""}
        };

        public const string Hysteria2ProtocolShare = "hy2://";

        public static readonly Dictionary<EConfigType, string> ProtocolShares = new()
        {
            {EConfigType.VMess,"vmess://"},
            {EConfigType.Shadowsocks,"ss://"},
            {EConfigType.SOCKS,"socks://"},
            {EConfigType.VLESS,"vless://"},
            {EConfigType.Trojan,"trojan://"},
            {EConfigType.Hysteria2,"hysteria2://"},
            {EConfigType.TUIC,"tuic://"},
            {EConfigType.WireGuard,"wireguard://"}
        };

        public static readonly Dictionary<EConfigType, string> ProtocolTypes = new()
        {
            {EConfigType.VMess,"vmess"},
            {EConfigType.Shadowsocks,"shadowsocks"},
            {EConfigType.SOCKS,"socks"},
            {EConfigType.HTTP,"http"},
            {EConfigType.VLESS,"vless"},
            {EConfigType.Trojan,"trojan"},
            {EConfigType.Hysteria2,"hysteria2"},
            {EConfigType.TUIC,"tuic"},
            {EConfigType.WireGuard,"wireguard"}
        };

        public static readonly List<string> VmessSecurities = new() { "aes-128-gcm", "chacha20-poly1305", "auto", "none", "zero" };
        public static readonly List<string> SsSecurities = new() { "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305", "chacha20-ietf-poly1305", "none", "plain" };
        public static readonly List<string> SsSecuritiesInXray = new() { "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305", "chacha20-ietf-poly1305", "xchacha20-poly1305", "xchacha20-ietf-poly1305", "none", "plain", "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305" };
        public static readonly List<string> SsSecuritiesInSingbox = new() { "aes-256-gcm", "aes-192-gcm", "aes-128-gcm", "chacha20-ietf-poly1305", "xchacha20-ietf-poly1305", "none", "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305", "aes-128-ctr", "aes-192-ctr", "aes-256-ctr", "aes-128-cfb", "aes-192-cfb", "aes-256-cfb", "rc4-md5", "chacha20-ietf", "xchacha20" };
        public static readonly List<string> Flows = new() { "", "xtls-rprx-vision", "xtls-rprx-vision-udp443" };
        public static readonly List<string> Networks = new() { "tcp", "kcp", "ws", "httpupgrade", "xhttp", "h2", "quic", "grpc" };
        public static readonly List<string> KcpHeaderTypes = new() { "srtp", "utp", "wechat-video", "dtls", "wireguard" };
        public static readonly List<string> CoreTypes = new() { "Xray", "sing_box" };
        public static readonly List<string> DomainStrategies = new() { "AsIs", "IPIfNonMatch", "IPOnDemand" };
        public static readonly List<string> DomainStrategies4Singbox = new() { "ipv4_only", "ipv6_only", "prefer_ipv4", "prefer_ipv6", "" };
        public static readonly List<string> DomainMatchers = new() { "linear", "mph", "" };
        public static readonly List<string> Fingerprints = new() { "chrome", "firefox", "safari", "ios", "android", "edge", "360", "qq", "random", "randomized", "" };
        public static readonly List<string> UserAgent = new() { "chrome", "firefox", "safari", "edge", "none" };
        public static readonly List<string> XhttpMode = new() { "auto", "packet-up", "stream-up", "stream-one" };

        public static readonly List<string> AllowInsecure = new() { "true", "false", "" };
        public static readonly List<string> DomainStrategy4Freedoms = new() { "AsIs", "UseIP", "UseIPv4", "UseIPv6", "" };
        public static readonly List<string> SingboxDomainStrategy4Out = new() { "ipv4_only", "prefer_ipv4", "prefer_ipv6", "ipv6_only", "" };
        public static readonly List<string> DomainDNSAddress = ["223.5.5.5", "223.6.6.6", "localhost"];
        public static readonly List<string> SingboxDomainDNSAddress = ["223.5.5.5", "223.6.6.6", "dhcp://auto"];
        public static readonly List<string> Languages = new() { "zh-Hans", "zh-Hant", "en", "fa-Ir", "ru", "hu" };
        public static readonly List<string> Alpns = new() { "h3", "h2", "http/1.1", "h3,h2", "h2,http/1.1", "h3,h2,http/1.1", "" };
        public static readonly List<string> LogLevels = new() { "debug", "info", "warning", "error", "none" };
        public static readonly List<string> InboundTags = new() { "socks", "socks2", "socks3" };
        public static readonly List<string> RuleProtocols = new() { "http", "tls", "bittorrent" };
        public static readonly List<string> RuleNetworks = new() { "", "tcp", "udp", "tcp,udp" };
        public static readonly List<string> destOverrideProtocols = ["http", "tls", "quic", "fakedns", "fakedns+others"];
        public static readonly List<string> TunMtus = new() { "1280", "1408", "1500", "9000" };
        public static readonly List<string> TunStacks = new() { "gvisor", "system" };
        public static readonly List<string> PresetMsgFilters = new() { "proxy", "direct", "block", "" };
        public static readonly List<string> SingboxMuxs = new() { "h2mux", "smux", "yamux", "" };
        public static readonly List<string> TuicCongestionControls = new() { "cubic", "new_reno", "bbr" };

        public static readonly List<string> allowSelectType = new() { "selector", "urltest", "loadbalance", "fallback" };
        public static readonly List<string> notAllowTestType = new() { "selector", "urltest", "direct", "reject", "compatible", "pass", "loadbalance", "fallback" };
        public static readonly List<string> proxyVehicleType = new() { "file", "http" };

        #endregion const
    }
}