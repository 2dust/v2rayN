using PacLib;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    public static class SysProxyHandle
    {
        //private const string _userWininetConfigFile = "user-wininet.json";

        //private static string _queryStr;

        // In general, this won't change
        // format:
        //  <flags><CR-LF>
        //  <proxy-server><CR-LF>
        //  <bypass-list><CR-LF>
        //  <pac-url>

        private enum RET_ERRORS : int
        {
            RET_NO_ERROR = 0,
            INVALID_FORMAT = 1,
            NO_PERMISSION = 2,
            SYSCALL_FAILED = 3,
            NO_MEMORY = 4,
            INVAILD_OPTION_COUNT = 5,
        };

        static SysProxyHandle()
        {
        }

        public static bool UpdateSysProxy(Config config, bool forceDisable)
        {
            var type = config.sysProxyType;

            if (forceDisable && type != ESysProxyType.Unchanged)
            {
                type = ESysProxyType.ForcedClear;
            }

            try
            {
                int port = LazyConfig.Instance.GetLocalPort(Global.InboundHttp);
                int portSocks = LazyConfig.Instance.GetLocalPort(Global.InboundSocks);
                int portPac = LazyConfig.Instance.GetLocalPort(ESysProxyType.Pac.ToString());
                if (port <= 0)
                {
                    return false;
                }
                if (type == ESysProxyType.ForcedChange)
                {
                    var strExceptions = $"{config.constItem.defIEProxyExceptions};{config.systemProxyExceptions}";

                    var strProxy = string.Empty;
                    if (Utils.IsNullOrEmpty(config.systemProxyAdvancedProtocol))
                    {
                        strProxy = $"{Global.Loopback}:{port}";
                    }
                    else
                    {
                        strProxy = config.systemProxyAdvancedProtocol
                            .Replace("{ip}", Global.Loopback)
                            .Replace("{http_port}", port.ToString())
                            .Replace("{socks_port}", portSocks.ToString());
                    }
                    ProxySetting.SetProxy(strProxy, strExceptions, 2); // set a named proxy
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    ProxySetting.UnsetProxy(); // set to no proxy
                }
                else if (type == ESysProxyType.Unchanged)
                {
                }
                else if (type == ESysProxyType.Pac)
                {
                    PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                    var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                    ProxySetting.SetProxy(strProxy, "", 4); // use pac script url for auto-config proxy
                }

                if (type != ESysProxyType.Pac)
                {
                    PacHandler.Stop();
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return true;
        }

        public static void ResetIEProxy4WindowsShutDown()
        {
            try
            {
                //TODO To be verified
                Utils.RegWriteValue(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyEnable", 0);
            }
            catch
            {
            }
        }
    }
}