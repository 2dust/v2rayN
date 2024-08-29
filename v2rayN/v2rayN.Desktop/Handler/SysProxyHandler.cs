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
                if (port <= 0)
                {
                    return false;
                }
                if (type == ESysProxyType.ForcedChange)
                {
                    var strProxy = $"{Global.Loopback}:{port}";
                    await SetProxy(strProxy);
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    await UnsetProxy();
                }
                else if (type == ESysProxyType.Unchanged)
                {
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return true;
        }

        private static async Task SetProxy(string? strProxy)
        {
            await Task.Run(() =>
            {
                var httpProxy = strProxy is null ? null : $"{Global.HttpProtocol}{strProxy}";
                var socksProxy = strProxy is null ? null : $"{Global.SocksProtocol}{strProxy}";
                var noProxy = $"localhost,127.0.0.0/8,::1";

                Environment.SetEnvironmentVariable("http_proxy", httpProxy, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("https_proxy", httpProxy, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("all_proxy", socksProxy, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("no_proxy", noProxy, EnvironmentVariableTarget.User);
            });
        }

        private static async Task UnsetProxy()
        {
            await SetProxy(null);
        }
    }
}