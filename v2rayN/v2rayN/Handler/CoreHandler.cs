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
    internal class CoreHandler
    {
        private Config _config;
        private CoreInfo? _coreInfo;
        private Process? _process;
        private Process? _processPre;
        private Action<bool, string> _updateFunc;

        public CoreHandler(Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        }

        public void LoadCore()
        {
            var node = ConfigHandler.GetDefaultServer(ref _config);
            if (node == null)
            {
                ShowMsg(false, ResUI.CheckServerSettings);
                return;
            }

            if (SetCore(node) != 0)
            {
                ShowMsg(false, ResUI.CheckServerSettings);
                return;
            }
            string fileName = Utils.GetConfigPath(Global.coreConfigFileName);
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
        }

        public int LoadCoreConfigString(List<ServerTestItem> _selecteds)
        {
            int pid = -1;
            string configStr = CoreConfigHandler.GenerateClientSpeedtestConfigString(_config, _selecteds, out string msg);
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
                            string? path = p.MainModule?.FileName;
                            if (path == $"{Utils.GetBinPath(vName, _coreInfo.coreType)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                if (_processPre != null)
                {
                    KillProcess(_processPre);
                    _processPre.Dispose();
                    _processPre = null;
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
                Utils.SaveLog(msg);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        private void CoreStart(ProfileItem node)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));

            var proc = RunProcess(node, _coreInfo, "", ShowMsg);
            if (proc is null)
            {
                return;
            }
            _process = proc;

            //start a socks service
            if (_process != null && !_process.HasExited)
            {
                if ((node.configType == EConfigType.Custom && node.preSocksPort > 0)
                    || (node.configType != EConfigType.Custom && _coreInfo.coreType != ECoreType.sing_box && _config.tunModeItem.enableTun))
                {
                    var itemSocks = new ProfileItem()
                    {
                        coreType = ECoreType.sing_box,
                        configType = EConfigType.Socks,
                        address = Global.Loopback,
                        port = node.preSocksPort > 0 ? node.preSocksPort : LazyConfig.Instance.GetLocalPort(Global.InboundSocks)
                    };
                    string fileName2 = Utils.GetConfigPath(Global.corePreConfigFileName);
                    if (CoreConfigHandler.GenerateClientConfig(itemSocks, fileName2, out string msg2, out string configStr) == 0)
                    {
                        var coreInfo = LazyConfig.Instance.GetCoreInfo(ECoreType.sing_box);
                        var proc2 = RunProcess(node, coreInfo, $" -c {Global.corePreConfigFileName}", ShowMsg);
                        if (proc2 is not null)
                        {
                            _processPre = proc2;
                        }
                    }
                }
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

                Process p = new()
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
                p.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

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

        private int SetCore(ProfileItem node)
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

        #region Process

        private Process? RunProcess(ProfileItem node, CoreInfo coreInfo, string configPath, Action<bool, string> update)
        {
            try
            {
                string fileName = CoreFindexe(coreInfo);
                if (Utils.IsNullOrEmpty(fileName))
                {
                    return null;
                }
                var displayLog = node.configType != EConfigType.Custom || node.displayLog;
                Process proc = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = string.Format(coreInfo.arguments, configPath),
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = displayLog,
                        RedirectStandardError = displayLog,
                        CreateNoWindow = true,
                        StandardOutputEncoding = displayLog ? Encoding.UTF8 : null,
                        StandardErrorEncoding = displayLog ? Encoding.UTF8 : null,
                    }
                };
                if (displayLog)
                {
                    proc.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            string msg = e.Data + Environment.NewLine;
                            update(false, msg);
                        }
                    };
                    proc.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            string msg = e.Data + Environment.NewLine;
                            update(false, msg);
                        }
                    };
                }
                proc.Start();
                if (displayLog)
                {
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                }

                if (proc.WaitForExit(1000))
                {
                    throw new Exception(displayLog ? proc.StandardError.ReadToEnd() : "启动进程失败并退出 (Failed to start the process and exited)");
                }

                Global.processJob.AddProcess(proc.Handle);
                return proc;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                update(true, msg);
                return null;
            }
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

        #endregion Process
    }
}