using System;
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
        HttpOpenAndClear = 2,
        HttpOpenOnly = 3,
    }
    /// <summary>
    /// 系统代理(http)总处理
    /// 启动privoxy提供http协议
    /// 设置IE系统代理
    /// </summary>
    class HttpProxyHandle
    {
        private static bool Update(Config config, bool forceDisable)
        {
            // ListenerType type = config.listenerType;
            var type = ListenerType.noHttpProxy;
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
                        //ProxySetting.SetProxy($"{Global.Loopback}:{port}", Global.IEProxyExceptions, 2);
                        SysProxyHandle.SetIEProxy(true, true, $"{Global.Loopback}:{port}");
                    }
                    else if (type == ListenerType.HttpOpenAndClear)
                    {
                        SysProxyHandle.ResetIEProxy();
                    }
                    else if (type == ListenerType.HttpOpenOnly)
                    {
                        //SysProxyHandle.ResetIEProxy();
                    }
                }
                else
                {
                    SysProxyHandle.ResetIEProxy();
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
                //if (config.listenerType != ListenerType.HttpOpenOnly)
                //{
                //    Update(config, true);
                //}

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
        public static void RestartHttpAgent(Config config, bool forced)
        {
            bool isRestart = false;
            //if (config.listenerType == ListenerType.noHttpProxy)
            //{
            //    // 关闭http proxy时，直接返回
            //    return;
            //}
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
        }

        public static bool UpdateSysProxy(Config config, bool forceDisable)
        {
            var type = config.sysProxyType;

            if (forceDisable && type == ESysProxyType.ForcedChange)
            {
                type = ESysProxyType.ForcedClear;
            }

            try
            {
                Global.httpPort = config.GetLocalPort(Global.InboundHttp);
                int port = Global.httpPort;
                if (port <= 0)
                {
                    return false;
                }
                if (type == ESysProxyType.ForcedChange)
                {
                    SysProxyHandle.SetIEProxy(true, true, $"{Global.Loopback}:{port}");
                }
                else if (type == ESysProxyType.ForcedClear)
                {
                    SysProxyHandle.ResetIEProxy();
                }
                else if (type == ESysProxyType.Unchanged)
                {
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
