using System.Windows.Input;
using v2rayN.Enums;

namespace v2rayN.Models
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

        public bool enableFragment { get; set; }

        public bool enableCacheFile4Sbox { get; set; } = true;
    }

    [Serializable]
    public class InItem
    {
        public int localPort { get; set; }

        public string protocol { get; set; }

        public bool udpEnabled { get; set; }

        public bool sniffingEnabled { get; set; } = true;
        public List<string>? destOverride { get; set; } = ["http", "tls"];
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
        public bool enableUpdateSubOnlyRemarksExist { get; set; }
        public double mainWidth { get; set; }
        public double mainHeight { get; set; }
        public double mainGirdHeight1 { get; set; }
        public double mainGirdHeight2 { get; set; }
        public bool colorModeDark { get; set; }
        public bool followSystemTheme { get; set; }
        public string? colorPrimaryName { get; set; }
        public string currentLanguage { get; set; }
        public string currentFontFamily { get; set; }
        public int currentFontSize { get; set; }
        public bool enableDragDropSort { get; set; }
        public bool doubleClick2Activate { get; set; }
        public bool autoHideStartup { get; set; }
        public string mainMsgFilter { get; set; }
        public List<ColumnItem> mainColumnItem { get; set; }
        public bool showInTaskbar { get; set; }
    }

    [Serializable]
    public class ConstItem
    {
        public string defIEProxyExceptions { get; set; }
        public string subConvertUrl { get; set; } = string.Empty;
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
        public bool strictRoute { get; set; } = true;
        public string stack { get; set; }
        public int mtu { get; set; }
        public bool enableExInbound { get; set; }
        public bool enableIPv6Address { get; set; }
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
        public string domainStrategy { get; set; }
        public string domainStrategy4Singbox { get; set; }
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

    [Serializable]
    public class Mux4SboxItem
    {
        public string protocol { get; set; }
        public int max_connections { get; set; }
    }

    [Serializable]
    public class HysteriaItem
    {
        public int up_mbps { get; set; }
        public int down_mbps { get; set; }
    }

    [Serializable]
    public class ClashUIItem
    {
        public ERuleMode ruleMode { get; set; }
        public bool enableIPv6 { get; set; }
        public bool enableMixinContent { get; set; }
        public int proxiesSorting { get; set; }
        public bool proxiesAutoRefresh { get; set; }
        public int proxiesAutoDelayTestInterval { get; set; } = 10;
        public int connectionsSorting { get; set; }
        public bool connectionsAutoRefresh { get; set; }
        public int connectionsRefreshInterval { get; set; } = 2;
    }
}