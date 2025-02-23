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
        private int _linuxSudoPid = -1;
        private Action<bool, string>? _updateFunc;
        private const string _tag = "CoreHandler";

        public async Task Init(Config config, Action<bool, string> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;

            Environment.SetEnvironmentVariable(Global.V2RayLocalAsset, Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(Global.XrayLocalAsset, Utils.GetBinPath(""), EnvironmentVariableTarget.Process);

            //Copy the bin folder to the storage location (for init)
            if (Environment.GetEnvironmentVariable(Global.LocalAppData) == "1")
            {
                var fromPath = Utils.GetBaseDirectory("bin");
                var toPath = Utils.GetBinPath("");
                if (fromPath != toPath)
                {
                    FileManager.CopyDirectory(fromPath, toPath, true, false);
                }
            }

            if (Utils.IsNonWindows())
            {
                var coreInfo = CoreInfoHandler.Instance.GetCoreInfo();
                foreach (var it in coreInfo)
                {
                    if (it.CoreType == ECoreType.v2rayN)
                    {
                        if (Utils.UpgradeAppExists(out var upgradeFileName))
                        {
                            await Utils.SetLinuxChmod(upgradeFileName);
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
                UpdateFunc(false, ResUI.CheckServerSettings);
                return;
            }

            var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
            var result = await CoreConfigHandler.GenerateClientConfig(node, fileName);
            if (result.Success != true)
            {
                UpdateFunc(true, result.Msg);
                return;
            }

            UpdateFunc(false, $"{node.GetSummary()}");
            UpdateFunc(false, $"{Utils.GetRuntimeInfo()}");
            UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
            await CoreStop();
            await Task.Delay(100);

            if (Utils.IsWindows() && _config.TunModeItem.EnableTun)
            {
                await Task.Delay(100);
                await WindowsUtils.RemoveTunDevice();
            }

            await CoreStart(node);
            await CoreStartPreService(node);
            if (_process != null)
            {
                UpdateFunc(true, $"{node.GetSummary()}");
            }
        }

        public async Task<int> LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds)
        {
            var coreType = selecteds.Exists(t => t.ConfigType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.WireGuard) ? ECoreType.sing_box : ECoreType.Xray;
            var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
            var configPath = Utils.GetBinConfigPath(fileName);
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType);
            UpdateFunc(false, result.Msg);
            if (result.Success != true)
            {
                return -1;
            }

            UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
            UpdateFunc(false, configPath);

            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(coreType);
            var proc = await RunProcess(coreInfo, fileName, true, false);
            if (proc is null)
            {
                return -1;
            }

            return proc.Id;
        }

        public async Task<int> LoadCoreConfigSpeedtest(ServerTestItem testItem)
        {
            var node = await AppHandler.Instance.GetProfileItem(testItem.IndexId);
            if (node is null)
            {
                return -1;
            }

            var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
            var configPath = Utils.GetBinConfigPath(fileName);
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, node, testItem, configPath);
            if (result.Success != true)
            {
                return -1;
            }

            var coreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(coreType);
            var proc = await RunProcess(coreInfo, fileName, true, false);
            if (proc is null)
            {
                return -1;
            }

            return proc.Id;
        }

        public async Task CoreStop()
        {
            try
            {
                if (_process != null)
                {
                    await ProcUtils.ProcessKill(_process, true);
                    _process = null;
                }

                if (_processPre != null)
                {
                    await ProcUtils.ProcessKill(_processPre, true);
                    _processPre = null;
                }

                if (_linuxSudoPid > 0)
                {
                    await KillProcessAsLinuxSudo();
                }
                _linuxSudoPid = -1;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }

        #region Private

        private async Task CoreStart(ProfileItem node)
        {
            var coreType = _config.RunningCoreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
            var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(coreType);

            var displayLog = node.ConfigType != EConfigType.Custom || node.DisplayLog;
            var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true);
            if (proc is null)
            {
                return;
            }
            _process = proc;
        }

        private async Task CoreStartPreService(ProfileItem node)
        {
            if (_process != null && !_process.HasExited)
            {
                var coreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
                var itemSocks = await ConfigHandler.GetPreSocksItem(_config, node, coreType);
                if (itemSocks != null)
                {
                    var preCoreType = itemSocks.CoreType ?? ECoreType.sing_box;
                    var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
                    var result = await CoreConfigHandler.GenerateClientConfig(itemSocks, fileName);
                    if (result.Success)
                    {
                        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(preCoreType);
                        var proc = await RunProcess(coreInfo, Global.CorePreConfigFileName, true, true);
                        if (proc is null)
                        {
                            return;
                        }
                        _processPre = proc;
                    }
                }
            }
        }

        private void UpdateFunc(bool notify, string msg)
        {
            _updateFunc?.Invoke(notify, msg);
        }

        private bool IsNeedSudo(ECoreType eCoreType)
        {
            return _config.TunModeItem.EnableTun
                   && eCoreType == ECoreType.sing_box
                   && (Utils.IsNonWindows())
                //&& _config.TunModeItem.LinuxSudoPwd.IsNotEmpty()
                ;
        }

        #endregion Private

        #region Process

        private async Task<Process?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo)
        {
            var fileName = CoreInfoHandler.Instance.GetCoreExecFile(coreInfo, out var msg);
            if (Utils.IsNullOrEmpty(fileName))
            {
                UpdateFunc(false, msg);
                return null;
            }

            try
            {
                Process proc = new()
                {
                    StartInfo = new()
                    {
                        FileName = fileName,
                        Arguments = string.Format(coreInfo.Arguments, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath) : configPath),
                        WorkingDirectory = Utils.GetBinConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = displayLog,
                        RedirectStandardError = displayLog,
                        CreateNoWindow = true,
                        StandardOutputEncoding = displayLog ? Encoding.UTF8 : null,
                        StandardErrorEncoding = displayLog ? Encoding.UTF8 : null,
                    }
                };

                var isNeedSudo = mayNeedSudo && IsNeedSudo(coreInfo.CoreType);
                if (isNeedSudo)
                {
                    await RunProcessAsLinuxSudo(proc, fileName, coreInfo, configPath);
                }

                if (displayLog)
                {
                    proc.OutputDataReceived += (sender, e) =>
                    {
                        if (Utils.IsNullOrEmpty(e.Data))
                            return;
                        UpdateFunc(false, e.Data + Environment.NewLine);
                    };
                    proc.ErrorDataReceived += (sender, e) =>
                    {
                        if (Utils.IsNullOrEmpty(e.Data))
                            return;
                        UpdateFunc(false, e.Data + Environment.NewLine);
                    };
                }
                proc.Start();

                if (isNeedSudo && _config.TunModeItem.LinuxSudoPwd.IsNotEmpty())
                {
                    var pwd = DesUtils.Decrypt(_config.TunModeItem.LinuxSudoPwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                }
                if (isNeedSudo)
                    _linuxSudoPid = proc.Id;

                if (displayLog)
                {
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                }

                await Task.Delay(500);
                AppHandler.Instance.AddProcess(proc.Handle);
                if (proc is null or { HasExited: true })
                {
                    throw new Exception(ResUI.FailedToRunCore);
                }
                return proc;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                UpdateFunc(mayNeedSudo, ex.Message);
                return null;
            }
        }

        #endregion Process

        #region Linux

        private async Task RunProcessAsLinuxSudo(Process proc, string fileName, CoreInfo coreInfo, string configPath)
        {
            var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, Utils.GetBinConfigPath(configPath).AppendQuotes())}";

            var shFilePath = await CreateLinuxShellFile(cmdLine, "run_as_sudo.sh");
            proc.StartInfo.FileName = shFilePath;
            proc.StartInfo.Arguments = "";
            proc.StartInfo.WorkingDirectory = "";
            if (_config.TunModeItem.LinuxSudoPwd.IsNotEmpty())
            {
                proc.StartInfo.StandardInputEncoding = Encoding.UTF8;
                proc.StartInfo.RedirectStandardInput = true;
            }
        }

        private async Task KillProcessAsLinuxSudo()
        {
            var cmdLine = $"kill {_linuxSudoPid}";
            var shFilePath = await CreateLinuxShellFile(cmdLine, "kill_as_sudo.sh");
            Process proc = new()
            {
                StartInfo = new()
                {
                    FileName = shFilePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardInputEncoding = Encoding.UTF8,
                    RedirectStandardInput = true
                }
            };
            proc.Start();

            if (_config.TunModeItem.LinuxSudoPwd.IsNotEmpty())
            {
                try
                {
                    var pwd = DesUtils.Decrypt(_config.TunModeItem.LinuxSudoPwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                    await Task.Delay(10);
                    await proc.StandardInput.WriteLineAsync(pwd);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await proc.WaitForExitAsync(timeout.Token);
            await Task.Delay(3000);
        }

        private async Task<string> CreateLinuxShellFile(string cmdLine, string fileName)
        {
            //Shell scripts
            var shFilePath = Utils.GetBinConfigPath(AppHandler.Instance.IsAdministrator ? "root_" + fileName : fileName);
            File.Delete(shFilePath);
            var sb = new StringBuilder();
            sb.AppendLine("#!/bin/sh");
            if (AppHandler.Instance.IsAdministrator)
            {
                sb.AppendLine($"{cmdLine}");
            }
            else if (_config.TunModeItem.LinuxSudoPwd.IsNullOrEmpty())
            {
                sb.AppendLine($"pkexec {cmdLine}");
            }
            else
            {
                sb.AppendLine($"sudo -S {cmdLine}");
            }

            await File.WriteAllTextAsync(shFilePath, sb.ToString());
            await Utils.SetLinuxChmod(shFilePath);
            Logging.SaveLog(shFilePath);

            return shFilePath;
        }

        #endregion Linux
    }
}
