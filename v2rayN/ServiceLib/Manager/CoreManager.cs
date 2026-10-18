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
    private ProcessService? _psiphonUpstreamService;
    private bool _runningTun;
    private ProcessService? _sniSpoofingService;
    private CancellationTokenSource? _dpiWatchdogCts;
    private Func<string, Task>? _dpiFailureFunc;
    private SniSpoofingPlan? _sniPlan;
    private int _runtimeGeneration;
    public int RuntimeGeneration => Volatile.Read(ref _runtimeGeneration);
    public bool IsDpiReady { get; private set; }
    private TaskCompletionSource<bool>? _psiphonTunnelReady;
    private CancellationTokenSource? _psiphonWatchdogCts;
    private Func<Task>? _psiphonRecoveryFunc;
    private Func<Task>? _psiphonFailureFunc;
    private int _psiphonRecoveryRunning;
    private int _psiphonTunnelCount;
    private int _psiphonSocksPort;
    public void CancelPendingPsiphonStartup() => _psiphonTunnelReady?.TrySetResult(false);
    private bool _linuxSudo = false;
    private Func<bool, string, Task>? _updateFunc;
    private const string _tag = "CoreHandler";

    public async Task Init(Config config, Func<bool, string, Task> updateFunc,
        Func<Task>? psiphonRecoveryFunc = null, Func<Task>? psiphonFailureFunc = null,
        Func<string, Task>? dpiFailureFunc = null)
    {
        _config = config;
        _updateFunc = updateFunc;
        _psiphonRecoveryFunc = psiphonRecoveryFunc;
        _psiphonFailureFunc = psiphonFailureFunc;
        _dpiFailureFunc = dpiFailureFunc;

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
        if (mainContext == null)
        {
            await UpdateFunc(false, ResUI.CheckServerSettings);
            return;
        }

        var node = mainContext.Node;
        var bypassMode = mainContext.AppConfig.CloudflareFragment || mainContext.AppConfig.SniSpoofing.Enabled
            || node.IndexId == ServerlessConfigService.NodeId;
        var fileName = Utils.GetBinConfigPath(Global.CoreConfigFileName);
        var result = await CoreConfigHandler.GenerateClientConfig(mainContext, fileName);
        if (result.Success != true)
        {
            await UpdateFunc(true, result.Msg);
            if (node.CoreType == ECoreType.Psiphon) await DisableFailedPsiphon();
            else if (bypassMode)
                await FailDpi(result.Msg);
            return;
        }

        await UpdateFunc(false, node.CoreType == ECoreType.Psiphon
            ? node.GetProtocolExtra().PsiphonUseUpstream == true ? "Psiphon after active config" : "Psiphon only"
            : node.GetSummary());
        await UpdateFunc(false, $"{Utils.GetRuntimeInfo()}");
        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        await CoreStop();
        await Task.Delay(100);

        if (Utils.IsWindows() && (mainContext?.IsTunEnabled == true || preContext?.IsTunEnabled == true))
        {
            await Task.Delay(100);
            await WindowsUtils.RemoveTunDevice();
        }

        if (mainContext.AppConfig.SniSpoofing.Enabled)
        {
            try
            {
                if (!Utils.IsWindows() || !Utils.IsAdministrator())
                    throw new InvalidOperationException("SNI spoofing requires running v2rayN as administrator on Windows.");
                if (!File.Exists(SniSpoofingService.BinaryPath))
                    throw new FileNotFoundException("The SNI helper is missing. Install the SNI runtime bundle.");
                _sniPlan = SniSpoofingService.CreatePlan(node, mainContext.AppConfig.SniSpoofing, PsiphonConfigService.FindAvailablePort());
                var rewritten = SniSpoofingService.RewriteOutbound(await File.ReadAllTextAsync(fileName), node, _sniPlan);
                await File.WriteAllTextAsync(fileName, rewritten);
                _sniSpoofingService = await StartCompanion(SniSpoofingService.BinaryPath, _sniPlan.Arguments,
                    updateFunc: async (notify, message) =>
                    {
                        Logging.SaveLog($"SNI helper: {message.Trim()}");
                        await UpdateFunc(notify, message);
                    });
                if (!await WaitForTcp(_sniPlan.LocalPort, _sniSpoofingService))
                    throw new InvalidOperationException("The SNI helper failed to open its local port.");
                await UpdateFunc(false, $"SNI spoofing enabled · {_sniPlan.Decoy}");
            }
            catch (Exception ex)
            {
                await FailDpi($"SNI spoofing: {ex.Message}");
                return;
            }
        }

        if (node.CoreType == ECoreType.Psiphon && node.GetProtocolExtra().PsiphonUseUpstream == true)
        {
            try
            {
                var upstream = await PsiphonConfigService.GenerateUpstream(mainContext.AppConfig, node);
                if (!upstream.Success)
                {
                    await UpdateFunc(true, upstream.Msg);
                    await DisableFailedPsiphon();
                    return;
                }
                const string upstreamFile = "configPsiphonUpstream.json";
                await File.WriteAllTextAsync(Utils.GetBinConfigPath(upstreamFile), upstream.Data!.ToString());
                _psiphonUpstreamService = await RunProcess(CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray), upstreamFile, true, false);
                if (_psiphonUpstreamService == null
                    || !await WaitForSocks(node.GetProtocolExtra().PsiphonUpstreamPort ?? 1089, _psiphonUpstreamService))
                {
                    await FailPsiphon("Psiphon's upstream failed to start.");
                    return;
                }
            }
            catch (Exception ex)
            {
                await FailPsiphon($"Psiphon upstream: {ex.Message}");
                return;
            }
        }
        if (node.CoreType == ECoreType.Psiphon)
        {
            _psiphonTunnelReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            await CoreStart(mainContext);
        }
        catch (Exception ex) when (bypassMode)
        {
            await FailDpi(ex.Message);
            return;
        }
        if (bypassMode
            && _processService is null or { HasExited: true })
        {
            await FailDpi("The bypass core failed to start. Switching back to the active config.");
            return;
        }
        if (node.CoreType == ECoreType.Psiphon
            && (_processService == null || !await WaitForSocks(node.PreSocksPort ?? 0, _processService)))
        {
            await FailPsiphon("Psiphon failed to open its local SOCKS proxy.");
            return;
        }
        if (node.CoreType == ECoreType.Psiphon
            && (_psiphonTunnelReady == null
                || await Task.WhenAny(_psiphonTunnelReady.Task, Task.Delay(TimeSpan.FromSeconds(45))) != _psiphonTunnelReady.Task
                || !await _psiphonTunnelReady.Task))
        {
            await FailPsiphon("Psiphon could not establish a tunnel.");
            return;
        }
        await WaitForProxyPort(preContext);
        try { await CoreStartPreService(preContext, node); }
        catch (Exception ex) when (bypassMode)
        {
            await FailDpi($"The bypass routing service failed: {ex.Message}");
            return;
        }
        if (node.CoreType == ECoreType.Psiphon && (preContext == null || _processPreService == null
            || !await WaitForSocks(AppManager.Instance.GetLocalPort(EInboundProtocol.socks), _processPreService)))
        {
            await FailPsiphon("Psiphon's local routing service failed to start. Check the local proxy port and routing assets.");
            return;
        }
        if (bypassMode && preContext != null
            && (_processPreService == null || !await WaitForSocks(AppManager.Instance.GetLocalPort(EInboundProtocol.socks), _processPreService)))
        {
            await FailDpi("The local bypass routing service failed to start.");
            return;
        }

        AppManager.Instance.RunningCoreType = preContext?.RunCoreType ?? mainContext.RunCoreType;

        if (_processService != null)
        {
            await UpdateFunc(true, node.CoreType == ECoreType.Psiphon
                ? node.GetProtocolExtra().PsiphonUseUpstream == true ? "Psiphon after active config" : "Psiphon only"
                : node.GetSummary());
        }
        if (node.CoreType == ECoreType.Psiphon && _processService is { HasExited: false })
        {
            StartPsiphonWatchdog(_processService, node.PreSocksPort ?? 0);
        }
        if (bypassMode)
        {
            IsDpiReady = true;
            StartDpiWatchdog();
        }
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
        Interlocked.Increment(ref _runtimeGeneration);
        IsDpiReady = false;
        _dpiWatchdogCts?.Cancel();
        _dpiWatchdogCts?.Dispose();
        _dpiWatchdogCts = null;
        StopPsiphonWatchdog();
        CancelPendingPsiphonStartup();
        var removeTun = _runningTun;
        _runningTun = false;
        try
        {
            if (_linuxSudo)
            {
                await CoreAdminManager.Instance.KillProcessAsLinuxSudo();
                _linuxSudo = false;
            }

        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        // Stop the routing frontend before the proxy it forwards to. Each process
        // must be cleaned up even if stopping another process fails.
        var frontend = _processPreService;
        var main = _processService;
        var upstream = _psiphonUpstreamService;
        _processPreService = null;
        _processService = null;
        _psiphonUpstreamService = null;
        await StopProcess(frontend);
        await StopProcess(main);
        await StopProcess(upstream);
        var sni = _sniSpoofingService;
        _sniSpoofingService = null;
        _sniPlan = null;
        await StopProcess(sni);
        if (sni != null) Logging.SaveLog("SNI helper stopped; its local forwarding endpoint has been removed.");
        // Use the running context, not the already changed UI setting. Otherwise
        // turning TUN off leaves its adapter/routes behind during the next start.
        if (removeTun && Utils.IsWindows())
        {
            await WindowsUtils.RemoveTunDevice();
        }
    }

    private static async Task StopProcess(ProcessService? process)
    {
        if (process == null) return;
        try
        {
            await process.StopAsync();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    #region Private

    private async Task FailDpi(string message)
    {
        await CoreStop();
        await UpdateFunc(true, message);
        if (_dpiFailureFunc != null) await _dpiFailureFunc(message);
    }

    private async Task<ProcessService> StartCompanion(string binary, string arguments, Dictionary<string, string>? environment = null,
        Func<bool, string, Task>? updateFunc = null)
    {
        var process = new ProcessService(binary, arguments, Utils.GetBinConfigPath(), true, false, environment, updateFunc ?? UpdateFunc);
        try
        {
            await process.StartAsync();
            AddProcessJob(process.Handle);
            await Task.Delay(200);
            if (process.HasExited) throw new InvalidOperationException("The bypass process exited during startup.");
            return process;
        }
        catch
        {
            await StopProcess(process);
            throw;
        }
    }

    private static async Task<bool> WaitForTcp(int port, ProcessService process)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested && !process.HasExited)
        {
            using var tcp = new TcpClient();
            try { await tcp.ConnectAsync(IPAddress.Loopback, port, timeout.Token); return true; }
            catch (SocketException) { }
            catch (OperationCanceledException) { return false; }
            await Task.Delay(50);
        }
        return false;
    }

    private void StartDpiWatchdog()
    {
        var cts = _dpiWatchdogCts = new();
        var main = _processService;
        var frontend = _processPreService;
        var sni = _sniSpoofingService;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(2000, cts.Token);
                    if (main?.HasExited == true || frontend?.HasExited == true || sni?.HasExited == true)
                    {
                        if (!cts.IsCancellationRequested) await FailDpi("The bypass process stopped. Restoring the active config with the current TUN setting.");
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Logging.SaveLog(_tag, ex); }
        });
    }

    private async Task FailPsiphon(string message)
    {
        await CoreStop();
        await UpdateFunc(true, message);
        await DisableFailedPsiphon();
    }

    private async Task DisableFailedPsiphon()
    {
        if (ShouldRecoverPsiphon(_config.TunModeItem.EnableTun, _config.PsiphonMode)
            && _psiphonFailureFunc != null)
        {
            await _psiphonFailureFunc();
        }
    }

    private void StartPsiphonWatchdog(ProcessService process, int socksPort)
    {
        StopPsiphonWatchdog();
        if (socksPort is < 1 or > 65535 || _psiphonRecoveryFunc == null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        Volatile.Write(ref _psiphonTunnelCount, 1);
        Volatile.Write(ref _psiphonSocksPort, socksPort);
        _psiphonWatchdogCts = cts;
        _ = MonitorPsiphon(process, socksPort, cts.Token);
    }

    private void StopPsiphonWatchdog()
    {
        var cts = Interlocked.Exchange(ref _psiphonWatchdogCts, null);
        Volatile.Write(ref _psiphonTunnelCount, 0);
        Volatile.Write(ref _psiphonSocksPort, 0);
        if (cts == null) return;
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    // Psiphon reports zero tunnels while it re-establishes on its own, keeping its local SOCKS
    // proxy open. The Android client simply lets the engine do that. Restarting the whole stack
    // here discards Psiphon's progress and tears down TUN, which turns a short upstream hiccup
    // into a long outage and a reconnect loop. Only intervene for real failures.
    public static readonly TimeSpan PsiphonReconnectGrace = TimeSpan.FromSeconds(90);

    public static bool PsiphonNeedsRestart(bool processesAlive, bool socksAnswering, int failedChecks,
        DateTime? tunnelLostAt, DateTime now) =>
        !processesAlive
        || (!socksAnswering && failedChecks >= 2)
        || (tunnelLostAt != null && now - tunnelLostAt.Value > PsiphonReconnectGrace);

    private async Task MonitorPsiphon(ProcessService process, int socksPort, CancellationToken token)
    {
        try
        {
            var failedChecks = 0;
            DateTime? tunnelLostAt = null;
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                if (!ReferenceEquals(process, _processService)) return;

                // The engine, the TUN frontend and (in chained mode) the Xray upstream must all run.
                var processesAlive = !process.HasExited
                    && _processPreService is not { HasExited: true }
                    && _psiphonUpstreamService is not { HasExited: true };
                var socksAnswering = processesAlive && await ProbeSocks(socksPort, token);
                failedChecks = socksAnswering ? 0 : failedChecks + 1;

                if (Volatile.Read(ref _psiphonTunnelCount) > 0)
                {
                    if (tunnelLostAt != null) await UpdateFunc(true, "Psiphon reconnected.");
                    tunnelLostAt = null;
                }
                else if (tunnelLostAt == null)
                {
                    tunnelLostAt = DateTime.UtcNow;
                    await UpdateFunc(true, "Psiphon tunnel lost. Psiphon is reconnecting...");
                }

                if (!PsiphonNeedsRestart(processesAlive, socksAnswering, failedChecks, tunnelLostAt, DateTime.UtcNow)) continue;

                if (!ShouldRecoverPsiphon(_config.TunModeItem.EnableTun, _config.PsiphonMode)) return;
                await RecoverPsiphon();
                return;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public static bool ShouldRecoverPsiphon(bool tunEnabled, string? mode) =>
        tunEnabled && mode is "only" or "after";

    private async Task RecoverPsiphon()
    {
        if (_psiphonRecoveryFunc == null
            || Interlocked.CompareExchange(ref _psiphonRecoveryRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await UpdateFunc(true, "Psiphon disconnected. Reconnecting automatically...");
            var delays = new[] { 2, 5, 10, 20, 30 };
            foreach (var delaySeconds in delays)
            {
                if (!ShouldRecoverPsiphon(_config.TunModeItem.EnableTun, _config.PsiphonMode)) return;
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                if (!ShouldRecoverPsiphon(_config.TunModeItem.EnableTun, _config.PsiphonMode)) return;

                try
                {
                    await _psiphonRecoveryFunc();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }

                if (!ShouldRecoverPsiphon(_config.TunModeItem.EnableTun, _config.PsiphonMode)) return;
                await Task.Delay(TimeSpan.FromSeconds(2));
                var socksPort = Volatile.Read(ref _psiphonSocksPort);
                if (_processService is { HasExited: false }
                    && _processPreService is { HasExited: false }
                    && Volatile.Read(ref _psiphonTunnelCount) > 0
                    && socksPort > 0
                    && await ProbeSocks(socksPort, CancellationToken.None))
                {
                    await UpdateFunc(true, "Psiphon reconnected.");
                    return;
                }
            }
            await FailPsiphon("Psiphon automatic reconnect failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _psiphonRecoveryRunning, 0);
        }
    }

    private static async Task<bool> ProbeSocks(int port, CancellationToken token)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token);
            var reply = new byte[2];
            await stream.ReadExactlyAsync(reply, timeout.Token);
            return reply[0] == 5 && reply[1] == 0;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForSocks(int port, ProcessService process)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested && !process.HasExited)
        {
            try
            {
                using var client = new TcpClient();
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                attempt.CancelAfter(500);
                await client.ConnectAsync(IPAddress.Loopback, port, attempt.Token);
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 5, 1, 0 }, attempt.Token);
                var reply = new byte[2];
                await stream.ReadExactlyAsync(reply, attempt.Token);
                if (reply[0] == 5 && reply[1] == 0)
                {
                    return !process.HasExited;
                }
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException) { }
            try { await Task.Delay(100, timeout.Token); }
            catch (OperationCanceledException) { break; }
        }
        return false;
    }

    private async Task CoreStart(CoreConfigContext context)
    {
        var node = context.Node;
        if (node.IndexId == ServerlessConfigService.NodeId || context.AppConfig.CloudflareFragment)
        {
            if (!File.Exists(ServerlessConfigService.BinaryPath))
                throw new FileNotFoundException("Serverless needs its separate Xray 26.6.27+ runtime bundle.");
            _processService = await StartCompanion(ServerlessConfigService.BinaryPath,
                $"run -c {Utils.GetBinConfigPath(Global.CoreConfigFileName).AppendQuotes()}",
                new() { ["XRAY_LOCATION_ASSET"] = Path.GetDirectoryName(ServerlessConfigService.BinaryPath)! });
            return;
        }
        var coreType = AppManager.Instance.GetCoreType(node, node.ConfigType);
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);

        var displayLog = node.ConfigType != EConfigType.Custom || node.DisplayLog;
        var proc = await RunProcess(coreInfo, Global.CoreConfigFileName, displayLog, true, context.IsTunEnabled);
        if (proc is null)
        {
            return;
        }
        _processService = proc;
        _runningTun |= context.IsTunEnabled;
    }

    private async Task CoreStartPreService(CoreConfigContext? preContext, ProfileItem mainNode)
    {
        if (_processService is { HasExited: false } && preContext != null)
        {
            var preCoreType = preContext?.Node?.CoreType ?? ECoreType.sing_box;
            var fileName = Utils.GetBinConfigPath(Global.CorePreConfigFileName);
            var result = await CoreConfigHandler.GenerateClientConfig(preContext, fileName);
            if (result.Success)
            {
                if (preCoreType == ECoreType.sing_box && preContext.IsTunEnabled)
                {
                    IPAddress[] upstreamAddresses = [];
                    var pinnedHosts = new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase);
                    var isPsiphon = mainNode.CoreType == ECoreType.Psiphon;
                    var upstream = mainNode.IndexId == ServerlessConfigService.NodeId ? null : !isPsiphon ? mainNode
                        : mainNode.GetProtocolExtra().PsiphonUseUpstream == true
                            ? await AppManager.Instance.GetProfileItem(mainNode.GetProtocolExtra().PsiphonUpstreamProfileId ?? "")
                            : null;
                    var addressToExclude = _sniPlan?.Host ?? (preContext.AppConfig.CloudflareFragment
                        && !string.IsNullOrWhiteSpace(preContext.AppConfig.SniSpoofing.CloudflareAddress)
                            ? preContext.AppConfig.SniSpoofing.CloudflareAddress : upstream?.Address);
                    if (!string.IsNullOrWhiteSpace(addressToExclude))
                    {
                        if (IPAddress.TryParse(addressToExclude, out var address)) upstreamAddresses = [address];
                        else
                        {
                            upstreamAddresses = await Dns.GetHostAddressesAsync(addressToExclude);
                            pinnedHosts[addressToExclude] = upstreamAddresses;
                        }
                    }
                    // ECH must be fetched before the proxy TLS connection can start.
                    // Exclude its resolver from TUN even if process lookup is unavailable.
                    if (TunBootstrapService.EchResolverHost(upstream?.EchConfigList) is { } echHost)
                    {
                        var echAddresses = IPAddress.TryParse(echHost, out var echAddress)
                            ? new[] { echAddress } : await Dns.GetHostAddressesAsync(echHost);
                        if (echAddress == null) pinnedHosts[echHost] = echAddresses;
                        upstreamAddresses = [.. upstreamAddresses, .. echAddresses];
                    }
                    await File.WriteAllTextAsync(fileName,
                        PsiphonConfigService.SimplifyTunFrontend(await File.ReadAllTextAsync(fileName), upstreamAddresses,
                            preserveRuleSets: !isPsiphon && mainNode.IndexId != ServerlessConfigService.NodeId, pinnedHosts));
                }
                if (_sniPlan != null && preCoreType == ECoreType.sing_box)
                    await File.WriteAllTextAsync(fileName, SniSpoofingService.ProtectTun(await File.ReadAllTextAsync(fileName)));
                var coreInfo = CoreInfoManager.Instance.GetCoreInfo(preCoreType);
                var proc = await RunProcess(coreInfo, Global.CorePreConfigFileName, true, true, preContext.IsTunEnabled);
                if (proc is null)
                {
                    return;
                }
                _processPreService = proc;
                _runningTun |= preContext.IsTunEnabled;
            }
        }
    }

    private async Task UpdateFunc(bool notify, string msg)
    {
        PsiphonConfigService.CaptureAvailableRegions(msg);
        try
        {
            var notice = JsonNode.Parse(msg);
            if (notice?["noticeType"]?.GetValue<string>() == "Tunnels")
            {
                var count = notice["data"]?["count"]?.GetValue<int>() ?? 0;
                Volatile.Write(ref _psiphonTunnelCount, count);
                if (count > 0) _psiphonTunnelReady?.TrySetResult(true);
            }
        }
        catch { }
        await _updateFunc?.Invoke(notify, msg);
    }

    private static async Task WaitForProxyPort(CoreConfigContext? preContext)
    {
        if (preContext is null)
        {
            return;
        }
        if (!preContext.IsTunEnabled)
        {
            return;
        }

        using var rootCts = new CancellationTokenSource(Global.LocalFetch);
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

    /// <summary>
    ///     Decides whether a core launch must be elevated on non-Windows platforms.
    ///     The TUN state comes from the immutable <see cref="CoreConfigContext" /> snapshot that
    ///     generated the config, never from the live mutable config: the generated config and the
    ///     launch mode must always agree, even if TUN is toggled while a reload is in flight.
    /// </summary>
    public static bool ShouldRunAsSudo(bool isTunLaunch, ECoreType? coreType, bool isNonWindows)
    {
        return isTunLaunch
            && coreType is ECoreType.sing_box or ECoreType.mihomo or ECoreType.Xray
            && isNonWindows;
    }

    private async Task<ProcessService?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo, bool isTunLaunch = false)
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
                && ShouldRunAsSudo(isTunLaunch, coreInfo.CoreType, Utils.IsNonWindows()))
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
            updateFunc: async (notify, msg) => await UpdateFunc(notify, msg)
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
