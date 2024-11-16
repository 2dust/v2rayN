using System.Diagnostics;
using System.Text;

namespace ServiceLib.Handler
{
    /// <summary>
    /// Core process processing class
    /// </summary>
    public class CoreHandler
    {
        private static readonly Lazy<CoreHandler> _instance = new(() => new());
        public static CoreHandler Instance => _instance.Value;
        private Config _config;
        private Process? _process;
        private Process? _processPre;
        private Action<bool, string>? _updateFunc;

        public async Task Init(Config config, Action<bool, string> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;

            Environment.SetEnvironmentVariable("V2RAY_LOCATION_ASSET", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("XRAY_LOCATION_ASSET", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);

            if (Utils.IsLinux())
            {
                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo();
                foreach (var it in coreInfo)
                {
                    if (it.CoreType == ECoreType.v2rayN)
                    {
                        if (Utils.UpgradeAppExists(out var fileName))
                        {
                            await Utils.SetLinuxChmod(fileName);
                        }
                        continue;
                    }

                    foreach (var name in it.CoreExes)
                    {
                        var exe = Utils.GetBinPath(Utils.GetExeName(name), it.CoreType.ToString());
                        if (File.Exists(exe))
                        {
                            await Utils.SetLinuxChmod(exe);
                        }
                    }
                }
            }
        }

        public async Task LoadCore(ProfileItem? node)
        {
            if (node == null)
            {
                ShowMsg(false, ResUI.CheckServerSettings);
                return;
            }

            var fileName = Utils.GetConfigPath(Global.CoreConfigFileName);
            var result = await CoreConfigHandler.GenerateClientConfig(node, fileName);
            ShowMsg(true, result.Msg);
            if (result.Success != true)
            {
                return;
            }
            else
            {
                ShowMsg(true, $"{node.GetSummary()}");
                await CoreStop();
                await Task.Delay(100);
                await CoreStart(node);

                //In tun mode, do a delay check and restart the core
                //if (_config.tunModeItem.enableTun)
                //{
                //    Observable.Range(1, 1)
                //    .Delay(TimeSpan.FromSeconds(15))
                //    .Subscribe(x =>
                //    {
                //        {
                //            if (_process == null || _process.HasExited)
                //            {
                //                CoreStart(node);
                //                ShowMsg(false, "Tun mode restart the core once");
                //                Logging.SaveLog("Tun mode restart the core once");
                //            }
                //        }
                //    });
                //}
            }
        }

        public async Task<int> LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds)
        {
            var pid = -1;
            var coreType = selecteds.Exists(t => t.ConfigType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard) ? ECoreType.sing_box : ECoreType.Xray;
            var configPath = Utils.GetConfigPath(Global.CoreSpeedtestConfigFileName);
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType);
            ShowMsg(false, result.Msg);
            if (result.Success)
            {
                pid = await CoreStartSpeedtest(configPath, coreType);
            }
            return pid;
        }

        public async Task CoreStop()
        {
            try
            {
                if (_process != null)
                {
                    await KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }

                if (_processPre != null)
                {
                    await KillProcess(_processPre);
                    _processPre.Dispose();
                    _processPre = null;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public async Task CoreStopPid(int pid)
        {
            try
            {
                var _p = Process.GetProcessById(pid);
                await KillProcess(_p);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        #region Private

        private string CoreFindExe(CoreInfo coreInfo)
        {
            var fileName = string.Empty;
            foreach (var name in coreInfo.CoreExes)
            {
                var vName = Utils.GetBinPath(Utils.GetExeName(name), coreInfo.CoreType.ToString());
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                var msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo.CoreType.ToString()), string.Join(", ", coreInfo.CoreExes.ToArray()), coreInfo.Url);
                Logging.SaveLog(msg);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        private async Task CoreStart(ProfileItem node)
        {
            ShowMsg(false, $"{Environment.OSVersion} - {(Environment.Is64BitOperatingSystem ? 64 : 32)}");
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));

            var coreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
            _config.RunningCoreType = coreType;
            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(coreType);

            var displayLog = node.ConfigType != EConfigType.Custom || node.DisplayLog;
            var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true);
            if (proc is null)
            {
                return;
            }
            _process = proc;

            //start a pre service
            if (_process != null && !_process.HasExited)
            {
                ProfileItem? itemSocks = null;
                var preCoreType = ECoreType.sing_box;
                if (node.ConfigType != EConfigType.Custom && coreType != ECoreType.sing_box && _config.TunModeItem.EnableTun)
                {
                    itemSocks = new ProfileItem()
                    {
                        CoreType = preCoreType,
                        ConfigType = EConfigType.SOCKS,
                        Address = Global.Loopback,
                        Sni = node.Address, //Tun2SocksAddress
                        Port = AppHandler.Instance.GetLocalPort(EInboundProtocol.socks)
                    };
                }
                else if ((node.ConfigType == EConfigType.Custom && node.PreSocksPort > 0))
                {
                    preCoreType = _config.TunModeItem.EnableTun ? ECoreType.sing_box : ECoreType.Xray;
                    itemSocks = new ProfileItem()
                    {
                        CoreType = preCoreType,
                        ConfigType = EConfigType.SOCKS,
                        Address = Global.Loopback,
                        Port = node.PreSocksPort.Value,
                    };
                    _config.RunningCoreType = preCoreType;
                }
                if (itemSocks != null)
                {
                    var fileName2 = Utils.GetConfigPath(Global.CorePreConfigFileName);
                    var result = await CoreConfigHandler.GenerateClientConfig(itemSocks, fileName2);
                    if (result.Success)
                    {
                        var coreInfo2 = CoreInfoHandler.Instance.GetCoreInfo(preCoreType);
                        var proc2 = await RunProcess(coreInfo2, Global.CorePreConfigFileName, true, true);
                        if (proc2 is not null)
                        {
                            _processPre = proc2;
                        }
                    }
                }
            }
        }

        private async Task<int> CoreStartSpeedtest(string configPath, ECoreType coreType)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));

            ShowMsg(false, configPath);
            try
            {
                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(coreType);
                var proc = await RunProcess(coreInfo, Global.CoreSpeedtestConfigFileName, true, false);
                if (proc is null)
                {
                    return -1;
                }

                return proc.Id;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                ShowMsg(false, ex.Message);
                return -1;
            }
        }

        private void ShowMsg(bool notify, string msg)
        {
            _updateFunc?.Invoke(notify, msg);
        }

        private bool IsNeedSudo(ECoreType eCoreType)
        {
            return _config.TunModeItem.EnableTun
                   && eCoreType == ECoreType.sing_box
                   && Utils.IsLinux()
                   && _config.TunModeItem.LinuxSudoPwd.IsNotEmpty()
                ;
        }

        #endregion Private

        #region Process

        private async Task<Process?> RunProcess(CoreInfo coreInfo, string configPath, bool displayLog, bool mayNeedSudo)
        {
            var fileName = CoreFindExe(coreInfo);
            if (Utils.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var isNeedSudo = mayNeedSudo && IsNeedSudo(coreInfo.CoreType);
            try
            {
                Process proc = new()
                {
                    StartInfo = new()
                    {
                        FileName = fileName,
                        Arguments = string.Format(coreInfo.Arguments, configPath),
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = displayLog,
                        RedirectStandardError = displayLog,
                        CreateNoWindow = true,
                        StandardOutputEncoding = displayLog ? Encoding.UTF8 : null,
                        StandardErrorEncoding = displayLog ? Encoding.UTF8 : null,
                    }
                };

                if (isNeedSudo)
                {
                    proc.StartInfo.FileName = $"/bin/sudo";
                    proc.StartInfo.Arguments = $"-S {fileName} {string.Format(coreInfo.Arguments, Utils.GetConfigPath(configPath))}";
                    proc.StartInfo.WorkingDirectory = null;
                    proc.StartInfo.StandardInputEncoding = Encoding.UTF8;
                    proc.StartInfo.RedirectStandardInput = true;
                }

                var startUpErrorMessage = new StringBuilder();
                var startUpSuccessful = false;
                if (displayLog)
                {
                    proc.OutputDataReceived += (sender, e) =>
                    {
                        if (Utils.IsNullOrEmpty(e.Data)) return;
                        ShowMsg(false, e.Data + Environment.NewLine);
                    };
                    proc.ErrorDataReceived += (sender, e) =>
                    {
                        if (Utils.IsNullOrEmpty(e.Data)) return;
                        ShowMsg(false, e.Data + Environment.NewLine);

                        if (!startUpSuccessful)
                        {
                            startUpErrorMessage.Append(e.Data + Environment.NewLine);
                        }
                    };
                }
                proc.Start();

                if (isNeedSudo)
                {
                    var pwd = DesUtils.Decrypt(_config.TunModeItem.LinuxSudoPwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                }

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

                AppHandler.Instance.AddProcess(proc.Handle);
                return proc;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                ShowMsg(true, ex.Message);
                return null;
            }
        }

        private async Task KillProcess(Process? proc)
        {
            if (proc is null)
            {
                return;
            }
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                await proc.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                proc.Kill();
            }
            if (!proc.HasExited)
            {
                try
                {
                    await proc.WaitForExitAsync(timeout.Token);
                }
                catch (Exception)
                {
                    proc.Kill();
                }
            }
        }

        #endregion Process
    }
}