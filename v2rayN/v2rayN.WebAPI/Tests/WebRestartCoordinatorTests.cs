using v2rayN.WebAPI.Launcher;

namespace v2rayN.WebAPI.Tests;

public class WebRestartCoordinatorTests
{
    [Test]
    public async Task NativeSelfContainedRestartUsesDetachedBackgroundChildCommand()
    {
        if (!OperatingSystem.IsLinux()) return;

        var command = WebReplacementCommand.Create(
            "/usr/bin/setsid",
            "/opt/v2rayN.WebAPI",
            null,
            ["--urls", "http://127.0.0.1:5090"]);

        await (command is not null).Should().BeTrue();
        await (command!.LauncherPath == "/usr/bin/setsid").Should().BeTrue();
        await command.Arguments.SequenceEqual([
            "/opt/v2rayN.WebAPI",
            WebLaunchOptions.BackgroundChildFlag,
            "--urls",
            "http://127.0.0.1:5090",
        ]).Should().BeTrue();
        await command.ToProcessStartInfo().UseShellExecute.Should().BeFalse();
    }

    [Test]
    public async Task FrameworkDependentRestartPreservesDotnetDllAndHostArguments()
    {
        if (!OperatingSystem.IsLinux()) return;

        var processPath = "/usr/bin/dotnet";
        var entryPoint = WebReplacementCommand.GetManagedEntryPoint(processPath, ["/opt/v2rayN/v2rayN.WebAPI.dll", "--urls"]);
        var command = WebReplacementCommand.Create(
            "/usr/bin/setsid",
            processPath,
            entryPoint,
            ["--urls", "http://127.0.0.1:5090", "--contentRoot", "/opt/v2rayN"]);

        await (entryPoint == "/opt/v2rayN/v2rayN.WebAPI.dll").Should().BeTrue();
        await command!.Arguments.SequenceEqual([
            "/usr/bin/dotnet",
            "/opt/v2rayN/v2rayN.WebAPI.dll",
            WebLaunchOptions.BackgroundChildFlag,
            "--urls",
            "http://127.0.0.1:5090",
            "--contentRoot",
            "/opt/v2rayN",
        ]).Should().BeTrue();
        await (WebReplacementCommand.Create("/usr/bin/setsid", processPath, null, []) is null).Should().BeTrue();
    }

    [Test]
    public async Task SystemdAndContainerOwnRestartWhileNativeLinuxOwnsItsReplacement()
    {
        var systemd = WebLifecyclePlanner.Create(WebLifecycleAction.RestoreAndRestart, true, daemonEnvironment: true, containerEnvironment: false);
        var cgroupManaged = WebStopper.IsSystemdServiceCgroup("0::/system.slice/v2rayn-web.service");
        var cgroupSystemd = WebLifecyclePlanner.Create(WebLifecycleAction.RestoreAndRestart, true, daemonEnvironment: cgroupManaged, containerEnvironment: false);
        var container = WebLifecyclePlanner.Create(WebLifecycleAction.RestoreAndRestart, true, daemonEnvironment: false, containerEnvironment: true);
        var native = WebLifecyclePlanner.Create(WebLifecycleAction.RestoreAndRestart, true, daemonEnvironment: false, containerEnvironment: false);
        var nativeRestart = WebLifecyclePlanner.Create(WebLifecycleAction.Restart, true, daemonEnvironment: false, containerEnvironment: false);
        var stop = WebLifecyclePlanner.Create(WebLifecycleAction.Stop, true, daemonEnvironment: false, containerEnvironment: false);
        var shutdown = WebLifecyclePlanner.Create(WebLifecycleAction.Shutdown, true, daemonEnvironment: false, containerEnvironment: false);

        await systemd.ExitsForSupervisor.Should().BeTrue();
        await cgroupSystemd.ExitsForSupervisor.Should().BeTrue();
        await container.ExitsForSupervisor.Should().BeTrue();
        await systemd.ShouldStartReplacement.Should().BeFalse();
        await cgroupSystemd.ShouldStartReplacement.Should().BeFalse();
        await container.ShouldStartReplacement.Should().BeFalse();
        await native.ShouldStartReplacement.Should().BeTrue();
        await nativeRestart.ShouldStartReplacement.Should().BeTrue();
        await stop.ShouldStartReplacement.Should().BeFalse();
        await shutdown.ShouldStartReplacement.Should().BeFalse();
    }

    [Test]
    public async Task HandoffWaitsForCurrentScopeLockAndDoesNotTouchAnotherDataHome()
    {
        var currentScope = "/data/one/v2rayN.WebAPI.instance.lock";
        var otherScope = "/data/two/v2rayN.WebAPI.instance.lock";
        var ownership = new FakeOwnershipProbe();
        ownership.Set(currentScope, held: true, owner: 111);
        ownership.Set(otherScope, held: true, owner: 222);
        var starter = new FakeProcessStarter(() =>
        {
            var lockWasReleasedBeforeSpawn = !ownership.IsLockHeld(currentScope);
            ownership.Set(currentScope, held: true, owner: 4242);
            return (new FakeReplacementProcess(4242), lockWasReleasedBeforeSpawn);
        });
        var health = new FakeHealthProbe(4242);
        var releasedOldOwner = false;
        async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
            if (!releasedOldOwner)
            {
                ownership.Set(currentScope, held: false, owner: null);
                releasedOldOwner = true;
            }
        }

        var coordinator = new NativeWebRestartCoordinator(
            health,
            starter,
            ownership,
            timeout: TimeSpan.FromSeconds(1),
            pollInterval: TimeSpan.FromMilliseconds(1),
            delay: Delay);
        var result = await coordinator.StartReplacementAfterOwnerReleaseAsync(
            currentScope,
            new Uri("http://127.0.0.1:5080/api/health"),
            NewCommand());

        await result.Success.Should().BeTrue();
        await starter.StartCount.Should().BeEqualTo(1);
        await starter.StartObservedReleasedLock.Should().BeTrue();
        await ownership.ObservedPaths.All(path => path == currentScope).Should().BeTrue();
        await ownership.IsLockHeld(otherScope).Should().BeTrue();
        await (health.LastUri == new Uri("http://127.0.0.1:5080/api/health")).Should().BeTrue();
    }

    [Test]
    public async Task HandoffNeverSpawnsWhileOldOwnerStillHoldsLock()
    {
        var lockPath = "/data/current/v2rayN.WebAPI.instance.lock";
        var ownership = new FakeOwnershipProbe();
        ownership.Set(lockPath, held: true, owner: 123);
        var starter = new FakeProcessStarter(() => (new FakeReplacementProcess(456), false));
        var coordinator = new NativeWebRestartCoordinator(
            new FakeHealthProbe(456),
            starter,
            ownership,
            timeout: TimeSpan.FromMilliseconds(30),
            pollInterval: TimeSpan.FromMilliseconds(2));

        var result = await coordinator.StartReplacementAfterOwnerReleaseAsync(
            lockPath,
            new Uri("http://127.0.0.1:5080/api/health"),
            NewCommand());

        await result.Success.Should().BeFalse();
        await result.Message.Should().Contain("did not release");
        await starter.StartCount.Should().BeEqualTo(0);
    }

    [Test]
    public async Task FailedReplacementStartupIsReportedWithoutRetryingOrKillingOtherInstances()
    {
        var lockPath = "/data/current/v2rayN.WebAPI.instance.lock";
        var otherScope = "/data/other/v2rayN.WebAPI.instance.lock";
        var ownership = new FakeOwnershipProbe();
        ownership.Set(otherScope, held: true, owner: 222);
        var starter = new FakeProcessStarter(() => (new FakeReplacementProcess(456, hasExited: true), false));
        var coordinator = new NativeWebRestartCoordinator(
            new FakeHealthProbe(null),
            starter,
            ownership,
            timeout: TimeSpan.FromSeconds(1),
            pollInterval: TimeSpan.FromMilliseconds(1));

        var result = await coordinator.StartReplacementAfterOwnerReleaseAsync(
            lockPath,
            new Uri("http://127.0.0.1:5080/api/health"),
            NewCommand());

        await result.Success.Should().BeFalse();
        await result.Message.Should().Contain("exited before its Web health endpoint");
        await starter.StartCount.Should().BeEqualTo(1);
        await ownership.IsLockHeld(otherScope).Should().BeTrue();
    }

    private static WebReplacementCommand NewCommand() => new(
        "/usr/bin/setsid",
        ["/opt/v2rayN.WebAPI", WebLaunchOptions.BackgroundChildFlag],
        "/opt");

    private sealed class FakeOwnershipProbe : IWebInstanceOwnershipProbe
    {
        private readonly Dictionary<string, (bool Held, int? Owner)> _scopes = new(StringComparer.Ordinal);

        public List<string> ObservedPaths { get; } = [];

        public void Set(string path, bool held, int? owner) => _scopes[path] = (held, owner);

        public bool IsLockHeld(string lockPath)
        {
            ObservedPaths.Add(lockPath);
            return _scopes.GetValueOrDefault(lockPath).Held;
        }

        public int? ReadOwnerProcessId(string lockPath)
        {
            ObservedPaths.Add(lockPath);
            return _scopes.GetValueOrDefault(lockPath).Owner;
        }
    }

    private sealed class FakeProcessStarter(Func<(FakeReplacementProcess Process, bool LockReleased)> start) : IWebReplacementProcessStarter
    {
        public int StartCount { get; private set; }
        public bool StartObservedReleasedLock { get; private set; }

        public IWebReplacementProcess? Start(WebReplacementCommand command)
        {
            StartCount++;
            var result = start();
            StartObservedReleasedLock = result.LockReleased;
            return result.Process;
        }
    }

    private sealed class FakeReplacementProcess(int id, bool hasExited = false) : IWebReplacementProcess
    {
        public int Id { get; } = id;
        public bool HasExited { get; } = hasExited;
        public bool Killed { get; private set; }
        public void KillTree() => Killed = true;
        public void Dispose() { }
    }

    private sealed class FakeHealthProbe(int? processId) : IWebHealthProbe
    {
        public Uri? LastUri { get; private set; }

        public Task<WebHealthProbeResult> ProbeAsync(Uri healthUri, CancellationToken cancellationToken)
        {
            LastUri = healthUri;
            return Task.FromResult(new WebHealthProbeResult(processId.HasValue, processId));
        }
    }
}
