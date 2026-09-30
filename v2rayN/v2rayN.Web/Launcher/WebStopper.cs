using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using v2rayN.Web.Services;

namespace v2rayN.Web.Launcher;

public enum WebStopResult
{
    NotRunning,
    Stopped,
    IdentityUnverified,
    SupervisorManaged,
    CoreProcessStillRunning,
    SignalFailed,
    TimedOut,
}

public interface IProcessSignalSender
{
    bool SendSigTerm(int processId);
}

public sealed class LinuxProcessSignalSender : IProcessSignalSender
{
    private const int SigTerm = 15;

    public bool SendSigTerm(int processId) => OperatingSystem.IsLinux() && Kill(processId, SigTerm) == 0;

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);
}

public sealed class WebStopper
{
    private static readonly TimeSpan DefaultStopTimeout = RuntimeShutdownBudgets.LauncherWait;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IWebHealthProbe _healthProbe;
    private readonly IProcessSignalSender _signalSender;
    private readonly TimeSpan _stopTimeout;
    private readonly TimeSpan _pollInterval;

    public string? LastObservedShutdownStage { get; private set; }

    public WebStopper(
        IWebHealthProbe healthProbe,
        IProcessSignalSender signalSender,
        TimeSpan? stopTimeout = null,
        TimeSpan? pollInterval = null)
    {
        _healthProbe = healthProbe;
        _signalSender = signalSender;
        _stopTimeout = stopTimeout ?? DefaultStopTimeout;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        if (_stopTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(stopTimeout));
        }
        if (_pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }
    }

    public async Task<WebStopResult> StopAsync(
        string lockPath,
        Uri healthUri,
        CancellationToken cancellationToken = default)
    {
        if (!IsLockHeld(lockPath))
        {
            return WebStopResult.NotRunning;
        }

        var ownerProcessId = WebInstanceLock.ReadOwnerProcessId(lockPath);
        if (!ownerProcessId.HasValue)
        {
            return WebStopResult.IdentityUnverified;
        }

        if (IsManagedBySystemd(ownerProcessId.Value))
        {
            return WebStopResult.SupervisorManaged;
        }

        var health = await _healthProbe.ProbeAsync(healthUri, cancellationToken);
        if (!health.IsHealthy || health.InstanceProcessId != ownerProcessId)
        {
            return WebStopResult.IdentityUnverified;
        }

        // Recheck the lock and owner immediately before signaling, so a released or
        // replaced instance cannot turn a stale PID into an unrelated process signal.
        if (!IsLockHeld(lockPath))
        {
            return WebStopResult.NotRunning;
        }
        if (WebInstanceLock.ReadOwnerProcessId(lockPath) != ownerProcessId)
        {
            return WebStopResult.IdentityUnverified;
        }

        var coreProcessIds = new HashSet<int>(health.CoreProcessIds ?? []);

        if (!_signalSender.SendSigTerm(ownerProcessId.Value))
        {
            return WebStopResult.SignalFailed;
        }

        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < _stopTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Keep probing health while waiting, but only report completion once the
            // instance has released its lock (after host cleanup and service shutdown).
            var currentHealth = await _healthProbe.ProbeAsync(healthUri, cancellationToken);
            LastObservedShutdownStage = currentHealth.ShutdownStage ?? LastObservedShutdownStage;
            coreProcessIds.UnionWith(currentHealth.CoreProcessIds ?? []);
            var lockHeld = IsLockHeld(lockPath);
            var currentOwnerProcessId = lockHeld ? WebInstanceLock.ReadOwnerProcessId(lockPath) : null;
            var endpointStillIdentifiesOwner = currentHealth.IsHealthy
                && currentHealth.InstanceProcessId == ownerProcessId;
            if (!endpointStillIdentifiesOwner && (!lockHeld || currentOwnerProcessId != ownerProcessId))
            {
                var remainingCoreProcesses = coreProcessIds.Where(IsProcessAlive).ToArray();
                return remainingCoreProcesses.Length == 0
                    ? WebStopResult.Stopped
                    : WebStopResult.CoreProcessStillRunning;
            }

            var remaining = _stopTimeout - timeout.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, cancellationToken);
            }
        }

        return WebStopResult.TimedOut;
    }

    private static bool IsLockHeld(string lockPath) =>
        File.Exists(lockPath) && WebInstanceLock.IsHeld(lockPath);

    internal static bool IsManagedBySystemd(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;
        try
        {
            return IsSystemdServiceCgroup(File.ReadAllText($"/proc/{processId}/cgroup"));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool IsSystemdServiceCgroup(string? cgroup) =>
        !string.IsNullOrWhiteSpace(cgroup)
        && cgroup.Contains("system.slice/", StringComparison.Ordinal)
        && cgroup.Contains(".service", StringComparison.Ordinal);

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
