using System.Windows.Forms;

namespace v2rayN.Mode
{
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
        public bool enableDragDropSort { get; set; }
        public Dictionary<string, int> mainLvColWidth { get; set; }
    }

    [Serializable]
    public class ConstItem
    {
        public string speedTestUrl { get; set; }
        public string speedPingTestUrl { get; set; }
        public string defIEProxyExceptions { get; set; }
    }

    [Serializable]
    public class KeyEventItem
    {
        public EGlobalHotkey eGlobalHotkey { get; set; }

        public bool Alt { get; set; }

        public bool Control { get; set; }

        public bool Shift { get; set; }

        public Keys? KeyCode { get; set; }

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
        public bool strictRoute { get; set; }
        public string stack { get; set; }
        public int mtu { get; set; }
        public string customTemplate { get; set; }
        public List<string> directIP { get; set; }
        public List<string> directProcess { get; set; }

    }
}
