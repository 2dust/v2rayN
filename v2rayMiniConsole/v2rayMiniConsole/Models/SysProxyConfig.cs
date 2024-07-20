namespace v2rayN.Models
{
    internal class SysProxyConfig
    {
        public bool UserSettingsRecorded;
        public string Flags;
        public string ProxyServer;
        public string BypassList;
        public string PacUrl;

        public SysProxyConfig()
        {
            UserSettingsRecorded = false;
            Flags = "1";
            ProxyServer = "";
            BypassList = "";
            PacUrl = "";
        }
    }
}