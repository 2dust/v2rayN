using PacLib;

namespace ServiceLib.Handler.SysProxy
{
    public static class SysProxyHandler
    {
        public static async Task<bool> UpdateSysProxy(Config config, bool forceDisable)
        {
            var type = config.SystemProxyItem.SysProxyType;

            if (forceDisable && type != ESysProxyType.Unchanged)
            {
                type = ESysProxyType.ForcedClear;
            }

            try
            {
                var port = AppHandler.Instance.GetLocalPort(EInboundProtocol.http);
                var portSocks = AppHandler.Instance.GetLocalPort(EInboundProtocol.socks);
                if (port <= 0)
                {
                    return false;
                }
                switch (type)
                {
                    case ESysProxyType.ForcedChange when Utils.IsWindows():
                        {
                            GetWindowsProxyString(config, port, portSocks, out var strProxy, out var strExceptions);
                            ProxySettingWindows.SetProxy(strProxy, strExceptions, 2);
                            break;
                        }
                    case ESysProxyType.ForcedChange when Utils.IsLinux():
                        await ProxySettingLinux.SetProxy(Global.Loopback, port);
                        break;

                    case ESysProxyType.ForcedChange:
                        {
                            if (Utils.IsOSX())
                            {
                                await ProxySettingOSX.SetProxy(Global.Loopback, port);
                            }

                            break;
                        }
                    case ESysProxyType.ForcedClear when Utils.IsWindows():
                        ProxySettingWindows.UnsetProxy();
                        break;

                    case ESysProxyType.ForcedClear when Utils.IsLinux():
                        await ProxySettingLinux.UnsetProxy();
                        break;

                    case ESysProxyType.ForcedClear:
                        {
                            if (Utils.IsOSX())
                            {
                                await ProxySettingOSX.UnsetProxy();
                            }

                            break;
                        }
                    case ESysProxyType.Pac when Utils.IsWindows():
                        {
                            var portPac = AppHandler.Instance.GetLocalPort(EInboundProtocol.pac);
                            PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                            var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                            ProxySettingWindows.SetProxy(strProxy, "", 4);
                            break;
                        }
                }

                if (type != ESysProxyType.Pac && Utils.IsWindows())
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

        private static void GetWindowsProxyString(Config config, int port, int portSocks, out string strProxy, out string strExceptions)
        {
            strExceptions = "";
            if (config.SystemProxyItem.NotProxyLocalAddress)
            {
                strExceptions = $"<local>;{config.ConstItem.DefIEProxyExceptions};{config.SystemProxyItem.SystemProxyExceptions}";
            }

            strProxy = string.Empty;
            if (Utils.IsNullOrEmpty(config.SystemProxyItem.SystemProxyAdvancedProtocol))
            {
                strProxy = $"{Global.Loopback}:{port}";
            }
            else
            {
                strProxy = config.SystemProxyItem.SystemProxyAdvancedProtocol
                    .Replace("{ip}", Global.Loopback)
                    .Replace("{http_port}", port.ToString())
                    .Replace("{socks_port}", portSocks.ToString());
            }
        }
    }
}