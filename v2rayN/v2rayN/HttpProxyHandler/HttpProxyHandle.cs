using System;
using v2rayN.Mode;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// 系统代理(http)总处理
    /// 启动privoxy提供http协议
    /// 使用SysProxy设置IE系统代理或者PAC模式
    /// </summary>
    class HttpProxyHandle
    {
        public static bool Update(Config config, bool forceDisable)
        {
            int type = config.listenerType;

            if (forceDisable)
            {
                type = 0;
            }

            try
            {
                if (type != 0)
                {
                    var port = Global.httpPort;
                    if (port <= 0)
                    {
                        return false;
                    }
                    if (type == 1)
                    {
                        PACServerHandle.Stop();
                        SysProxyHandle.SetIEProxy(true, true, $"{Global.Loopback}:{port}", null);
                    }
                    else if (type == 2)
                    {
                        string pacUrl = GetPacUrl();
                        SysProxyHandle.SetIEProxy(true, false, null, pacUrl);
                        PACServerHandle.Stop();
                        PACServerHandle.Init(config);
                    }
                    else if (type == 3)
                    {
                        PACServerHandle.Stop();
                        SysProxyHandle.SetIEProxy(false, false, null, null);
                    }
                    else if (type == 4)
                    {
                        string pacUrl = GetPacUrl();
                        SysProxyHandle.SetIEProxy(false, false, null, null);
                        PACServerHandle.Stop();
                        PACServerHandle.Init(config);
                    }
                }
                else
                {
                    SysProxyHandle.SetIEProxy(false, false, null, null);
                    PACServerHandle.Stop();
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
        public static void StartHttpAgent(Config config)
        {
            try
            {
                int localPort = config.GetLocalPort(Global.InboundSocks);
                if (localPort > 0)
                {
                    PrivoxyHandler.Instance.Start(localPort, config);
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
                PrivoxyHandler.Instance.Stop();

                Global.sysAgent = false;
                Global.socksPort = 0;
                Global.httpPort = 0;
                Global.pacPort = 0;
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
        public static bool RestartHttpAgent(Config config, bool forced)
        {
            bool isRestart = false;
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
                return true;
            }
            return false;
        }

        public static string GetPacUrl()
        {
            string pacUrl = $"http://{Global.Loopback}:{Global.pacPort}/pac/?t={ DateTime.Now.ToString("HHmmss")}";
            return pacUrl;
        }
    }
}
