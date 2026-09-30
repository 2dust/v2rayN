using System.Diagnostics;
using System.ComponentModel;
using System.IO;

namespace v2rayN.Web.Launcher;

public enum WebLifecycleAction
{
    Stop,
    Restart,
    RestoreAndRestart,
    Shutdown,
}

public enum WebLifecycleOwner
{
    None,
    ExternalSupervisor,
    NativeProcess,
}

public sealed record WebLifecyclePlan(WebLifecycleAction Action, WebLifecycleOwner Owner)
{
    public bool ShouldStartReplacement => Owner == WebLifecycleOwner.NativeProcess
        && Action is WebLifecycleAction.Restart or WebLifecycleAction.RestoreAndRestart;

    public bool ExitsForSupervisor => Owner == WebLifecycleOwner.ExternalSupervisor
        && Action is WebLifecycleAction.Restart or WebLifecycleAction.RestoreAndRestart;
}

public static class WebLifecyclePlanner
{
    public static WebLifecyclePlan Create(
        WebLifecycleAction action,
        bool isLinux,
        bool daemonEnvironment,
        bool containerEnvironment)
    {
        var owner = action is not (WebLifecycleAction.Restart or WebLifecycleAction.RestoreAndRestart)
            ? WebLifecycleOwner.None
            : daemonEnvironment || containerEnvironment
                ? WebLifecycleOwner.ExternalSupervisor
                : isLinux
                    ? WebLifecycleOwner.NativeProcess
                    : WebLifecycleOwner.None;
        return new WebLifecyclePlan(action, owner);
    }
}

public sealed record WebReplacementCommand(string LauncherPath, string[] Arguments, string WorkingDirectory)
{
    public ProcessStartInfo ToProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = LauncherPath,
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
        };
        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    public static string? GetManagedEntryPoint(string? processPath, IReadOnlyList<string> commandLine) =>
        !string.IsNullOrWhiteSpace(processPath)
        && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        && commandLine.Count > 0
        && commandLine[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? commandLine[0]
            : null;

    public static WebReplacementCommand? Create(
        string? setsidPath,
        string? processPath,
        string? managedEntryPoint,
        IEnumerable<string> hostArguments)
    {
        if (string.IsNullOrWhiteSpace(setsidPath) || string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var arguments = new List<string>();
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(managedEntryPoint)
                || !managedEntryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            arguments.Add(Path.GetFullPath(processPath));
            arguments.Add(Path.GetFullPath(managedEntryPoint));
        }
        else
        {
            arguments.Add(Path.GetFullPath(processPath));
        }

        arguments.Add(WebLaunchOptions.BackgroundChildFlag);
        arguments.AddRange(hostArguments);
        return new WebReplacementCommand(Path.GetFullPath(setsidPath), arguments.ToArray(), Environment.CurrentDirectory);
    }
}

public interface IWebReplacementProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    void KillTree();
}

public interface IWebReplacementProcessStarter
{
    IWebReplacementProcess? Start(WebReplacementCommand command);
}

public interface IWebInstanceOwnershipProbe
{
    bool IsLockHeld(string lockPath);
    int? ReadOwnerProcessId(string lockPath);
}

public sealed class FileWebInstanceOwnershipProbe : IWebInstanceOwnershipProbe
{
    public bool IsLockHeld(string lockPath) => File.Exists(lockPath) && WebInstanceLock.IsHeld(lockPath);

    public int? ReadOwnerProcessId(string lockPath) => WebInstanceLock.ReadOwnerProcessId(lockPath);
}

public sealed class LinuxReplacementProcessStarter : IWebReplacementProcessStarter
{
    public IWebReplacementProcess? Start(WebReplacementCommand command)
    {
        try
        {
            var process = Process.Start(command.ToProcessStartInfo());
            return process is null ? null : new ReplacementProcess(process);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or Win32Exception)
        {
            throw new InvalidOperationException(
                $"Could not start '{command.LauncherPath}' for the native Web replacement: {exception.Message}",
                exception);
        }
    }

    private sealed class ReplacementProcess(Process process) : IWebReplacementProcess
    {
        public int Id => process.Id;

        public bool HasExited
        {
            get
            {
                try { return process.HasExited; }
                catch (InvalidOperationException) { return true; }
            }
        }

        public void KillTree()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                // A process may exit between the check and the kill request.
            }
        }

        public void Dispose() => process.Dispose();
    }
}

public sealed record WebRestartHandoffResult(bool Success, string Message, int? ReplacementProcessId = null);

public sealed class NativeWebRestartCoordinator
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IWebHealthProbe _healthProbe;
    private readonly IWebReplacementProcessStarter _processStarter;
    private readonly IWebInstanceOwnershipProbe _ownershipProbe;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public NativeWebRestartCoordinator(
        IWebHealthProbe healthProbe,
        IWebReplacementProcessStarter processStarter,
        IWebInstanceOwnershipProbe ownershipProbe,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _healthProbe = healthProbe;
        _processStarter = processStarter;
        _ownershipProbe = ownershipProbe;
        _timeout = timeout ?? DefaultTimeout;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _delay = delay ?? Task.Delay;
        if (_timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (_pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
    }

    public async Task<WebRestartHandoffResult> StartReplacementAfterOwnerReleaseAsync(
        string lockPath,
        Uri healthUri,
        WebReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var timeout = Stopwatch.StartNew();
        while (_ownershipProbe.IsLockHeld(lockPath))
        {
            if (timeout.Elapsed >= _timeout)
            {
                return new(false, "The previous Web owner did not release the current data-scope instance lock.");
            }
            await _delay(Min(_pollInterval, _timeout - timeout.Elapsed), cancellationToken);
        }

        IWebReplacementProcess? replacement;
        try
        {
            replacement = _processStarter.Start(command);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or Win32Exception)
        {
            return new(false, $"The native replacement process could not be started: {exception.Message}");
        }
        if (replacement is null)
        {
            return new(false, "The native replacement process could not be started.");
        }

        using (replacement)
        {
            while (timeout.Elapsed < _timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_ownershipProbe.IsLockHeld(lockPath))
                {
                    var ownerProcessId = _ownershipProbe.ReadOwnerProcessId(lockPath);
                    if (ownerProcessId.HasValue)
                    {
                        var health = await _healthProbe.ProbeAsync(healthUri, cancellationToken);
                        if (health.IsHealthy && health.InstanceProcessId == ownerProcessId)
                        {
                            return new(true, "The native replacement Web instance is healthy.", ownerProcessId);
                        }
                    }
                }

                if (replacement.HasExited)
                {
                    return new(false, "The native replacement process exited before its Web health endpoint became ready.", replacement.Id);
                }

                await _delay(Min(_pollInterval, _timeout - timeout.Elapsed), cancellationToken);
            }

            var currentOwner = _ownershipProbe.IsLockHeld(lockPath)
                ? _ownershipProbe.ReadOwnerProcessId(lockPath)
                : null;
            if (!replacement.HasExited && (!currentOwner.HasValue || currentOwner == replacement.Id))
            {
                replacement.KillTree();
            }
            return new(false, "The native replacement Web instance did not become healthy before the startup timeout.", replacement.Id);
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}
