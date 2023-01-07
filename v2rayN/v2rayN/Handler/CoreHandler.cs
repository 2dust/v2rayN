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
        private static string _coreCConfigRes = Global.coreConfigFileName;
        private CoreInfo _coreInfo;
        private int _processId = 0;
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
                string fileName = Utils.GetConfigPath(_coreCConfigRes);
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
                        _processId = CoreStartViaString(configStr);
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
                    if (_coreInfo == null || _coreInfo.coreExes == null)
                    {
                        return;
                    }
                    foreach (string vName in _coreInfo.coreExes)
                    {
                        Process[] existing = Process.GetProcessesByName(vName);
                        foreach (Process p in existing)
                        {
                            string path = p.MainModule.FileName;
                            if (path == $"{Utils.GetBinPath(vName, _coreInfo.coreType)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                if (_processId > 0)
                {
                    CoreStopPid(_processId);
                    _processId = 0;
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

        private string CoreFindexe(CoreInfo coreInfo)
        {
            string fileName = string.Empty;
            foreach (string name in coreInfo.coreExes)
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
                string msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo.coreType), string.Join(", ", coreInfo.coreExes.ToArray()), coreInfo.coreUrl);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        private void CoreStart(ProfileItem node)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            try
            {
                string fileName = CoreFindexe(_coreInfo);
                if (fileName == "") return;

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = _coreInfo.arguments,
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
                var coreInfo = LazyConfig.Instance.GetCoreInfo(ECoreType.Xray);
                string fileName = CoreFindexe(coreInfo);
                if (fileName == "") return -1;

                var pathTemp = Utils.GetConfigPath($"temp_{Utils.GetGUID(false)}.json");
                File.WriteAllText(pathTemp, configStr);
                if (!File.Exists(pathTemp))
                {
                    return -1;
                }

                Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = $"-config \"{pathTemp}\"",
                        WorkingDirectory = Utils.GetConfigPath(),
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
                p.BeginOutputReadLine();

                if (p.WaitForExit(1000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }

                Global.processJob.AddProcess(p.Handle);

                Thread.Sleep(1000);
                File.Delete(pathTemp);
                
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

            _coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);

            if (_coreInfo == null)
            {
                return -1;
            }
            return 0;
        }
    }
}
