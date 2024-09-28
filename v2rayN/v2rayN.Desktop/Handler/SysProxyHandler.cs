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
                        //TODO
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
                        //TODO
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