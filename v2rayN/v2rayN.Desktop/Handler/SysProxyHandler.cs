using PacLib;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Handler
{
    public static class SysProxyHandler
    {
        public static async Task<bool> UpdateSysProxy(Config config, bool forceDisable)
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
                    if (Utils.IsWindows())
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
                    else if (Utils.IsLinux())
                    {
                        await ProxySettingLinux.SetProxy(Global.Loopback, port);
                    }
                    else if (Utils.IsOSX())
                    {
                        await ProxySettingOSX.SetProxy(Global.Loopback, port);
                    }
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    if (Utils.IsWindows())
                    {
                        ProxySettingWindows.UnsetProxy();
                    }
                    else if (Utils.IsLinux())
                    {
                        await ProxySettingLinux.UnsetProxy();
                    }
                    else if (Utils.IsOSX())
                    {
                        await ProxySettingOSX.UnsetProxy();
                    }
                }
                else if (type == ESysProxyType.Pac)
                {
                }

                //if (type != ESysProxyType.Pac)
                //{
                //    PacHandler.Stop();
                //}
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return true;
        }
    }
}