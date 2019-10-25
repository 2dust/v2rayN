﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using v2rayN.Mode;
using v2rayN.Properties;
using v2rayN.Tool;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// Privoxy处理类，提供http协议代理
    /// </summary>
    class PrivoxyHandler
    {
        /// <summary>
        /// 单例
        /// </summary>
        private static PrivoxyHandler instance;

        private static int _uid;
        private static string _uniqueConfigFile;
        private static Job _privoxyJob;
        private Process _process;

        static PrivoxyHandler()
        {
            try
            {
                _uid = Application.StartupPath.GetHashCode(); // Currently we use ss's StartupPath to identify different Privoxy instance.
                _uniqueConfigFile = string.Format("privoxy_{0}.conf", _uid);
                _privoxyJob = new Job();

                FileManager.UncompressFile(Utils.GetTempPath("v2ray_privoxy.exe"), Resources.privoxy_exe);
            }
            catch (IOException ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private PrivoxyHandler()
        {

        }

        /// <summary>
        /// 单例
        /// </summary>
        public static PrivoxyHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PrivoxyHandler();
                }
                return instance;
            }
        }

        public int RunningPort
        {
            get; set;
        }

        public void Start(int localPort, Config config)
        {
            if (_process == null)
            {
                Process[] existingPrivoxy = Process.GetProcessesByName("v2ray_privoxy");
                foreach (Process p in existingPrivoxy.Where(IsChildProcess))
                {
                    KillProcess(p);
                }
                string privoxyConfig = Resources.privoxy_conf;
                RunningPort = config.GetLocalPort(Global.InboundHttp);
                privoxyConfig = privoxyConfig.Replace("__SOCKS_PORT__", localPort.ToString());
                privoxyConfig = privoxyConfig.Replace("__PRIVOXY_BIND_PORT__", RunningPort.ToString());
                if (config.allowLANConn)
                {
                    privoxyConfig = privoxyConfig.Replace("__PRIVOXY_BIND_IP__", "0.0.0.0");
                }
                else
                {
                    privoxyConfig = privoxyConfig.Replace("__PRIVOXY_BIND_IP__", Global.Loopback);
                }
                FileManager.ByteArrayToFile(Utils.GetTempPath(_uniqueConfigFile), Encoding.UTF8.GetBytes(privoxyConfig));

                _process = new Process
                {
                    // Configure the process using the StartInfo properties.
                    StartInfo =
                    {
                        FileName = "v2ray_privoxy.exe",
                        Arguments = _uniqueConfigFile,
                        WorkingDirectory = Utils.GetTempPath(),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };
                _process.Start();

                /*
                 * Add this process to job obj associated with this ss process, so that
                 * when ss exit unexpectedly, this process will be forced killed by system.
                 */
                _privoxyJob.AddProcess(_process.Handle);

            }
        }

        public void Stop()
        {
            if (_process != null)
            {
                KillProcess(_process);
                _process.Dispose();
                _process = null;
                RunningPort = 0;
            }
        }

        private static void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        /*
         * We won't like to kill other ss instances' v2ray_privoxy.exe.
         * This function will check whether the given process is created
         * by this process by checking the module path or command line.
         * 
         * Since it's required to put ss in different dirs to run muti instances,
         * different instance will create their unique "privoxy_UID.conf" where
         * UID is hash of ss's location.
         */

        private static bool IsChildProcess(Process process)
        {
            try
            {
                /*
                 * Under PortableMode, we could identify it by the path of v2ray_privoxy.exe.
                 */
                var path = process.MainModule.FileName;

                return Utils.GetTempPath("v2ray_privoxy.exe").Equals(path);

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                /*
                 * Sometimes Process.GetProcessesByName will return some processes that
                 * are already dead, and that will cause exceptions here.
                 * We could simply ignore those exceptions.
                 */
                //Logging.LogUsefulException(ex);
                return false;
            }
        }

    }
}
