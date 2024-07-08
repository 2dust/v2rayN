using PacLib;
using v2rayN.Common;
using v2rayN.Enums;
using v2rayN.Models;

namespace v2rayN.Handler
{
    public static class SysProxyHandle
    {
        private const string _regPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static bool UpdateSysProxy(Config config, bool forceDisable)
        {
            var type = config.sysProxyType;

            if (forceDisable && type != ESysProxyType.Unchanged)
            {
                type = ESysProxyType.ForcedClear;
            }

            try
            {
                int port = LazyConfig.Instance.GetLocalPort(EInboundProtocol.http);
                int portSocks = LazyConfig.Instance.GetLocalPort(EInboundProtocol.socks);
                int portPac = LazyConfig.Instance.GetLocalPort(EInboundProtocol.pac);
                if (port <= 0)
                {
                    return false;
                }
                if (type == ESysProxyType.ForcedChange)
                {
                    var strExceptions = $"<local>;{config.constItem.defIEProxyExceptions};{config.systemProxyExceptions}";

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
                    ProxySetting.SetProxy(strProxy, strExceptions, 2);
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    ProxySetting.UnsetProxy();
                }
                else if (type == ESysProxyType.Unchanged)
                {
                }
                else if (type == ESysProxyType.Pac)
                {
                    PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                    var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                    ProxySetting.SetProxy(strProxy, "", 4);
                }

                if (type != ESysProxyType.Pac)
                {
                    PacHandler.Stop();
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return true;
        }

        public static void ResetIEProxy4WindowsShutDown()
        {
            ProxySetting.UnsetProxy();
        }
    }
}