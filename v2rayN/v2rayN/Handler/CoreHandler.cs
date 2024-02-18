﻿using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using v2rayN.Model;
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

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utile.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utile.GetBinPath(""), EnvironmentVariableTarget.Process);
        }

        public void LoadCore()
        {
            var node = ConfigHandler.GetDefaultServer(_config);
            if (node == null)
            {
                ShowMsg(false, ResUI.CheckServerSettings);
                return;
            }

            string fileName = Utile.GetConfigPath(Global.CoreConfigFileName);
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
                    Utile.RemoveTunDevice();
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
            string configPath = Utile.GetConfigPath(Global.CoreSpeedtestConfigFileName);
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
                    var coreInfo = LazyConfig.Instance.GetCoreInfo();
                    foreach (var it in coreInfo)
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
                                if (path == $"{Utile.GetBinPath(vName, it.coreType.ToString())}.exe")
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

        #region Private

        private string CoreFindExe(CoreInfo coreInfo)
        {
            string fileName = string.Empty;
            foreach (string name in coreInfo.coreExes)
            {
                string vName = $"{name}.exe";
                vName = Utile.GetBinPath(vName, coreInfo.coreType.ToString());
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utile.IsNullOrEmpty(fileName))
            {
                string msg = string.Format(ResUI.NotFoundCore, Utile.GetBinPath("", coreInfo.coreType.ToString()), string.Join(", ", coreInfo.coreExes.ToArray()), coreInfo.coreUrl);
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
                    string fileName2 = Utile.GetConfigPath(Global.CorePreConfigFileName);
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
                var coreInfo = LazyConfig.Instance.GetCoreInfo(coreType);
                var proc = RunProcess(new(), coreInfo, $" -c {Global.CoreSpeedtestConfigFileName}", true, ShowMsg);
                if (proc is null)
                {
                    return -1;
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
                if (Utile.IsNullOrEmpty(fileName))
                {
                    return null;
                }
                Process proc = new()
                {
                    StartInfo = new()
                    {
                        FileName = fileName,
                        Arguments = string.Format(coreInfo.arguments, configPath),
                        WorkingDirectory = Utile.GetConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = displayLog,
                        RedirectStandardError = displayLog,
                        CreateNoWindow = true,
                        StandardOutputEncoding = displayLog ? Encoding.UTF8 : null,
                        StandardErrorEncoding = displayLog ? Encoding.UTF8 : null,
                    }
                };
                var startUpErrorMessage = new StringBuilder();
                var startUpSuccessful = false;
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

                            if (!startUpSuccessful)
                            {
                                startUpErrorMessage.Append(msg);
                            }
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
                    throw new Exception(displayLog ? startUpErrorMessage.ToString() : "启动进程失败并退出 (Failed to start the process and exited)");
                }
                else
                {
                    startUpSuccessful = true;
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