using System.Diagnostics;
using System.Text;
using ServiceLib.Enums;
using ServiceLib.Models;

namespace ServiceLib.Handler;

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

        // Create launch context and configure parameters
        var context = new CoreLaunchContext(node, _config);
        context.AdjustForConfigType();

        // Start main core
        if (!await CoreStart(context))
        {
            return;
        }

        // Start pre-core if needed
        if (!await CoreStartPreService(context))
        {
            await CoreStop(); // Clean up main core if pre-core fails
            return;
        }

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
            if (_linuxSudo)
            {
                await CoreAdminHandler.Instance.KillProcessAsLinuxSudo();
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

    /// <summary>
    /// Core launch context that encapsulates all parameters required for launching
    /// </summary>
    private class CoreLaunchContext
    {
        public ProfileItem Node { get; set; }
        public bool SplitCore { get; set; }
        public ECoreType CoreType { get; set; }
        public ECoreType? PreCoreType { get; set; }
        public ECoreType PureEndpointCore { get; set; }
        public ECoreType SplitRouteCore { get; set; }
        public bool EnableTun { get; set; }
        public int PreSocksPort { get; set; }

        public CoreLaunchContext(ProfileItem node, Config config)
        {
            Node = node;
            SplitCore = config.SplitCoreItem.EnableSplitCore;
            CoreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
            PureEndpointCore = AppHandler.Instance.GetSplitCoreType(node, node.ConfigType);
            SplitRouteCore = config.SplitCoreItem.RouteCoreType;
            EnableTun = config.TunModeItem.EnableTun;
            PreSocksPort = 0;
            PreCoreType = null;
        }

        /// <summary>
        /// Adjust context parameters based on configuration type
        /// </summary>
        public void AdjustForConfigType()
        {
            (SplitCore, CoreType, PreCoreType) = AppHandler.Instance.GetCoreAndPreType(Node);
            if (Node.ConfigType == EConfigType.Custom)
            {
                if (Node.PreSocksPort > 0)
                {
                    PreSocksPort = Node.PreSocksPort.Value;
                }
                else
                {
                    EnableTun = false;
                }
            }
            else if (PreCoreType != null)
            {
                PreSocksPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.split);
            }
        }
    }

    private async Task<bool> CoreStart(CoreLaunchContext context)
    {
        var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        var result = context.SplitCore
            ? await CoreConfigHandler.GeneratePureEndpointConfig(context.Node, fileName)
            : await CoreConfigHandler.GenerateClientConfig(context.Node, fileName);

        if (result.Success != true)
        {
            UpdateFunc(true, result.Msg);
            return false;
        }

        UpdateFunc(false, $"{context.Node.GetSummary()}");
        UpdateFunc(false, $"{Utils.GetRuntimeInfo()}");
        UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        
        await CoreStop();
        await Task.Delay(100);

        if (Utils.IsWindows() && _config.TunModeItem.EnableTun)
        {
            await Task.Delay(100);
            await WindowsUtils.RemoveTunDevice();
        }

        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo(context.CoreType);
        var displayLog = context.Node.ConfigType != EConfigType.Custom || context.Node.DisplayLog;
        var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true);
        
        if (proc is null)
        {
            UpdateFunc(true, ResUI.FailedToRunCore);
            return false;
        }
        
        _process = proc;
        _config.RunningCoreType = AppHandler.Instance.GetCoreType(context.Node, context.Node.ConfigType);
        return true;
    }

    private async Task<bool> CoreStartPreService(CoreLaunchContext context)
    {
        if (context.PreCoreType == null)
        {
            return true; // No pre-core needed, consider successful
        }

        var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
        var itemSocks = new ProfileItem()
        {
            CoreType = context.PreCoreType,
            ConfigType = EConfigType.SOCKS,
            Address = Global.Loopback,
            Sni = context.EnableTun && Utils.IsDomain(context.Node.Address) ? context.Node.Address : string.Empty, //Tun2SocksAddress
            Port = context.PreSocksPort
        };
        
        var result = await CoreConfigHandler.GenerateClientConfig(itemSocks, fileName);
        if (!result.Success)
        {
            UpdateFunc(true, result.Msg);
            return false;
        }

        var coreInfo = CoreInfoHandler.Instance.GetCoreInfo((ECoreType)context.PreCoreType);
        var proc = await RunProcess(coreInfo, Global.CorePreConfigFileName, true, true);
        
        if (proc is null || (_process?.HasExited == true))
        {
            UpdateFunc(true, ResUI.FailedToRunCore);
            return false;
        }
        
        _processPre = proc;
        return true;
    }

    private void UpdateFunc(bool notify, string msg)
    {
        _updateFunc?.Invoke(notify, msg);
    }

    #endregion Private

    #region Process

    private async Task<Process?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo)
    {
        var fileName = CoreInfoHandler.Instance.GetCoreExecFile(coreInfo, out var msg);
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
                await CoreAdminHandler.Instance.Init(_config, _updateFunc);
                return await CoreAdminHandler.Instance.RunProcessAsLinuxSudo(fileName, coreInfo, configPath);
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
        AppHandler.Instance.AddProcess(proc.Handle);
        if (proc is null or { HasExited: true })
        {
            throw new Exception(ResUI.FailedToRunCore);
        }
        return proc;
    }

    #endregion Process
}
