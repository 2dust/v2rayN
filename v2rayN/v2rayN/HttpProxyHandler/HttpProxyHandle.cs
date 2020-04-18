using System;
using System.Threading.Tasks;
using v2rayN.Mode;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// 系统代理(http)模式
    /// </summary>
    public enum ListenerType
    {
        noHttpProxy = 0,
        GlobalHttp = 1,
        GlobalPac = 2,
        HttpOpenAndClear = 3,
        PacOpenAndClear = 4,
        HttpOpenOnly = 5,
        PacOpenOnly = 6
    }
    /// <summary>
    /// 系统代理(http)总处理
    /// 启动privoxy提供http协议
    /// 设置IE系统代理或者PAC模式
    /// </summary>
    class HttpProxyHandle
    {
        private static bool Update(Config config, bool forceDisable)
        {
            ListenerType type = config.listenerType;

            if (forceDisable)
            {
                type = ListenerType.noHttpProxy;
            }

            try
            {
                if (type != ListenerType.noHttpProxy)
                {
                    int port = Global.httpPort;
                    if (port <= 0)
                    {
                        return false;
                    }
                    if (type == ListenerType.GlobalHttp)
                    {
                        //PACServerHandle.Stop();
                        //ProxySetting.SetProxy($"{Global.Loopback}:{port}", Global.IEProxyExceptions, 2);
                        SysProxyHandle.SetIEProxy(true, true, $"{Global.Loopback}:{port}");
                    }
                    else if (type == ListenerType.GlobalPac)
                    {
                        string pacUrl = GetPacUrl();
                        //ProxySetting.SetProxy(pacUrl, "", 4);
                        SysProxyHandle.SetIEProxy(true, false, pacUrl);
                        //PACServerHandle.Stop();
                        PACServerHandle.Init(config);
                    }
                    else if (type == ListenerType.HttpOpenAndClear)
                    {
                        //PACServerHandle.Stop();
                        SysProxyHandle.ResetIEProxy();
                    }
                    else if (type == ListenerType.PacOpenAndClear)
                    {
                        string pacUrl = GetPacUrl();
                        SysProxyHandle.ResetIEProxy();
                        //PACServerHandle.Stop();
                        PACServerHandle.Init(config);
                    }
                    else if (type == ListenerType.HttpOpenOnly)
                    {
                        //PACServerHandle.Stop();
                        //SysProxyHandle.ResetIEProxy();
                    }
                    else if (type == ListenerType.PacOpenOnly)
                    {
                        string pacUrl = GetPacUrl();
                        //SysProxyHandle.ResetIEProxy();
                        //PACServerHandle.Stop();
                        PACServerHandle.Init(config);
                    }
                }
                else
                {
                    SysProxyHandle.ResetIEProxy();
                    //PACServerHandle.Stop();
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return true;
        }

        /// <summary>
        /// 启用系统代理(http)
        /// </summary>
        /// <param name="config"></param>
        private static void StartHttpAgent(Config config)
        {
            try
            {
                int localPort = config.GetLocalPort(Global.InboundSocks);
                if (localPort > 0)
                {
                    PrivoxyHandler.Instance.Restart(localPort, config);
                    if (PrivoxyHandler.Instance.RunningPort > 0)
                    {
                        Global.sysAgent = true;
                        Global.socksPort = localPort;
                        Global.httpPort = PrivoxyHandler.Instance.RunningPort;
                        Global.pacPort = config.GetLocalPort("pac");
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 关闭系统代理
        /// </summary>
        /// <param name="config"></param>
        public static void CloseHttpAgent(Config config)
        {
            try
            {
                if (config.listenerType != ListenerType.HttpOpenOnly && config.listenerType != ListenerType.PacOpenOnly)
                {
                    Update(config, true);
                }

                PrivoxyHandler.Instance.Stop();

                Global.sysAgent = false;
                Global.socksPort = 0;
                Global.httpPort = 0;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 重启系统代理(http)
        /// </summary>
        /// <param name="config"></param>
        /// <param name="forced"></param>
        public static Task RestartHttpAgent(Config config, bool forced)
        {
            return Task.Run(() =>
            { 
                bool isRestart = false;
                if (config.listenerType == ListenerType.noHttpProxy)
                {
                    // 关闭http proxy时，直接返回
                    return;
                }
                //强制重启或者socks端口变化
                if (forced)
                {
                    isRestart = true;
                }
                else
                {
                    int localPort = config.GetLocalPort(Global.InboundSocks);
                    if (localPort != Global.socksPort)
                    {
                        isRestart = true;
                    }
                }
                if (isRestart)
                {
                    CloseHttpAgent(config);
                    StartHttpAgent(config);
                }
                Update(config, false);
            });
        }

        public static string GetPacUrl()
        {
            string pacUrl = $"http://{Global.Loopback}:{Global.pacPort}/pac/?t={ DateTime.Now.ToString("HHmmss")}";
            return pacUrl;
        }
    }
}
