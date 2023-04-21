using System.Windows.Input;

namespace v2rayN.Mode
{
    [Serializable]
    public class CoreBasicItem
    {
        /// <summary>
        /// 允许日志
        /// </summary>
        public bool logEnabled { get; set; }

        /// <summary>
        /// 日志等级
        /// </summary>
        public string loglevel { get; set; }

        /// <summary>
        /// 允许Mux多路复用
        /// </summary>
        public bool muxEnabled { get; set; }

        /// <summary>
        /// 是否允许不安全连接
        /// </summary>
        public bool defAllowInsecure { get; set; }

        public string defFingerprint { get; set; }

        /// <summary>
        /// 默认用户代理
        /// </summary>
        public string defUserAgent { get; set; }
    }

    [Serializable]
    public class InItem
    {
        public int localPort { get; set; }

        public string protocol { get; set; }

        public bool udpEnabled { get; set; }

        public bool sniffingEnabled { get; set; } = true;
        public bool routeOnly { get; set; }

        public bool allowLANConn { get; set; }

        public bool newPort4LAN { get; set; }

        public string user { get; set; }

        public string pass { get; set; }
    }

    [Serializable]
    public class KcpItem
    {
        public int mtu { get; set; }

        public int tti { get; set; }

        public int uplinkCapacity { get; set; }

        public int downlinkCapacity { get; set; }

        public bool congestion { get; set; }

        public int readBufferSize { get; set; }

        public int writeBufferSize { get; set; }
    }

    [Serializable]
    public class GrpcItem
    {
        public int idle_timeout { get; set; }
        public int health_check_timeout { get; set; }
        public bool permit_without_stream { get; set; }
        public int initial_windows_size { get; set; }
    }

    [Serializable]
    public class GUIItem
    {
        public bool autoRun { get; set; }

        public bool enableStatistics { get; set; }

        public int statisticsFreshRate { get; set; }

        public bool keepOlderDedupl { get; set; }

        public bool ignoreGeoUpdateCore { get; set; } = true;

        public int autoUpdateInterval { get; set; } = 10;

        public bool checkPreReleaseUpdate { get; set; } = false;

        public bool enableSecurityProtocolTls13 { get; set; }

        public int trayMenuServersLimit { get; set; } = 20;

        public bool enableHWA { get; set; } = false;

        public bool enableLog { get; set; } = true;
    }

    [Serializable]
    public class UIItem
    {
        public bool enableAutoAdjustMainLvColWidth { get; set; }
        public double mainWidth { get; set; }
        public double mainHeight { get; set; }
        public double mainGirdHeight1 { get; set; }
        public double mainGirdHeight2 { get; set; }
        public bool colorModeDark { get; set; }
        public string? colorPrimaryName { get; set; }
        public string currentLanguage { get; set; }
        public string currentFontFamily { get; set; }
        public int currentFontSize { get; set; }
        public bool enableDragDropSort { get; set; }
        public bool doubleClick2Activate { get; set; }
        public bool autoHideStartup { get; set; } = true;
        public string mainMsgFilter { get; set; }
        public bool showTrayTip { get; set; }
        public List<ColumnItem> mainColumnItem { get; set; }
    }

    [Serializable]
    public class ConstItem
    {
        public string defIEProxyExceptions { get; set; }
    }

    [Serializable]
    public class KeyEventItem
    {
        public EGlobalHotkey eGlobalHotkey { get; set; }

        public bool Alt { get; set; }

        public bool Control { get; set; }

        public bool Shift { get; set; }

        public Key? KeyCode { get; set; }
    }

    [Serializable]
    public class CoreTypeItem
    {
        public EConfigType configType { get; set; }

        public ECoreType coreType { get; set; }
    }

    [Serializable]
    public class TunModeItem
    {
        public bool enableTun { get; set; }
        public bool showWindow { get; set; }
        public bool enabledLog { get; set; }
        public bool strictRoute { get; set; }
        public string stack { get; set; }
        public int mtu { get; set; }
        public string customTemplate { get; set; }
        public bool bypassMode { get; set; } = true;
        public List<string> directIP { get; set; }
        public List<string> directProcess { get; set; }
        public string directDNS { get; set; }
        public List<string> proxyIP { get; set; }
        public List<string> proxyProcess { get; set; }
        public string proxyDNS { get; set; }
    }

    [Serializable]
    public class SpeedTestItem
    {
        public int speedTestTimeout { get; set; }
        public string speedTestUrl { get; set; }
        public string speedPingTestUrl { get; set; }
    }

    [Serializable]
    public class RoutingBasicItem
    {
        /// <summary>
        /// 域名解析策略
        /// </summary>
        public string domainStrategy { get; set; }

        public string domainMatcher { get; set; }
        public string routingIndexId { get; set; }
        public bool enableRoutingAdvanced { get; set; }
    }

    [Serializable]
    public class ColumnItem
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public int Index { get; set; }
    }
}