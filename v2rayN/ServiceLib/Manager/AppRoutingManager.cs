using ServiceLib.Services.AppRouting;

namespace ServiceLib.Manager;

public interface IAppRoutingRuntime
{
    bool IsEnabled
    {
        get;
    }
    Task StartAsync(Config config);
    Task StopAsync();
}

public sealed class AppRoutingManager : IAppRoutingRuntime
{
    public static AppRoutingManager Instance { get; } = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RouteRuntime _runtime;
    private CancellationTokenSource? _preparation;
    private CancellationTokenSource? _watch;
    private volatile bool _shuttingDown;
    private volatile AppRoutingState _state;
    public AppRoutingState State { get => _state; private set => _state = value; }
    public bool IsRunning => _runtime.IsRunning || State == AppRoutingState.Starting;
    public bool IsEnabled => State == AppRoutingState.Running;
    private string? _lastError;

    private AppRoutingManager()
    {
        _runtime = new(() => OperatingSystem.IsWindows() ? new AppRouteEngine([], [], Report) : throw new PlatformNotSupportedException(),
            () => OperatingSystem.IsWindows() ? new RouteCaptureLease() : throw new PlatformNotSupportedException());
    }

    public static void Validate(IEnumerable<AppRouteRule> rules)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            var executable = AppRouteMatcher.Normalize(rule.ExecutablePath, rule.MatchByName);
            if (!paths.Add($"{rule.MatchByName}:{executable}"))
            {
                throw new ArgumentException("Each executable match can have only one enabled rule.");
            }

            if (IsProtectedExecutable(executable))
            {
                throw new ArgumentException("v2rayN and proxy-core executables cannot be routed, to prevent routing loops.");
            }

            if (!Enum.IsDefined(rule.Kind))
            {
                throw new ArgumentException("Unknown application route type.");
            }

            if (rule.Kind == AppRouteKind.Profile && string.IsNullOrWhiteSpace(rule.ProfileId))
            {
                throw new ArgumentException("Select a saved profile.");
            }

            if (rule.Kind == AppRouteKind.Interface && string.IsNullOrWhiteSpace(rule.InterfaceId))
            {
                throw new ArgumentException("Select a network interface.");
            }

            if (rule.Kind == AppRouteKind.Socks5 && (Uri.CheckHostName(rule.SocksHost) == UriHostNameType.Unknown || rule.SocksPort is < 1 or > 65535))
            {
                throw new ArgumentException("Enter a valid SOCKS5 host and port (1–65535).");
            }

            if (rule.Kind == AppRouteKind.Socks5 && ((rule.SocksUsername.Length == 0) != (rule.SocksPassword.Length == 0) ||
                Encoding.UTF8.GetByteCount(rule.SocksUsername) > 255 || Encoding.UTF8.GetByteCount(rule.SocksPassword) > 255))
            {
                throw new ArgumentException("Provide both SOCKS5 credentials, each at most 255 UTF-8 bytes, or leave both empty.");
            }
        }
    }

    internal static bool IsProtectedExecutable(string executable) =>
        CoreInfoManager.Instance.GetCoreInfo().SelectMany(c => c.CoreExes ?? []).Append("v2rayN")
            .Any(name => string.Equals(Path.GetFileNameWithoutExtension(name), Path.GetFileNameWithoutExtension(executable), StringComparison.OrdinalIgnoreCase));

    public async Task StartAsync(Config config)
    {
        if (_shuttingDown || !config.AppRouting.Enabled) { return; }
        if (!OperatingSystem.IsWindows() || RuntimeInformation.OSArchitecture is not (Architecture.X86 or Architecture.X64) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X86 or Architecture.X64))
        {
            throw new PlatformNotSupportedException("Application routing requires an x86 or x64 build of v2rayN on x86 or x64 Windows.");
        }
        if (!Utils.IsAdministrator()) { throw new InvalidOperationException(ResUI.AppRoutingAdminRequired); }
        var driver = Environment.Is64BitOperatingSystem ? "WinDivert64.sys" : "WinDivert32.sys";
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "WinDivert.dll")) || !File.Exists(Path.Combine(AppContext.BaseDirectory, driver)))
        {
            throw new FileNotFoundException(ResUI.AppRoutingDriverRequired);
        }
        await _gate.WaitAsync();
        try
        {
            if (_shuttingDown || !config.AppRouting.Enabled) { return; }
            if (config.TunModeItem.EnableTun) { throw new InvalidOperationException(ResUI.AppRoutingTunConflict); }
            var snapshot = JsonUtils.DeepCopy(config);
            var rules = snapshot.AppRouting.Rules.Where(r => r.Enabled).ToList();
            Validate(rules);
            if (rules.Count == 0) { throw new InvalidOperationException("Add at least one enabled application rule."); }
            _preparation = new CancellationTokenSource();
            if (!_runtime.IsRunning) { State = AppRoutingState.Starting; }
            var plan = await PreparePlan(snapshot, rules, _preparation.Token);
            _preparation.Token.ThrowIfCancellationRequested();
            if (_shuttingDown || !config.AppRouting.Enabled) { return; }
            await _runtime.ApplyAsync(plan, _preparation.Token);
            State = AppRoutingState.Running;
            _lastError = null;
            WatchRuntime();
        }
        catch
        {
            // Staging failure preserves the old engine, its cores, and its policy.
            State = _runtime.IsRunning ? AppRoutingState.Running : AppRoutingState.Faulted;
            throw;
        }
        finally
        {
            _preparation?.Dispose();
            _preparation = null;
            _gate.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<RouteRuntimePlan> PreparePlan(Config config, IReadOnlyList<AppRouteRule> rules, CancellationToken token)
    {
        var profiles = new Dictionary<string, RouteProfilePlan>();
        var shared = new Dictionary<(string, bool), RouteProfilePlan>();
        var blocking = rules.Any(r => r.Kind == AppRouteKind.Profile && r.ApplyBlockingRules)
            ? AppRouteProfileConfig.GetBlockingRouting(config, await ConfigHandler.GetDefaultRouting(config)) : null;
        foreach (var rule in rules.Where(r => r.Kind == AppRouteKind.Profile))
        {
            token.ThrowIfCancellationRequested();
            var choice = (rule.ProfileId, rule.ApplyBlockingRules);
            if (!shared.TryGetValue(choice, out var plan))
            {
                var source = await AppManager.Instance.GetProfileItem(rule.ProfileId)
                    ?? throw new InvalidOperationException("Saved application-routing profile no longer exists.");
                if (source.ConfigType == EConfigType.Custom) { throw new InvalidOperationException("Custom configuration files cannot be used as application-routing profiles."); }
                var node = JsonUtils.DeepCopy(source);
                node.CoreType = ECoreType.Xray;
                var isolatedConfig = JsonUtils.DeepCopy(config);
                isolatedConfig.TunModeItem.EnableTun = false;
                var built = await CoreConfigContextBuilder.Build(isolatedConfig, node);
                if (!built.Success || built.Context.RunCoreType != ECoreType.Xray)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, built.ValidatorResult.Errors));
                }
                var endpoint = new AppRouteRule { SocksUsername = "app-route", SocksPassword = "template", ApplyBlockingRules = rule.ApplyBlockingRules };
                var template = AppRouteProfileConfig.Generate(built.Context with { RoutingItem = blocking }, endpoint);
                var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray);
                var core = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var error);
                if (string.IsNullOrEmpty(core)) { throw new FileNotFoundException(error); }
                var environment = coreInfo.Environment?.Where(p => p.Value != null).OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value!);
                // Fingerprint the effective generated configuration: includes profile, chain/balancer,
                // routing, DNS, transport and core settings, without random listener credentials/ports.
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(core + "\n" + JsonUtils.Serialize(environment) + "\n" + template)));
                plan = new(key, cancellation => RouteProfileInstance.StartAsync(template, core, environment, cancellation));
                shared.Add(choice, plan);
            }
            profiles.Add(rule.Id, plan);
        }
        return new(rules, profiles);
    }

    public Task RefreshAsync(Config config) => IsEnabled && config.AppRouting.Enabled ? StartAsync(config) : Task.CompletedTask;

    private void WatchRuntime()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = new CancellationTokenSource();
        _ = ObserveRuntime(_runtime.Generation, _runtime.WaitForFailureAsync(_watch.Token), _watch.Token);
    }

    private async Task ObserveRuntime(long generation, Task<Exception?> failure, CancellationToken token)
    {
        try
        {
            var error = await failure;
            await _gate.WaitAsync(token);
            try
            {
                if (generation != _runtime.Generation) { return; }
                await _runtime.StopAsync();
                State = AppRoutingState.Faulted;
                Report("Application routing stopped: " + (error?.Message ?? "The capture engine stopped unexpectedly."));
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Logging.SaveLog("AppRouting supervision", ex); }
    }

    private void Report(string message)
    {
        var error = ResUI.AppRoutingRouteError + ": " + message;
        if (_lastError == error) { return; }
        _lastError = error;
        Logging.SaveLog(error);
        NoticeManager.Instance.Enqueue(error);
    }

    public async Task StopAsync()
    {
        // Abort a pending core readiness wait before waiting for lifecycle serialization.
        try { _preparation?.Cancel(); } catch (ObjectDisposedException) { }
        await _gate.WaitAsync();
        try
        {
            State = AppRoutingState.Stopping;
            _watch?.Cancel();
            _watch?.Dispose();
            _watch = null;
            await _runtime.StopAsync();
            State = AppRoutingState.Stopped;
        }
        finally { _gate.Release(); }
    }

    public async Task ShutdownAsync()
    {
        _shuttingDown = true;
        await StopAsync();
    }
}
