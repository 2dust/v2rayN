using PacLib;

namespace v2rayN.Handler
{
    public static class SysProxyHandler
    {
        public static bool UpdateSysProxy(Config config, bool forceDisable)
        {
            var type = config.systemProxyItem.sysProxyType;

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
                    var strExceptions = "";
                    if (config.systemProxyItem.notProxyLocalAddress)
                    {
                        strExceptions = $"<local>;{config.constItem.defIEProxyExceptions};{config.systemProxyItem.systemProxyExceptions}";
                    }

                    var strProxy = string.Empty;
                    if (Utils.IsNullOrEmpty(config.systemProxyItem.systemProxyAdvancedProtocol))
                    {
                        strProxy = $"{Global.Loopback}:{port}";
                    }
                    else
                    {
                        strProxy = config.systemProxyItem.systemProxyAdvancedProtocol
                            .Replace("{ip}", Global.Loopback)
                            .Replace("{http_port}", port.ToString())
                            .Replace("{socks_port}", portSocks.ToString());
                    }
                    ProxySettingWindows.SetProxy(strProxy, strExceptions, 2);
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    ProxySettingWindows.UnsetProxy();
                }
                else if (type == ESysProxyType.Unchanged)
                {
                }
                else if (type == ESysProxyType.Pac)
                {
                    PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                    var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                    ProxySettingWindows.SetProxy(strProxy, "", 4);
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
    }
}