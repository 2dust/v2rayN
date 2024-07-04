using PacLib;
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
                    if (!ProxySetting.SetProxy(strProxy, strExceptions, 2))
                    {
                        SetProxy(strProxy, strExceptions, 2);
                    }
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    if (!ProxySetting.UnsetProxy())
                    {
                        UnsetProxy();
                    }
                }
                else if (type == ESysProxyType.Unchanged)
                {
                }
                else if (type == ESysProxyType.Pac)
                {
                    PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                    var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                    if (!ProxySetting.SetProxy(strProxy, "", 4))
                    {
                        SetProxy(strProxy, "", 4);
                    }
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
            SetProxy(null, null, 1);
        }

        private static void UnsetProxy()
        {
            SetProxy(null, null, 1);
        }

        private static bool SetProxy(string? strProxy, string? exceptions, int type)
        {
            if (type == 1)
            {
                Utils.RegWriteValue(_regPath, "ProxyEnable", 0);
                Utils.RegWriteValue(_regPath, "ProxyServer", string.Empty);
                Utils.RegWriteValue(_regPath, "ProxyOverride", string.Empty);
                Utils.RegWriteValue(_regPath, "AutoConfigURL", string.Empty);
            }
            if (type == 2)
            {
                Utils.RegWriteValue(_regPath, "ProxyEnable", 1);
                Utils.RegWriteValue(_regPath, "ProxyServer", strProxy ?? string.Empty);
                Utils.RegWriteValue(_regPath, "ProxyOverride", exceptions ?? string.Empty);
                Utils.RegWriteValue(_regPath, "AutoConfigURL", string.Empty);
            }
            else if (type == 4)
            {
                Utils.RegWriteValue(_regPath, "ProxyEnable", 0);
                Utils.RegWriteValue(_regPath, "ProxyServer", string.Empty);
                Utils.RegWriteValue(_regPath, "ProxyOverride", string.Empty);
                Utils.RegWriteValue(_regPath, "AutoConfigURL", strProxy ?? string.Empty);
            }
            return true;
        }
    }
}