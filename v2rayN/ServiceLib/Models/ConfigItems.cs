namespace ServiceLib.Models
{
    [Serializable]
    public class CoreBasicItem
    {
        public bool LogEnabled { get; set; }

        public string Loglevel { get; set; }

        public bool MuxEnabled { get; set; }

        public bool DefAllowInsecure { get; set; }

        public string DefFingerprint { get; set; }

        public string DefUserAgent { get; set; }

        public bool EnableFragment { get; set; }

        public bool EnableCacheFile4Sbox { get; set; } = true;
    }

    [Serializable]
    public class InItem
    {
        public int LocalPort { get; set; }
        public string Protocol { get; set; }
        public bool UdpEnabled { get; set; }
        public bool SniffingEnabled { get; set; } = true;
        public List<string>? DestOverride { get; set; } = ["http", "tls"];
        public bool RouteOnly { get; set; }
        public bool AllowLANConn { get; set; }
        public bool NewPort4LAN { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
        public bool SecondLocalPortEnabled { get; set; }
    }

    [Serializable]
    public class KcpItem
    {
        public int Mtu { get; set; }

        public int Tti { get; set; }

        public int UplinkCapacity { get; set; }

        public int DownlinkCapacity { get; set; }

        public bool Congestion { get; set; }

        public int ReadBufferSize { get; set; }

        public int WriteBufferSize { get; set; }
    }

    [Serializable]
    public class GrpcItem
    {
        public int? IdleTimeout { get; set; }
        public int? HealthCheckTimeout { get; set; }
        public bool? PermitWithoutStream { get; set; }
        public int? InitialWindowsSize { get; set; }
    }

    [Serializable]
    public class GUIItem
    {
        public bool AutoRun { get; set; }
        public bool EnableStatistics { get; set; }
        public bool DisplayRealTimeSpeed { get; set; }
        public bool KeepOlderDedupl { get; set; }
        public int AutoUpdateInterval { get; set; }
        public bool EnableSecurityProtocolTls13 { get; set; }
        public int TrayMenuServersLimit { get; set; } = 20;
        public bool EnableHWA { get; set; } = false;
        public bool EnableLog { get; set; } = true;
    }

    [Serializable]
    public class MsgUIItem
    {
        public string? MainMsgFilter { get; set; }
        public bool? AutoRefresh { get; set; }
    }

    [Serializable]
    public class UIItem
    {
        public bool EnableAutoAdjustMainLvColWidth { get; set; }
        public bool EnableUpdateSubOnlyRemarksExist { get; set; }
        public double MainWidth { get; set; }
        public double MainHeight { get; set; }
        public double MainGirdHeight1 { get; set; }
        public double MainGirdHeight2 { get; set; }
        public EGirdOrientation MainGirdOrientation { get; set; } = EGirdOrientation.Vertical;
        public string? ColorPrimaryName { get; set; }
        public string? CurrentTheme { get; set; }
        public string CurrentLanguage { get; set; }
        public string CurrentFontFamily { get; set; }
        public int CurrentFontSize { get; set; }
        public bool EnableDragDropSort { get; set; }
        public bool DoubleClick2Activate { get; set; }
        public bool AutoHideStartup { get; set; }
        public bool Hide2TrayWhenClose { get; set; }
        public List<ColumnItem> MainColumnItem { get; set; }
        public bool ShowInTaskbar { get; set; }
    }

    [Serializable]
    public class ConstItem
    {
        public string? SubConvertUrl { get; set; }
        public string? GeoSourceUrl { get; set; }
        public string? SrsSourceUrl { get; set; }
        public string? RouteRulesTemplateSourceUrl { get; set; }
    }

    [Serializable]
    public class KeyEventItem
    {
        public EGlobalHotkey EGlobalHotkey { get; set; }

        public bool Alt { get; set; }

        public bool Control { get; set; }

        public bool Shift { get; set; }

        public int? KeyCode { get; set; }
    }

    [Serializable]
    public class CoreTypeItem
    {
        public EConfigType ConfigType { get; set; }

        public ECoreType CoreType { get; set; }
    }

    [Serializable]
    public class TunModeItem
    {
        public bool EnableTun { get; set; }
        public bool StrictRoute { get; set; } = true;
        public string Stack { get; set; }
        public int Mtu { get; set; }
        public bool EnableExInbound { get; set; }
        public bool EnableIPv6Address { get; set; }
        public string? LinuxSudoPwd { get; set; }
    }

    [Serializable]
    public class SpeedTestItem
    {
        public int SpeedTestTimeout { get; set; }
        public string SpeedTestUrl { get; set; }
        public string SpeedPingTestUrl { get; set; }
        public int MixedConcurrencyCount { get; set; }
    }

    [Serializable]
    public class RoutingBasicItem
    {
        public string DomainStrategy { get; set; }
        public string DomainStrategy4Singbox { get; set; }
        public string DomainMatcher { get; set; }
        public string RoutingIndexId { get; set; }
    }

    [Serializable]
    public class ColumnItem
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public int Index { get; set; }
    }

    [Serializable]
    public class Mux4RayItem
    {
        public int? Concurrency { get; set; }
        public int? XudpConcurrency { get; set; }
        public string? XudpProxyUDP443 { get; set; }
    }

    [Serializable]
    public class Mux4SboxItem
    {
        public string Protocol { get; set; }
        public int MaxConnections { get; set; }
        public bool? Padding { get; set; }
    }

    [Serializable]
    public class HysteriaItem
    {
        public int UpMbps { get; set; }
        public int DownMbps { get; set; }
    }

    [Serializable]
    public class ClashUIItem
    {
        public ERuleMode RuleMode { get; set; }
        public bool EnableIPv6 { get; set; }
        public bool EnableMixinContent { get; set; }
        public int ProxiesSorting { get; set; }
        public bool ProxiesAutoRefresh { get; set; }
        public int ProxiesAutoDelayTestInterval { get; set; } = 10;
        public bool ConnectionsAutoRefresh { get; set; }
        public int ConnectionsRefreshInterval { get; set; } = 2;
    }

    [Serializable]
    public class SystemProxyItem
    {
        public ESysProxyType SysProxyType { get; set; }
        public string SystemProxyExceptions { get; set; }
        public bool NotProxyLocalAddress { get; set; } = true;
        public string SystemProxyAdvancedProtocol { get; set; }
    }

    [Serializable]
    public class WebDavItem
    {
        public string? Url { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DirName { get; set; }
    }

    [Serializable]
    public class CheckUpdateItem
    {
        public bool CheckPreReleaseUpdate { get; set; }
        public List<string>? SelectedCoreTypes { get; set; }
    }

    [Serializable]
    public class Fragment4RayItem
    {
        public string? Packets { get; set; }
        public string? Length { get; set; }
        public string? Interval { get; set; }
    }
}
