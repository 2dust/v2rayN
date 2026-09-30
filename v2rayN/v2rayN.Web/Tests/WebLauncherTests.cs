using v2rayN.Web.Launcher;

namespace v2rayN.Web.Tests;

public class WebLauncherTests
{
    [Test]
    public async Task StopFlagSelectsStopModeAndIsNotForwardedToHost()
    {
        var options = WebLaunchOptions.Parse(
            ["--stop", "--foreground", "--background", "--urls", "http://127.0.0.1:5090"],
            isLinux: true,
            daemonEnvironment: false,
            containerEnvironment: false);

        await (options.Mode == WebLaunchMode.Stop).Should().BeTrue();
        await options.HostArguments.SequenceEqual(["--urls", "http://127.0.0.1:5090"]).Should().BeTrue();
    }

    [Test]
    public async Task ForegroundFlagAlwaysSelectsForegroundMode()
    {
        var options = WebLaunchOptions.Parse(["--foreground"], isLinux: true, daemonEnvironment: false, containerEnvironment: false);
        await (options.Mode == WebLaunchMode.Foreground).Should().BeTrue();
    }

    [Test]
    public async Task BackgroundFlagSelectsLauncherMode()
    {
        var options = WebLaunchOptions.Parse(["--background"], isLinux: true, daemonEnvironment: true, containerEnvironment: true);
        await (options.Mode == WebLaunchMode.BackgroundLauncher).Should().BeTrue();
    }

    [Test]
    public async Task BackgroundChildRunsTheForegroundHostWithoutRecursiveLauncherArguments()
    {
        var options = WebLaunchOptions.Parse(["--background-child", "--no-open", "--urls", "http://127.0.0.1:5090"], isLinux: true, daemonEnvironment: false, containerEnvironment: false);
        await (options.Mode == WebLaunchMode.BackgroundChild).Should().BeTrue();
        await options.NoOpen.Should().BeTrue();
        await options.HostArguments.SequenceEqual(["--urls", "http://127.0.0.1:5090"]).Should().BeTrue();
    }

    [Test]
    public async Task SystemdAndContainerDefaultsRemainForeground()
    {
        var systemd = WebLaunchOptions.Parse([], isLinux: true, daemonEnvironment: true, containerEnvironment: false);
        var container = WebLaunchOptions.Parse([], isLinux: true, daemonEnvironment: false, containerEnvironment: true);

        await (systemd.Mode == WebLaunchMode.Foreground).Should().BeTrue();
        await (container.Mode == WebLaunchMode.Foreground).Should().BeTrue();
        await (WebLaunchOptions.Parse(["--foreground"], true, true, true).Mode == WebLaunchMode.Foreground).Should().BeTrue();
    }

    [Test]
    public async Task NativeLinuxDefaultUsesTheBackgroundLauncher()
    {
        var desktop = WebLaunchOptions.Parse([], isLinux: true, daemonEnvironment: false, containerEnvironment: false);
        var otherPlatform = WebLaunchOptions.Parse([], isLinux: false, daemonEnvironment: false, containerEnvironment: false);

        await (desktop.Mode == WebLaunchMode.BackgroundLauncher).Should().BeTrue();
        await (otherPlatform.Mode == WebLaunchMode.Foreground).Should().BeTrue();
    }

    [Test]
    public async Task SystemdEnvironmentDetectionRecognizesInvocationMarkers()
    {
        var environment = new Dictionary<string, string?> { ["INVOCATION_ID"] = "unit-run-id" };
        await LauncherEnvironment.IsDaemonEnvironment(environment).Should().BeTrue();
        await LauncherEnvironment.IsContainerEnvironment(new Dictionary<string, string?> { ["container"] = "podman" }).Should().BeTrue();
    }

    [Test]
    public async Task ExistingHealthyInstanceIsReusedAndBrowserAdapterIsInvoked()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        await WebInstanceLock.TryAcquire(lockPath, writeOwner: true, out var heldLock).Should().BeTrue();
        using (heldLock)
        {
            var browser = new FakeBrowserOpener();
            var result = await ExistingInstanceHandler.TryReuseAsync(
                lockPath,
                new Uri("http://127.0.0.1:5080/api/health"),
                new Uri("http://127.0.0.1:5080/"),
                new FakeHealthProbe(true, WebInstanceLock.ReadOwnerProcessId(lockPath)),
                browser,
                noOpen: false,
                TimeSpan.FromSeconds(1));

            await result.Existing.Should().BeTrue();
            await result.BrowserOpened.Should().BeTrue();
            await (browser.LastUri == new Uri("http://127.0.0.1:5080/")).Should().BeTrue();
        }
    }

    [Test]
    public async Task HealthyEndpointFromAnotherInstanceIsNotReusedForThisDataHome()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        await WebInstanceLock.TryAcquire(lockPath, writeOwner: true, out var heldLock).Should().BeTrue();
        using (heldLock)
        {
            var expectedOwner = WebInstanceLock.ReadOwnerProcessId(lockPath);
            var browser = new FakeBrowserOpener();
            var timedOut = false;
            try
            {
                await ExistingInstanceHandler.TryReuseAsync(
                    lockPath,
                    new Uri("http://127.0.0.1:5080/api/health"),
                    new Uri("http://127.0.0.1:5080/"),
                    new FakeHealthProbe(true, expectedOwner + 1),
                    browser,
                    noOpen: false,
                    TimeSpan.FromMilliseconds(50));
            }
            catch (TimeoutException)
            {
                timedOut = true;
            }

            await timedOut.Should().BeTrue();
            await (browser.LastUri is null).Should().BeTrue();
        }
    }

    [Test]
    public async Task UnlockedStaleLockFileCanBeRecovered()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        await File.WriteAllTextAsync(lockPath, "99999999\n");

        var result = await ExistingInstanceHandler.TryReuseAsync(
            lockPath,
            new Uri("http://127.0.0.1:5080/api/health"),
            new Uri("http://127.0.0.1:5080/"),
            new FakeHealthProbe(false),
            new FakeBrowserOpener(),
            noOpen: true,
            TimeSpan.FromMilliseconds(100));

        await result.Existing.Should().BeFalse();
        await WebInstanceLock.TryAcquire(lockPath, writeOwner: false, out var recovered).Should().BeTrue();
        recovered!.Dispose();
    }

    [Test]
    public async Task StopTreatsAnUnlockedStaleLockAsAlreadyStopped()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        await File.WriteAllTextAsync(lockPath, "123\n");
        var signals = new FakeSignalSender();
        var stopper = new WebStopper(new FakeHealthProbe(true, 123), signals);

        var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

        await (result == WebStopResult.NotRunning).Should().BeTrue();
        await (signals.ProcessIds.Count == 0).Should().BeTrue();
    }

    [Test]
    public async Task StopSendsOneSigTermToVerifiedOwnerAndWaitsForLockRelease()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        using (heldLock)
        {
            var signals = new FakeSignalSender();
            var health = new FakeHealthProbe(true, 123);
            signals.OnSignal = processId =>
            {
                _ = ReleaseLockAfterDelayAsync(heldLock, health);
            };
            var stopper = new WebStopper(
                health,
                signals,
                stopTimeout: TimeSpan.FromMilliseconds(500),
                pollInterval: TimeSpan.FromMilliseconds(10));

            var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

            await (result == WebStopResult.Stopped).Should().BeTrue();
            await signals.ProcessIds.SequenceEqual([123]).Should().BeTrue();
            await (health.ProbeCount >= 2).Should().BeTrue();
        }
    }

    [Test]
    public async Task StopRefusesToCompeteWithSystemdManagedLifecycle()
    {
        await WebStopper.IsSystemdServiceCgroup("0::/system.slice/v2rayn-web.service").Should().BeTrue();
        await WebStopper.IsSystemdServiceCgroup("0::/user.slice/user-1000.slice/session-2.scope").Should().BeFalse();
    }

    [Test]
    public async Task StopDoesNotReportSuccessWhileAKnownCoreChildRemainsAlive()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        using (heldLock)
        {
            var health = new FakeHealthProbe(true, 123, [Environment.ProcessId]);
            var signals = new FakeSignalSender();
            signals.OnSignal = processId => _ = ReleaseLockAfterDelayAsync(heldLock, health);
            var stopper = new WebStopper(health, signals, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(10));

            var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

            await (result == WebStopResult.CoreProcessStillRunning).Should().BeTrue();
            await signals.ProcessIds.SequenceEqual([123]).Should().BeTrue();
        }
    }

    [Test]
    public async Task StopNeverSignalsWhenHealthPidDoesNotMatchLockOwner()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        using var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        var signals = new FakeSignalSender();
        var stopper = new WebStopper(new FakeHealthProbe(true, 456), signals);

        var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

        await (result == WebStopResult.IdentityUnverified).Should().BeTrue();
        await (signals.ProcessIds.Count == 0).Should().BeTrue();
    }

    [Test]
    public async Task StopNeverSignalsWhenHealthCannotConfirmTheInstance()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        using var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        var signals = new FakeSignalSender();
        var stopper = new WebStopper(new FakeHealthProbe(false, 123), signals);

        var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

        await (result == WebStopResult.IdentityUnverified).Should().BeTrue();
        await (signals.ProcessIds.Count == 0).Should().BeTrue();
    }

    [Test]
    public async Task StopTimesOutWithoutEscalatingBeyondOneSigTerm()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        using var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        var signals = new FakeSignalSender();
        var stopper = new WebStopper(
            new FakeHealthProbe(true, 123),
            signals,
            stopTimeout: TimeSpan.FromMilliseconds(60),
            pollInterval: TimeSpan.FromMilliseconds(5));

        var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

        await (result == WebStopResult.TimedOut).Should().BeTrue();
        await signals.ProcessIds.SequenceEqual([123]).Should().BeTrue();
        await WebInstanceLock.IsHeld(lockPath).Should().BeTrue();
    }

    [Test]
    public async Task StopTimeoutReportsLastObservedCleanupStage()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var lockPath = Path.Combine(directory.Path, "instance.lock");
        using var heldLock = await AcquireLockWithOwnerAsync(lockPath, 123);
        var health = new FakeHealthProbe(true, 123);
        health.SetShutdownStage("Core stop");
        var stopper = new WebStopper(
            health,
            new FakeSignalSender(),
            stopTimeout: TimeSpan.FromMilliseconds(60),
            pollInterval: TimeSpan.FromMilliseconds(5));

        var result = await stopper.StopAsync(lockPath, new Uri("http://127.0.0.1:5080/api/health"));

        await (result == WebStopResult.TimedOut).Should().BeTrue();
        await (stopper.LastObservedShutdownStage == "Core stop").Should().BeTrue();
        await LauncherMessages.StopMessage(result, LauncherLocale.English, stopper.LastObservedShutdownStage)
            .Contains("Core stop", StringComparison.Ordinal).Should().BeTrue();
    }

    [Test]
    public async Task LockIsReleasedWhenOwnerExitsAndDataHomesUseDistinctLocks()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var firstPath = Path.Combine(directory.Path, "one", "instance.lock");
        var secondPath = Path.Combine(directory.Path, "two", "instance.lock");
        await WebInstanceLock.TryAcquire(firstPath, writeOwner: true, out var first).Should().BeTrue();
        await WebInstanceLock.TryAcquire(secondPath, writeOwner: true, out var second).Should().BeTrue();
        using (first)
        using (second)
        {
            await WebInstanceLock.IsHeld(firstPath).Should().BeTrue();
            await WebInstanceLock.IsHeld(secondPath).Should().BeTrue();
        }
        await WebInstanceLock.IsHeld(firstPath).Should().BeFalse();
        await WebInstanceLock.TryAcquire(firstPath, writeOwner: false, out var recovered).Should().BeTrue();
        recovered!.Dispose();
    }

    [Test]
    public async Task LauncherMessagesUseTheSharedLocaleResources()
    {
        var command = LauncherMessages.ExecutableCommand("/opt/v2rayn/v2rayN.Web");
        var chinese = LauncherMessages.Started("http://127.0.0.1:5080", command, LauncherLocale.SimplifiedChinese);
        var english = LauncherMessages.Started("http://127.0.0.1:5080", command, LauncherLocale.English);

        await chinese.Contains("管理页面", StringComparison.Ordinal).Should().BeTrue();
        await chinese.Contains("Ctrl+C 不会停止", StringComparison.Ordinal).Should().BeTrue();
        await chinese.Contains("./v2rayN.Web --stop", StringComparison.Ordinal).Should().BeTrue();
        await LauncherMessages.AlreadyRunning("http://127.0.0.1:5080", command, false, LauncherLocale.TraditionalChinese)
            .Contains("--stop", StringComparison.Ordinal).Should().BeTrue();
        await (LauncherMessages.Started("http://127.0.0.1:5080", command, LauncherLocale.TraditionalChinese)
            .Contains("--stop", StringComparison.Ordinal)).Should().BeTrue();
        await english.Contains("Web UI", StringComparison.Ordinal).Should().BeTrue();
        await LauncherMessages.ForegroundStarted("http://127.0.0.1:5080", LauncherLocale.English)
            .Contains("Ctrl+C", StringComparison.Ordinal).Should().BeTrue();
        await (LauncherMessages.StopMessage(WebStopResult.NotRunning, LauncherLocale.English)
            == "v2rayN Web is not running for this data directory.").Should().BeTrue();
        await LauncherMessages.StopMessage(WebStopResult.NotRunning, LauncherLocale.SimplifiedChinese)
            .Contains("数据目录", StringComparison.Ordinal).Should().BeTrue();
        await LauncherMessages.StopMessage(WebStopResult.NotRunning, LauncherLocale.TraditionalChinese)
            .Contains("資料目錄", StringComparison.Ordinal).Should().BeTrue();
        await LauncherMessages.StopMessage(WebStopResult.SupervisorManaged, LauncherLocale.English)
            .Contains("systemctl stop", StringComparison.Ordinal).Should().BeTrue();
        await LauncherMessages.StopMessage(WebStopResult.CoreProcessStillRunning, LauncherLocale.SimplifiedChinese)
            .Contains("Core", StringComparison.Ordinal).Should().BeTrue();
        await (LauncherMessages.ExecutableCommand("/usr/share/dotnet/dotnet", "/opt/v2rayn/v2rayN.Web.dll")
            == "\"/usr/share/dotnet/dotnet\" \"/opt/v2rayn/v2rayN.Web.dll\"").Should().BeTrue();
        await (LauncherMessages.ResolveLocale("zh_Hant_TW") == LauncherLocale.TraditionalChinese).Should().BeTrue();
        await (LauncherMessages.ResolveLocale("zh_CN.UTF-8") == LauncherLocale.SimplifiedChinese).Should().BeTrue();
        await (LauncherMessages.ResolveLocale("fr_FR.UTF-8") == LauncherLocale.English).Should().BeTrue();
    }

    private static async Task<WebInstanceLock> AcquireLockWithOwnerAsync(string lockPath, int ownerProcessId)
    {
        await File.WriteAllTextAsync(lockPath, $"{ownerProcessId}\n");
        await WebInstanceLock.TryAcquire(lockPath, writeOwner: false, out var heldLock).Should().BeTrue();
        return heldLock!;
    }

    private static async Task ReleaseLockAfterDelayAsync(WebInstanceLock heldLock, FakeHealthProbe health)
    {
        await Task.Delay(40);
        heldLock.Dispose();
        health.SetResult(false, null);
    }

    private sealed class FakeHealthProbe(bool healthy, int? processId = null, int[]? coreProcessIds = null) : IWebHealthProbe
    {
        private bool _healthy = healthy;
        private int? _processId = processId;
        private string? _shutdownStage;
        private int[] _coreProcessIds = coreProcessIds ?? [];

        public int ProbeCount { get; private set; }

        public Task<WebHealthProbeResult> ProbeAsync(Uri healthUri, CancellationToken cancellationToken) =>
            RecordProbe();

        public void SetResult(bool isHealthy, int? instanceProcessId, int[]? coreProcessIds = null)
        {
            _healthy = isHealthy;
            _processId = instanceProcessId;
            if (coreProcessIds is not null) _coreProcessIds = coreProcessIds;
        }

        public void SetShutdownStage(string? stage) => _shutdownStage = stage;

        private Task<WebHealthProbeResult> RecordProbe()
        {
            ProbeCount++;
            return Task.FromResult(new WebHealthProbeResult(_healthy, _processId, _shutdownStage, _coreProcessIds));
        }
    }

    private sealed class FakeSignalSender : IProcessSignalSender
    {
        public List<int> ProcessIds { get; } = [];
        public Action<int>? OnSignal { get; set; }

        public bool SendSigTerm(int processId)
        {
            ProcessIds.Add(processId);
            OnSignal?.Invoke(processId);
            return true;
        }
    }

    private sealed class FakeBrowserOpener : IBrowserOpener
    {
        public Uri? LastUri { get; private set; }

        public bool TryOpen(Uri webUiUri)
        {
            LastUri = webUiUri;
            return true;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-launcher-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
