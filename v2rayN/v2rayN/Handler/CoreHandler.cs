using System.Diagnostics;
using System.IO;
using System.Text;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    /// <summary>
    /// Core process processing class
    /// </summary>
    class CoreHandler
    {
        private static string coreCConfigRes = Global.coreConfigFileName;
        private CoreInfo coreInfo;
        private int processId = 0;
        private Process _process;
        Action<bool, string> _updateFunc;

        public CoreHandler(Action<bool, string> update)
        {
            _updateFunc = update;
        }

        public void LoadCore(Config config)
        {
            if (Global.reloadCore)
            {
                var node = ConfigHandler.GetDefaultServer(ref config);
                if (node == null)
                {
                    ShowMsg(false, ResUI.CheckServerSettings);
                    return;
                }

                if (SetCore(config, node) != 0)
                {
                    ShowMsg(false, ResUI.CheckServerSettings);
                    return;
                }
                string fileName = Utils.GetConfigPath(coreCConfigRes);
                if (CoreConfigHandler.GenerateClientConfig(node, fileName, out string msg, out string content) != 0)
                {
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(false, msg);
                    ShowMsg(true, $"{node.GetSummary()}");
                    CoreStop();
                    CoreStart(node);
                }

                //start a socks service
                if (_process != null && !_process.HasExited && node.configType == EConfigType.Custom && node.preSocksPort > 0)
                {
                    var itemSocks = new ProfileItem()
                    {
                        configType = EConfigType.Socks,
                        address = Global.Loopback,
                        port = node.preSocksPort
                    };
                    if (CoreConfigHandler.GenerateClientConfig(itemSocks, null, out string msg2, out string configStr) == 0)
                    {
                        processId = CoreStartViaString(configStr);
                    }
                }
            }
        }

        public int LoadCoreConfigString(Config config, List<ServerTestItem> _selecteds)
        {
            int pid = -1;
            string configStr = CoreConfigHandler.GenerateClientSpeedtestConfigString(config, _selecteds, out string msg);
            if (configStr == "")
            {
                ShowMsg(false, msg);
            }
            else
            {
                ShowMsg(false, msg);
                pid = CoreStartViaString(configStr);
            }
            return pid;
        }

        public void CoreStop()
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
                            if (path == $"{Utils.GetBinPath(vName, coreInfo.coreType)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                if (processId > 0)
                {
                    CoreStopPid(processId);
                    processId = 0;
                }

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void CoreStopPid(int pid)
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

        private string CoreFindexe(List<string> lstCoreTemp)
        {
            string fileName = string.Empty;
            foreach (string name in lstCoreTemp)
            {
                string vName = $"{name}.exe";
                vName = Utils.GetBinPath(vName, coreInfo.coreType);
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                string msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo.coreType), string.Join(", ", lstCoreTemp.ToArray()), coreInfo.coreUrl);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        private void CoreStart(ProfileItem node)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = CoreFindexe(coreInfo.coreExes);
                if (fileName == "") return;

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = coreInfo.arguments,
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = node.displayLog,
                        RedirectStandardError = node.displayLog,
                        CreateNoWindow = true,
                        StandardOutputEncoding = node.displayLog ? Encoding.UTF8 : null,
                        StandardErrorEncoding = node.displayLog ? Encoding.UTF8 : null,
                    }
                };
                if (node.displayLog)
                {
                    p.OutputDataReceived += (sender, e) =>
                    {
                        if (!String.IsNullOrEmpty(e.Data))
                        {
                            string msg = e.Data + Environment.NewLine;
                            ShowMsg(false, msg);
                        }
                    };
                }
                p.Start();
                if (node.displayLog)
                {
                    p.BeginOutputReadLine();
                }
                _process = p;

                if (p.WaitForExit(1000))
                {
                    throw new Exception(node.displayLog ? p.StandardError.ReadToEnd() : "启动进程失败并退出 (Failed to start the process and exited)");
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

        private int CoreStartViaString(string configStr)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = CoreFindexe(new List<string> { "xray", "wxray", "wv2ray", "v2ray" });
                if (fileName == "") return -1;

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "-config stdin:",
                        WorkingDirectory = Utils.GetConfigPath(),
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

        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            _updateFunc(updateToTrayTooltip, msg);
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

        private int SetCore(Config config, ProfileItem node)
        {
            if (node == null)
            {
                return -1;
            }
            var coreType = LazyConfig.Instance.GetCoreType(node, node.configType);

            coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);

            if (coreInfo == null)
            {
                return -1;
            }
            return 0;
        }
    }
}
