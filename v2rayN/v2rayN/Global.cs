﻿
namespace v2rayN
{
    class Global
    {
        #region 常量


        public const string v2rayWebsiteUrl = @"https://www.v2fly.org/";
        public const string AboutUrl = @"https://github.com/2dust/v2rayN";
        public const string UpdateUrl = AboutUrl + @"/releases";


        /// <summary>
        /// SpeedTestUrl
        /// </summary>
        public const string SpeedTestUrl = @"http://cachefly.cachefly.net/10mb.test";
        public const string SpeedPingTestUrl = @"https://www.google.com/generate_204";
        public const string AvailabilityTestUrl = @"https://www.google.com/generate_204";

        /// <summary>
        /// CustomRoutingListUrl
        /// </summary>
        public const string CustomRoutingListUrl = @"https://raw.githubusercontent.com/2dust/v2rayCustomRoutingList/master/";

        public const string GFWLIST_URL = "https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        /// <summary>
        /// PromotionUrl
        /// </summary>
        public const string PromotionUrl = @"aHR0cHM6Ly85LjIzNDQ1Ni54eXovYWJjLmh0bWw=";

        /// <summary>
        /// 本软件配置文件名
        /// </summary>
        public const string ConfigFileName = "guiNConfig.json";

        /// <summary>
        /// v2ray配置文件名
        /// </summary>
        public const string v2rayConfigFileName = "config.json";

        /// <summary>
        /// v2ray客户端配置样例文件名
        /// </summary>
        public const string v2raySampleClient = "v2rayN.Sample.SampleClientConfig.txt";
        /// <summary>
        /// v2ray服务端配置样例文件名
        /// </summary>
        public const string v2raySampleServer = "v2rayN.Sample.SampleServerConfig.txt";
        /// <summary>
        /// v2ray配置Httprequest文件名
        /// </summary>
        public const string v2raySampleHttprequestFileName = "v2rayN.Sample.SampleHttprequest.txt";
        /// <summary>
        /// v2ray配置Httpresponse文件名
        /// </summary>
        public const string v2raySampleHttpresponseFileName = "v2rayN.Sample.SampleHttpresponse.txt";
        /// <summary>
        /// 空白的pac文件
        /// </summary>
        public const string BlankPacFileName = "v2rayN.Sample.BlankPac.txt";

        public const string CustomRoutingFileName = "v2rayN.Sample.custom_routing_";


        /// <summary>
        /// 默认加密方式
        /// </summary>
        public const string DefaultSecurity = "auto";

        /// <summary>
        /// 默认传输协议
        /// </summary>
        public const string DefaultNetwork = "tcp";

        /// <summary>
        /// Tcp伪装http
        /// </summary>
        public const string TcpHeaderHttp = "http";

        /// <summary>
        /// None值
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// 代理 tag值
        /// </summary>
        public const string agentTag = "proxy";

        /// <summary>
        /// 直连 tag值
        /// </summary>
        public const string directTag = "direct";

        /// <summary>
        /// 阻止 tag值
        /// </summary>
        public const string blockTag = "block";

        /// <summary>
        /// 
        /// </summary>
        public const string StreamSecurity = "tls";
        public const string StreamSecurityX = "xtls";

        public const string InboundSocks = "socks";
        public const string InboundHttp = "http";
        public const string Loopback = "127.0.0.1";
        public const string InboundAPITagName = "api";
        public const string InboundAPIProtocal = "dokodemo-door";


        /// <summary>
        /// vmess
        /// </summary>
        public const string vmessProtocol = "vmess://";
        /// <summary>
        /// vmess
        /// </summary>
        public const string vmessProtocolLite = "vmess";
        /// <summary>
        /// shadowsocks
        /// </summary>
        public const string ssProtocol = "ss://";
        /// <summary>
        /// shadowsocks
        /// </summary>
        public const string ssProtocolLite = "shadowsocks";
        /// <summary>
        /// socks
        /// </summary>
        public const string socksProtocol = "socks://";
        /// <summary>
        /// socks
        /// </summary>
        public const string socksProtocolLite = "socks";
        /// <summary>
        /// http
        /// </summary>
        public const string httpProtocol = "http://";
        /// <summary>
        /// https
        /// </summary>
        public const string httpsProtocol = "https://";
        /// <summary>
        /// vless
        /// </summary>
        public const string vlessProtocol = "vless://";
        /// <summary>
        /// vless
        /// </summary>
        public const string vlessProtocolLite = "vless";
        /// <summary>
        /// trojan
        /// </summary>
        public const string trojanProtocol = "trojan://";
        /// <summary>
        /// trojan
        /// </summary>
        public const string trojanProtocolLite = "trojan";

        /// <summary>
        /// pac
        /// </summary>
        public const string pacFILE = "pac.txt";

        /// <summary>
        /// email
        /// </summary>
        public const string userEMail = "t@t.tt";

        /// <summary>
        /// MyRegPath
        /// </summary>
        public const string MyRegPath = "Software\\v2rayNGUI";

        /// <summary>
        /// Language
        /// </summary>
        public const string MyRegKeyLanguage = "CurrentLanguage";
        /// <summary>
        /// Icon
        /// </summary>
        public const string CustomIconName = "v2rayN.ico";

        public enum StatisticsFreshRate
        {
            quick = 1000,
            medium = 2000,
            slow = 3000
        }
        public const string StatisticLogOverall = "StatisticLogOverall.json";

        public const string IEProxyExceptions = "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*";

        #endregion

        #region 全局变量

        /// <summary>
        /// 是否需要重启服务V2ray
        /// </summary>
        public static bool reloadV2ray
        {
            get; set;
        }

        /// <summary>
        /// 是否开启全局代理(http)
        /// </summary>
        public static bool sysAgent
        {
            get; set;
        }

        /// <summary>
        /// socks端口
        /// </summary>
        public static int socksPort
        {
            get; set;
        }

        /// <summary>
        /// http端口
        /// </summary>
        public static int httpPort
        {
            get; set;
        }

        /// <summary>
        /// PAC端口
        /// </summary>
        public static int pacPort
        {
            get; set;
        }

        /// <summary>
        ///  
        /// </summary>
        public static int statePort
        {
            get; set;
        }

        public static Job processJob
        {
            get; set;
        }
        public static System.Threading.Mutex mutexObj
        {
            get; set;
        }

        #endregion



    }
}
