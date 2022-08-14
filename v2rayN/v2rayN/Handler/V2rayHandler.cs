using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{

    /// <summary>
    /// 消息委托
    /// </summary>
    /// <param name="notify">是否显示在托盘区</param>
    /// <param name="msg">内容</param>
    public delegate void ProcessDelegate(bool notify, string msg);

    /// <summary>
    /// v2ray进程处理类
    /// </summary>
    class V2rayHandler
    {
        private static string v2rayConfigRes = Global.v2rayConfigFileName;
        private CoreInfo coreInfo;
        public event ProcessDelegate ProcessEvent;
        private int processId = 0;
        private Process _process;

        public V2rayHandler()
        {
        }

        /// <summary>
        /// 载入V2ray
        /// </summary>
        public void LoadV2ray(Config config)
        {
            if (Global.reloadV2ray)
            {
                var item = ConfigHandler.GetDefaultServer(ref config);
                if (item == null)
                {
                    ShowMsg(false, ResUI.CheckServerSettings);
                    return;
                }

                if (SetCore(config, item) != 0)
                {
                    ShowMsg(false, ResUI.CheckServerSettings);
                    return;
                }
                string fileName = Utils.GetPath(v2rayConfigRes);
                if (V2rayConfigHandler.GenerateClientConfig(item, fileName, out string msg, out string content) != 0)
                {
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(false, msg);
                    ShowMsg(true, $"[{config.GetGroupRemarks(item.groupId)}] {item.GetSummary()}");
                    V2rayRestart();
                }

                //start a socks service
                if (item.configType == EConfigType.Custom && item.preSocksPort > 0)
                {
                    var itemSocks = new VmessItem()
                    {
                        configType = EConfigType.Socks,
                        address = Global.Loopback,
                        port = item.preSocksPort
                    };
                    if (V2rayConfigHandler.GenerateClientConfig(itemSocks, null, out string msg2, out string configStr) == 0)
                    {
                        processId = V2rayStartNew(configStr);
                    }
                }
            }
        }

        /// <summary>
        /// 新建进程，载入V2ray配置文件字符串
        /// 返回新进程pid。
        /// </summary>
        public int LoadV2rayConfigString(Config config, List<ServerTestItem> _selecteds)
        {
            int pid = -1;
            string configStr = V2rayConfigHandler.GenerateClientSpeedtestConfigString(config, _selecteds, out string msg);
            if (configStr == "")
            {
                ShowMsg(false, msg);
            }
            else
            {
                ShowMsg(false, msg);
                pid = V2rayStartNew(configStr);
                //V2rayRestart();
                // start with -config
            }
            return pid;
        }

        /// <summary>
        /// V2ray重启
        /// </summary>
        private void V2rayRestart()
        {
            V2rayStop();
            V2rayStart();
        }

        /// <summary>
        /// V2ray停止
        /// </summary>
        public void V2rayStop()
        {
            try
            {
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }
                else
                {
                    if (coreInfo == null || coreInfo.coreExes == null)
                    {
                        return;
                    }
                    foreach (string vName in coreInfo.coreExes)
                    {
                        Process[] existing = Process.GetProcessesByName(vName);
                        foreach (Process p in existing)
                        {
                            string path = p.MainModule.FileName;
                            if (path == $"{Utils.GetPath(vName)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                if (processId > 0)
                {
                    V2rayStopPid(processId);
                    processId = 0;
                }

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }
        /// <summary>
        /// V2ray停止
        /// </summary>
        public void V2rayStopPid(int pid)
        {
            try
            {
                Process _p = Process.GetProcessById(pid);
                KillProcess(_p);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private string V2rayFindexe(List<string> lstCoreTemp)
        {
            string fileName = string.Empty;
            foreach (string name in lstCoreTemp)
            {
                string vName = $"{name}.exe";
                vName = Utils.GetPath(vName);
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                string msg = string.Format(ResUI.NotFoundCore, string.Join(", ", lstCoreTemp.ToArray()), coreInfo.coreUrl);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        /// <summary>
        /// V2ray启动
        /// </summary>
        private void V2rayStart()
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = V2rayFindexe(coreInfo.coreExes);
                if (fileName == "") return;

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = coreInfo.arguments,
                        WorkingDirectory = Utils.StartupPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                p.OutputDataReceived += (sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        string msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                };
                p.Start();
                p.PriorityClass = ProcessPriorityClass.High;
                p.BeginOutputReadLine();
                _process = p;

                if (p.WaitForExit(1000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }

                Global.processJob.AddProcess(p.Handle);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                ShowMsg(true, msg);
            }
        }
        /// <summary>
        /// V2ray启动，新建进程，传入配置字符串
        /// </summary>
        private int V2rayStartNew(string configStr)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = V2rayFindexe(new List<string> { "xray", "wv2ray", "v2ray" });
                if (fileName == "") return -1;

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "-config stdin:",
                        WorkingDirectory = Utils.StartupPath(),
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                p.OutputDataReceived += (sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        string msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                };
                p.Start();
                p.BeginOutputReadLine();

                p.StandardInput.Write(configStr);
                p.StandardInput.Close();

                if (p.WaitForExit(1000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }

                Global.processJob.AddProcess(p.Handle);
                return p.Id;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                ShowMsg(false, msg);
                return -1;
            }
        }

        /// <summary>
        /// 消息委托
        /// </summary>
        /// <param name="updateToTrayTooltip">是否更新托盘图标的工具提示</param>
        /// <param name="msg">输出到日志框</param>
        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            ProcessEvent?.Invoke(updateToTrayTooltip, msg);
        }

        private void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private int SetCore(Config config, VmessItem item)
        {
            if (item == null)
            {
                return -1;
            }
            var coreType = LazyConfig.Instance.GetCoreType(item, item.configType);

            coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);

            if (coreInfo == null)
            {
                return -1;
            }
            return 0;
        }
    }
}
