namespace ServiceLib.Manager;

/// <summary>
/// Core process processing class
/// </summary>
public class CoreManager
{
    private static readonly Lazy<CoreManager> _instance = new(() => new());
    public static CoreManager Instance => _instance.Value;
    private Config _config;

    [SupportedOSPlatform("windows")]
    private WindowsJobService? _processJob;

    private ProcessService? _processService;
    private ProcessService? _processPreService;
    private bool _linuxSudo = false;
    private Func<bool, string, Task>? _updateFunc;
    private long _loadCoreSequence;
    private int _tunStartObservationMs = 20000;
    private const string _tag = "CoreHandler";

    private const int TunStartMaxAttempts = 3;
    private const int TunStartRetryDelayMs = 5000;

    private enum TunStartResult
    {
        Success,
        Failed,
        Cancelled
    }

    private enum TunObservationResult
    {
        Survived,
        Exited,
        Cancelled
    }

    public async Task Init(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        var tunSettings = TunStartupSettings.LoadOrCreate();
        _tunStartObservationMs = checked(tunSettings.ObservationSeconds * 1000);

        //Copy the bin folder to the storage location (for init)
        if (Environment.GetEnvironmentVariable(Global.LocalAppData) == "1")
        {
            var fromPath = Utils.GetBaseDirectory("bin");
            var toPath = Utils.GetBinPath("");
            if (fromPath != toPath)
            {
                FileUtils.CopyDirectory(fromPath, toPath, true, false);
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

    /// <param name="mainContext">Resolved main context (with pre-socks ports already merged if applicable).</param>
    /// <param name="preContext">Optional pre-socks context passed to <see cref="CoreStartPreService"/>.</param>
    public async Task LoadCore(CoreConfigContext? mainContext, CoreConfigContext? preContext)
    {
        var loadId = Interlocked.Increment(ref _loadCoreSequence);
        await LogLifecycle(false, $"Core load #{loadId}: request received; TUN={_config.TunModeItem.EnableTun}.");

        if (mainContext == null)
        {
            await LogLifecycle(true, $"Core load #{loadId}: cancelled because the main core context is missing.");
            await UpdateFunc(false, ResUI.CheckServerSettings);
            return;
        }

        var node = mainContext.Node;
        var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        var result = await CoreConfigHandler.GenerateClientConfig(mainContext, fileName);
        if (result.Success != true)
        {
            await LogLifecycle(true, $"Core load #{loadId}: configuration generation failed: {result.Msg}");
            await UpdateFunc(true, result.Msg);
            return;
        }

        await LogLifecycle(false,
            $"Core load #{loadId}: configuration generated; mainCore={mainContext.RunCoreType}, " +
            $"preCore={(preContext == null ? "none" : preContext.RunCoreType.ToString())}, TUN={_config.TunModeItem.EnableTun}.");

        await UpdateFunc(false, $"{node.GetSummary()}");
        await UpdateFunc(false, $"{Utils.GetRuntimeInfo()}");
        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));

        await LogLifecycle(false, $"Core load #{loadId}: stopping the previous core process.");
        await CoreStop();
        await LogLifecycle(false, $"Core load #{loadId}: previous core stop completed.");
        await Task.Delay(100);

        var retryTunStart = Utils.IsWindows()
                            && _config.TunModeItem.EnableTun
                            && mainContext.RunCoreType is ECoreType.Xray or ECoreType.sing_box;

        bool started;
        if (retryTunStart)
        {
            var tunStartResult = await StartTunWithRetry(mainContext, loadId);
            if (tunStartResult == TunStartResult.Cancelled)
            {
                await LogLifecycle(false,
                    $"Core load #{loadId}: TUN startup observation was cancelled because TUN was disabled; " +
                    "the queued non-TUN reload may continue immediately.");
                return;
            }

            started = tunStartResult == TunStartResult.Success;
        }
        else
        {
            started = await StartMainCoreOnce(mainContext, loadId, _config.TunModeItem.EnableTun);
        }

        if (!started && retryTunStart)
        {
            started = await StartWithoutTunFallback(mainContext, preContext, loadId);
        }

        if (!started || _processService == null)
        {
            await LogLifecycle(true, $"Core load #{loadId}: the main core process failed to start.");
            return;
        }

        await WaitForProxyPort(preContext);
        await CoreStartPreService(preContext);

        if (_processPreService != null)
        {
            await LogLifecycle(false,
                $"Core load #{loadId}: pre-core process started; PID={_processPreService.Id}, core={preContext?.RunCoreType}.");
        }

        AppManager.Instance.RunningCoreType = preContext?.RunCoreType ?? mainContext.RunCoreType;

        if (_processService != null)
        {
            await UpdateFunc(true, $"{node.GetSummary()}");
        }

        await LogLifecycle(false, $"Core load #{loadId}: completed; effective TUN={_config.TunModeItem.EnableTun}.");
    }

    public async Task<ProcessService?> LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds)
    {
        var coreType = selecteds.FirstOrDefault()?.CoreType == ECoreType.sing_box ? ECoreType.sing_box : ECoreType.Xray;
        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType);
        await UpdateFunc(false, result.Msg);
        if (result.Success != true)
        {
            return null;
        }

        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        await UpdateFunc(false, configPath);

        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
    }

    public async Task<ProcessService?> LoadCoreConfigSpeedtest(ServerTestItem testItem)
    {
        var node = await AppManager.Instance.GetProfileItem(testItem.IndexId);
        if (node is null)
        {
            return null;
        }

        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var (context, _) = await CoreConfigContextBuilder.Build(_config, node);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, context, testItem, configPath);
        if (result.Success != true)
        {
            return null;
        }

        var coreType = context.RunCoreType;
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
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

            if (_processService != null)
            {
                await _processService.StopAsync();
                _processService.Dispose();
                _processService = null;
            }

            if (_processPreService != null)
            {
                await _processPreService.StopAsync();
                _processPreService.Dispose();
                _processPreService = null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    #region Private

    private async Task<bool> StartMainCoreOnce(CoreConfigContext context, long loadId, bool tunEnabled)
    {
        await LogLifecycle(false, $"Core load #{loadId}: starting the main core process; TUN={tunEnabled}.");
        await CoreStart(context);

        if (_processService == null)
        {
            await LogLifecycle(true, $"Core load #{loadId}: main core start returned no process; TUN={tunEnabled}.");
            return false;
        }

        await LogLifecycle(false,
            $"Core load #{loadId}: main core process started; PID={_processService.Id}, " +
            $"core={context.RunCoreType}, TUN={tunEnabled}.");
        return true;
    }

    private async Task<TunStartResult> StartTunWithRetry(CoreConfigContext context, long loadId)
    {
        var coreName = context.RunCoreType.ToString();
        var observationSeconds = _tunStartObservationMs / 1000;

        for (var attempt = 1; attempt <= TunStartMaxAttempts; attempt++)
        {
            if (!_config.TunModeItem.EnableTun)
            {
                return TunStartResult.Cancelled;
            }

            await LogLifecycle(false,
                $"Core load #{loadId}: {coreName} TUN start attempt {attempt}/{TunStartMaxAttempts}.");

            await CoreStart(context);
            var process = _processService;

            if (process == null)
            {
                if (!_config.TunModeItem.EnableTun)
                {
                    return TunStartResult.Cancelled;
                }

                await LogLifecycle(true,
                    $"Core load #{loadId}: {coreName} TUN attempt {attempt} did not create a process.");
            }
            else
            {
                var pid = process.Id;
                await LogLifecycle(false,
                    $"Core load #{loadId}: {coreName} TUN attempt {attempt} started PID={pid}; " +
                    $"observing it for {observationSeconds} seconds.");

                var observationResult = await ObserveTunStart(process, _tunStartObservationMs);
                if (observationResult == TunObservationResult.Survived)
                {
                    await LogLifecycle(false,
                        $"Core load #{loadId}: {coreName} TUN attempt {attempt} succeeded; " +
                        $"PID={pid} remained running for {observationSeconds} seconds.");
                    return TunStartResult.Success;
                }

                if (observationResult == TunObservationResult.Cancelled)
                {
                    await LogLifecycle(false,
                        $"Core load #{loadId}: {coreName} TUN observation cancelled because TUN was disabled; " +
                        $"stopping PID={pid} immediately.");
                    await CoreStop();
                    return TunStartResult.Cancelled;
                }

                await LogLifecycle(true,
                    $"Core load #{loadId}: {coreName} TUN attempt {attempt} failed; " +
                    $"PID={pid} exited during startup observation.");

                process.Dispose();
                if (ReferenceEquals(_processService, process))
                {
                    _processService = null;
                }
            }

            if (attempt < TunStartMaxAttempts)
            {
                await LogLifecycle(false,
                    $"Core load #{loadId}: waiting {TunStartRetryDelayMs / 1000} seconds before " +
                    $"{coreName} TUN retry {attempt + 1}.");

                if (!await DelayWhileTunEnabled(TunStartRetryDelayMs))
                {
                    await LogLifecycle(false,
                        $"Core load #{loadId}: {coreName} TUN retry cancelled because TUN was disabled.");
                    return TunStartResult.Cancelled;
                }
            }
        }

        await LogLifecycle(true,
            $"Core load #{loadId}: all {TunStartMaxAttempts} {coreName} TUN start attempts failed.");
        return TunStartResult.Failed;
    }

    private async Task<bool> StartWithoutTunFallback(
        CoreConfigContext mainContext,
        CoreConfigContext? preContext,
        long loadId)
    {
        await LogLifecycle(true,
            $"Core load #{loadId}: disabling TUN and starting the selected server without TUN as fallback.");

        _config.TunModeItem.EnableTun = false;
        mainContext.AppConfig.TunModeItem.EnableTun = false;
        if (preContext != null)
        {
            preContext.AppConfig.TunModeItem.EnableTun = false;
        }

        await ConfigHandler.SaveConfig(_config);

        var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        var result = await CoreConfigHandler.GenerateClientConfig(mainContext, fileName);
        if (result.Success != true)
        {
            await LogLifecycle(true,
                $"Core load #{loadId}: fallback configuration generation failed: {result.Msg}");
            return false;
        }

        return await StartMainCoreOnce(mainContext, loadId, false);
    }

    private async Task<TunObservationResult> ObserveTunStart(ProcessService process, int observationMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < observationMs)
        {
            if (!_config.TunModeItem.EnableTun)
            {
                return TunObservationResult.Cancelled;
            }

            if (process.HasExited)
            {
                return TunObservationResult.Exited;
            }

            await Task.Delay(100);
        }

        if (!_config.TunModeItem.EnableTun)
        {
            return TunObservationResult.Cancelled;
        }

        return process.HasExited ? TunObservationResult.Exited : TunObservationResult.Survived;
    }

    private async Task<bool> DelayWhileTunEnabled(int delayMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < delayMs)
        {
            if (!_config.TunModeItem.EnableTun)
            {
                return false;
            }

            await Task.Delay(100);
        }

        return _config.TunModeItem.EnableTun;
    }

    private async Task CoreStart(CoreConfigContext context)
    {
        var node = context.Node;
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);

        var displayLog = node.ConfigType != EConfigType.Custom || node.DisplayLog;
        var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true);
        if (proc is null)
        {
            return;
        }
        _processService = proc;
    }

    private async Task CoreStartPreService(CoreConfigContext? preContext)
    {
        if (_processService is { HasExited: false } && preContext != null)
        {
            var preCoreType = preContext?.Node?.CoreType ?? ECoreType.sing_box;
            var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
            var result = await CoreConfigHandler.GenerateClientConfig(preContext, fileName);
            if (result.Success)
            {
                var coreInfo = CoreInfoManager.Instance.GetCoreInfo(preCoreType);
                var proc = await RunProcess(coreInfo, Global.CorePreConfigFileName, true, true);
                if (proc is null)
                {
                    return;
                }
                _processPreService = proc;
            }
        }
    }

    private async Task UpdateFunc(bool notify, string msg)
    {
        await _updateFunc?.Invoke(notify, msg);
    }

    private async Task LogLifecycle(bool notify, string message)
    {
        Logging.SaveLog(message);
        await UpdateFunc(notify, message + Environment.NewLine);
    }

    private static async Task WaitForProxyPort(CoreConfigContext? preContext, int timeoutMs = 5000)
    {
        if (preContext is null)
        {
            return;
        }
        if (!preContext.AppConfig.TunModeItem.EnableTun)
        {
            return;
        }

        using var rootCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        var rootToken = rootCts.Token;

        var port = preContext.Node.Port;
        // SOCKS5 client greeting: VER=5, NMETHODS=1, METHOD=0x00 (no auth)
        ReadOnlyMemory<byte> greeting = new byte[] { 0x05, 0x01, 0x00 };
        var buf = new byte[2];

        while (!rootToken.IsCancellationRequested)
        {
            using var tcp = new TcpClient();
            using var attemptCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(rootToken, attemptCts.Token);
            var linkedToken = linkedCts.Token;
            try
            {
                await tcp.ConnectAsync(Global.Loopback, port, linkedToken);
                var stream = tcp.GetStream();

                await stream.WriteAsync(greeting, linkedToken);

                var read = await stream.ReadAsync(buf.AsMemory(0, 2), linkedToken);

                // Server selection: VER=5, METHOD=0x00 — proxy is fully ready
                if (read == 2 && buf[0] == 0x05)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                if (!rootToken.IsCancellationRequested)
                {
                    continue;
                }
                Logging.SaveLog($"WaitForProxyPort Timeout waiting for proxy port {port} to be ready.");
                return;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                // Connection refused, proxy not ready yet, wait 50ms before retrying
                try
                {
                    await Task.Delay(50, rootToken);
                }
                catch (OperationCanceledException)
                {
                    Logging.SaveLog($"WaitForProxyPort Timeout waiting for proxy port {port} to be ready.");
                    return;
                }
            }
            catch
            {
                // Ignore other exceptions and continue
            }
        }
    }

    #endregion Private

    #region Process

    private async Task<ProcessService?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo)
    {
        var fileName = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var msg);
        if (fileName.IsNullOrEmpty())
        {
            await UpdateFunc(false, msg);
            return null;
        }

        try
        {
            if (mayNeedSudo
                && _config.TunModeItem.EnableTun
                && (coreInfo.CoreType is ECoreType.sing_box or ECoreType.mihomo or ECoreType.Xray)
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
            await UpdateFunc(mayNeedSudo, ex.Message);
            return null;
        }
    }

    private async Task<ProcessService?> RunProcessNormal(string fileName, CoreInfo? coreInfo, string configPath, bool displayLog)
    {
        var environmentVars = new Dictionary<string, string>();
        foreach (var kv in coreInfo.Environment)
        {
            environmentVars[kv.Key] = string.Format(kv.Value, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath);
        }

        var procService = new ProcessService(
            fileName: fileName,
            arguments: string.Format(coreInfo.Arguments, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath),
            workingDirectory: Utils.GetBinConfigPath(),
            displayLog: displayLog,
            redirectInput: false,
            environmentVars: environmentVars,
            updateFunc: _updateFunc
        );

        await procService.StartAsync();

        await Task.Delay(100);

        if (procService is null or { HasExited: true })
        {
            throw new Exception(ResUI.FailedToRunCore);
        }
        AddProcessJob(procService.Handle);

        return procService;
    }

    private void AddProcessJob(nint processHandle)
    {
        if (Utils.IsWindows())
        {
            _processJob ??= new();
            try
            {
                _processJob?.AddProcess(processHandle);
            }
            catch { }
        }
    }

    #endregion Process
}
