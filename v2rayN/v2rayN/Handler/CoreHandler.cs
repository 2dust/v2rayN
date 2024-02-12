using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
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
            var node = ConfigHandler.GetDefaultServer(_config);
            if (node == null)
            {
                ShowMsg(false, ResUI.CheckServerSettings);
                return;
            }

            string fileName = Utils.GetConfigPath(Global.CoreConfigFileName);
            if (CoreConfigHandler.GenerateClientConfig(node, fileName, out string msg, out string content) != 0)
            {
                ShowMsg(false, msg);
            }
            else
            {
                ShowMsg(false, msg);
                ShowMsg(true, $"{node.GetSummary()}");
                CoreStop();
                if (_config.tunModeItem.enableTun)
                {
                    Thread.Sleep(1000);
                    Utils.RemoveTunDevice();
                }

                CoreStart(node);

                //In tun mode, do a delay check and restart the core
                if (_config.tunModeItem.enableTun)
                {
                    Observable.Range(1, 1)
                    .Delay(TimeSpan.FromSeconds(15))
                    .Subscribe(x =>
                    {
                        {
                            if (_process == null || _process.HasExited)
                            {
                                CoreStart(node);
                                ShowMsg(false, "Tun mode restart the core once");
                                Logging.SaveLog("Tun mode restart the core once");
                            }
                        }
                    });
                }
            }
        }

        public int LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds)
        {
            int pid = -1;
            var coreType = selecteds.Exists(t => t.configType == EConfigType.Hysteria2 || t.configType == EConfigType.Tuic || t.configType == EConfigType.Wireguard) ? ECoreType.sing_box : ECoreType.Xray;
            string configPath = Utils.GetConfigPath(Global.CoreSpeedtestConfigFileName);
            if (CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType, out string msg) != 0)
            {
                ShowMsg(false, msg);
            }
            else
            {
                ShowMsg(false, msg);
                pid = CoreStartSpeedtest(configPath, coreType);
            }
            return pid;
        }

        public void CoreStop()
        {
            try
            {
                bool hasProc = false;
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                    hasProc = true;
                }

                if (_processPre != null)
                {
                    KillProcess(_processPre);
                    _processPre.Dispose();
                    _processPre = null;
                    hasProc = true;
                }

                if (!hasProc)
                {
                    var coreInfos = LazyConfig.Instance.GetCoreInfos();
                    foreach (var it in coreInfos)
                    {
                        if (it.coreType == ECoreType.v2rayN)
                        {
                            continue;
                        }
                        foreach (string vName in it.coreExes)
                        {
                            var existing = Process.GetProcessesByName(vName);
                            foreach (Process p in existing)
                            {
                                string? path = p.MainModule?.FileName;
                                if (path == $"{Utils.GetBinPath(vName, it.coreType.ToString())}.exe")
                                {
                                    KillProcess(p);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void CoreStopPid(int pid)
        {
            try
            {
                var _p = Process.GetProcessById(pid);
                KillProcess(_p);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private string CoreFindexe(CoreInfo coreInfo)
        {
            string fileName = string.Empty;
            foreach (string name in coreInfo.coreExes)
            {
                string vName = $"{name}.exe";
                vName = Utils.GetBinPath(vName, coreInfo.coreType.ToString());
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                string msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo.coreType.ToString()), string.Join(", ", coreInfo.coreExes.ToArray()), coreInfo.coreUrl);
                Logging.SaveLog(msg);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        private void CoreStart(ProfileItem node)
        {
            ShowMsg(false, $"{Environment.OSVersion} - {(Environment.Is64BitOperatingSystem ? 64 : 32)}");
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));

            ECoreType coreType;
            if (node.configType != EConfigType.Custom && _config.tunModeItem.enableTun)
            {
                coreType = ECoreType.sing_box;
            }
            else
            {
                coreType = LazyConfig.Instance.GetCoreType(node, node.configType);
            }
            var coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);

            var displayLog = node.configType != EConfigType.Custom || node.displayLog;
            var proc = RunProcess(node, coreInfo, "", displayLog, ShowMsg);
            if (proc is null)
            {
                return;
            }
            _process = proc;

            //start a socks service
            if (_process != null && !_process.HasExited)
            {
                if ((node.configType == EConfigType.Custom && node.preSocksPort > 0))
                {
                    var itemSocks = new ProfileItem()
                    {
                        coreType = ECoreType.sing_box,
                        configType = EConfigType.Socks,
                        address = Global.Loopback,
                        port = node.preSocksPort
                    };
                    string fileName2 = Utils.GetConfigPath(Global.CorePreConfigFileName);
                    if (CoreConfigHandler.GenerateClientConfig(itemSocks, fileName2, out string msg2, out string configStr) == 0)
                    {
                        var coreInfo2 = LazyConfig.Instance.GetCoreInfo(ECoreType.sing_box);
                        var proc2 = RunProcess(node, coreInfo2, $" -c {Global.CorePreConfigFileName}", true, ShowMsg);
                        if (proc2 is not null)
                        {
                            _processPre = proc2;
                        }
                    }
                }
            }
        }

        private int CoreStartSpeedtest(string configPath, ECoreType coreType)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));

            ShowMsg(false, configPath);
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

                return proc.Id;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                ShowMsg(false, msg);
                return -1;
            }
        }

        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            _updateFunc(updateToTrayTooltip, msg);
        }

        #endregion Private

        #region Process

        private Process? RunProcess(ProfileItem node, CoreInfo coreInfo, string configPath, bool displayLog, Action<bool, string> update)
        {
            try
            {
                string fileName = CoreFindExe(coreInfo);
                if (Utils.IsNullOrEmpty(fileName))
                {
                    return null;
                }
                Process proc = new()
                {
                    StartInfo = new()
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
                    proc.CancelErrorRead();
                    throw new Exception(displayLog ? proc.StandardError.ReadToEnd() : "启动进程失败并退出 (Failed to start the process and exited)");
                }

                LazyConfig.Instance.AddProcess(proc.Handle);
                return proc;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                string msg = ex.Message;
                update(true, msg);
                return null;
            }
        }

        private void KillProcess(Process? proc)
        {
            if (proc is null)
            {
                return;
            }
            try
            {
                proc.CloseMainWindow();
                proc.WaitForExit(100);
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        #endregion Process
    }
}