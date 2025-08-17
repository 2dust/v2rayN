using System.Diagnostics;
using System.Text;

namespace ServiceLib.Manager;

/// <summary>
/// Core process processing class
/// </summary>
public class CoreManager
{
    private static readonly Lazy<CoreManager> _instance = new(() => new());
    public static CoreManager Instance => _instance.Value;
    private Config _config;
    private Process? _process;
    private Process? _processPre;
    private bool _linuxSudo = false;
    private Action<bool, string>? _updateFunc;
    private const string _tag = "CoreHandler";

    public async Task Init(Config config, Action<bool, string> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Environment.SetEnvironmentVariable(Global.V2RayLocalAsset, Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(Global.XrayLocalAsset, Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(Global.XrayLocalCert, Utils.GetBinPath(""), EnvironmentVariableTarget.Process);

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
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo();
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
        var coreType = selecteds.Exists(t => t.ConfigType is EConfigType.Hysteria2 or EConfigType.TUIC or EConfigType.Anytls) ? ECoreType.sing_box : ECoreType.Xray;
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

        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        var proc = await RunProcess(coreInfo, fileName, true, false);
        if (proc is null)
        {
            return -1;
        }

        return proc.Id;
    }

    public async Task<int> LoadCoreConfigSpeedtest(ServerTestItem testItem)
    {
        var node = await AppManager.Instance.GetProfileItem(testItem.IndexId);
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

        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
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
            if (_linuxSudo)
            {
                await CoreAdminManager.Instance.KillProcessAsLinuxSudo();
                _linuxSudo = false;
            }

            if (_process != null)
            {
                await ProcUtils.ProcessKill(_process, Utils.IsWindows());
                _process = null;
            }

            if (_processPre != null)
            {
                await ProcUtils.ProcessKill(_processPre, Utils.IsWindows());
                _processPre = null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    #region Private

    private async Task CoreStart(ProfileItem node)
    {
        var coreType = _config.RunningCoreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);

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
            var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
            var itemSocks = await ConfigHandler.GetPreSocksItem(_config, node, coreType);
            if (itemSocks != null)
            {
                var preCoreType = itemSocks.CoreType ?? ECoreType.sing_box;
                var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
                var result = await CoreConfigHandler.GenerateClientConfig(itemSocks, fileName);
                if (result.Success)
                {
                    var coreInfo = CoreInfoManager.Instance.GetCoreInfo(preCoreType);
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

    #endregion Private

    #region Process

    private async Task<Process?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo)
    {
        var fileName = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var msg);
        if (fileName.IsNullOrEmpty())
        {
            UpdateFunc(false, msg);
            return null;
        }

        try
        {
            if (mayNeedSudo
                && _config.TunModeItem.EnableTun
                && coreInfo.CoreType == ECoreType.sing_box
                && Utils.IsNonWindows())
            {
                _linuxSudo = true;
                await CoreAdminManager.Instance.Init(_config, _updateFunc);
                return await CoreAdminManager.Instance.RunProcessAsLinuxSudo(fileName, coreInfo, configPath);
            }

            return await RunProcessNormal(fileName, coreInfo, configPath, displayLog);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            UpdateFunc(mayNeedSudo, ex.Message);
            return null;
        }
    }

    private async Task<Process?> RunProcessNormal(string fileName, CoreInfo? coreInfo, string configPath, bool displayLog)
    {
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = fileName,
                Arguments = string.Format(coreInfo.Arguments, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath),
                WorkingDirectory = Utils.GetBinConfigPath(),
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
            void dataHandler(object sender, DataReceivedEventArgs e)
            {
                if (e.Data.IsNotEmpty())
                {
                    UpdateFunc(false, e.Data + Environment.NewLine);
                }
            }
            proc.OutputDataReceived += dataHandler;
            proc.ErrorDataReceived += dataHandler;
        }
        proc.Start();

        if (displayLog)
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        await Task.Delay(100);
        AppManager.Instance.AddProcess(proc.Handle);
        if (proc is null or { HasExited: true })
        {
            throw new Exception(ResUI.FailedToRunCore);
        }
        return proc;
    }

    #endregion Process
}
