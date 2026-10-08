using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
namespace v2rayN.WebAPI.Launcher;

public interface IWebHealthProbe
{
    Task<WebHealthProbeResult> ProbeAsync(Uri healthUri, CancellationToken cancellationToken);
}

public sealed record WebHealthProbeResult(
    bool IsHealthy,
    int? InstanceProcessId,
    string? ShutdownStage = null,
    IReadOnlyList<int>? CoreProcessIds = null,
    string? CoreState = null,
    string? WebVersion = null,
    string? CoreProfileId = null);

public interface IBrowserOpener
{
    bool TryOpen(Uri apiUri);
}

public sealed class HttpWebHealthProbe : IWebHealthProbe
{
    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromMilliseconds(500),
        UseProxy = false,
    })
    {
        Timeout = TimeSpan.FromSeconds(1),
    };

    public async Task<WebHealthProbeResult> ProbeAsync(Uri healthUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(healthUri, cancellationToken);
            var instanceProcessId = response.Headers.TryGetValues("X-v2rayn-web-instance-pid", out var values)
                && int.TryParse(values.FirstOrDefault(), out var parsedProcessId)
                    ? parsedProcessId
                    : (int?)null;
            var shutdownStage = response.Headers.TryGetValues("X-v2rayn-web-shutdown-stage", out var stageValues)
                ? stageValues.FirstOrDefault()
                : null;
            var coreProcessIds = response.Headers.TryGetValues("X-v2rayn-web-core-process-ids", out var processValues)
                ? processValues.FirstOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => int.TryParse(value, out var processId) ? processId : 0)
                    .Where(processId => processId > 0)
                    .Distinct()
                    .ToArray()
                : [];
            var coreState = response.Headers.TryGetValues("X-v2rayn-web-core-state", out var stateValues)
                ? stateValues.FirstOrDefault()
                : null;
            var webVersion = response.Headers.TryGetValues("X-v2rayn-web-version", out var versionValues)
                ? versionValues.FirstOrDefault()
                : null;
            var coreProfileId = response.Headers.TryGetValues("X-v2rayn-web-core-profile-id", out var profileValues)
                ? profileValues.FirstOrDefault()
                : null;
            return new WebHealthProbeResult(response.IsSuccessStatusCode, instanceProcessId, shutdownStage,
                coreProcessIds, coreState, webVersion, coreProfileId);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new WebHealthProbeResult(false, null);
        }
    }
}

public sealed class LinuxXdgBrowserOpener : IBrowserOpener
{
    public bool TryOpen(Uri apiUri)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return false;
        }

        var xdgOpen = FindExecutable("xdg-open");
        if (xdgOpen is null)
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = xdgOpen,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(apiUri.ToString());
            return Process.Start(startInfo) is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    internal static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

public static class ExistingInstanceHandler
{
    public static async Task<ExistingInstanceResult> TryReuseAsync(
        string lockPath,
        Uri healthUri,
        Uri apiUri,
        IWebHealthProbe healthProbe,
        IBrowserOpener browserOpener,
        bool noOpen,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default)
    {
        if (WebInstanceLock.TryAcquire(lockPath, writeOwner: false, out var probe))
        {
            probe!.Dispose();
            return new ExistingInstanceResult(false, false);
        }

        var timeout = Stopwatch.StartNew();
        do
        {
            var ownerProcessId = WebInstanceLock.ReadOwnerProcessId(lockPath);
            var health = await healthProbe.ProbeAsync(healthUri, cancellationToken);
            if (health.IsHealthy && ownerProcessId.HasValue && health.InstanceProcessId == ownerProcessId)
            {
                if (!noOpen)
                {
                    var browserOpened = browserOpener.TryOpen(apiUri);
                    return new ExistingInstanceResult(true, browserOpened);
                }
                return new ExistingInstanceResult(true, false);
            }

            if (WebInstanceLock.TryAcquire(lockPath, writeOwner: false, out probe))
            {
                probe!.Dispose();
                return new ExistingInstanceResult(false, false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
        while (timeout.Elapsed < waitTimeout);

        throw new TimeoutException("The existing v2rayN.WebAPI instance holds its lock but did not become healthy.");
    }
}

public sealed record ExistingInstanceResult(bool Existing, bool BrowserOpened);

public sealed class WebLauncher
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IWebHealthProbe _healthProbe;
    private readonly IBrowserOpener _browserOpener;
    private readonly LauncherLocale _locale;

    public WebLauncher(IWebHealthProbe healthProbe, IBrowserOpener browserOpener, LauncherLocale locale)
    {
        _healthProbe = healthProbe;
        _browserOpener = browserOpener;
        _locale = locale;
    }

    public async Task<int> RunAsync(
        string executablePath,
        string[] hostArguments,
        string instanceLockPath,
        Uri healthUri,
        Uri apiUri,
        bool noOpen,
        CancellationToken cancellationToken = default)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var launcherCommand = LauncherMessages.ExecutableCommand(executablePath, commandLine.FirstOrDefault());
        try
        {
            var existing = await ExistingInstanceHandler.TryReuseAsync(
                    instanceLockPath,
                    healthUri,
                    apiUri,
                    _healthProbe,
                    _browserOpener,
                    noOpen,
                    StartupTimeout,
                    cancellationToken);
            if (existing.Existing)
            {
                Print(LauncherMessages.AlreadyRunning(
                    apiUri.ToString(),
                    launcherCommand,
                    existing.BrowserOpened,
                    _locale));
                return 0;
            }
        }
        catch (TimeoutException)
        {
            Print(LauncherMessages.ExistingUnhealthy(_locale));
            return 1;
        }

        using var child = StartDetachedChild(executablePath, hostArguments);
        if (child is null)
        {
            Print(LauncherMessages.StartFailed(_locale));
            return 1;
        }

        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < StartupTimeout && !cancellationToken.IsCancellationRequested)
        {
            var ownerProcessId = WebInstanceLock.ReadOwnerProcessId(instanceLockPath);
            var lockHeld = WebInstanceLock.IsHeld(instanceLockPath);
            var health = lockHeld
                ? await _healthProbe.ProbeAsync(healthUri, cancellationToken)
                : new WebHealthProbeResult(false, null);
            if (lockHeld && ownerProcessId.HasValue
                && health.IsHealthy && health.InstanceProcessId == ownerProcessId)
            {
                var opened = !noOpen && _browserOpener.TryOpen(apiUri);
                if (ownerProcessId.Value == child.Id)
                {
                    Print(LauncherMessages.Started(
                        apiUri.ToString(),
                        launcherCommand,
                        _locale));
                    return 0;
                }

                Print(LauncherMessages.AlreadyRunning(
                    apiUri.ToString(),
                    launcherCommand,
                    opened,
                    _locale));
                return 0;
            }

            if (child.HasExited)
            {
                // A competing launcher for the same data scope may have won the startup race.
                if (lockHeld && ownerProcessId.HasValue && ownerProcessId != child.Id
                    && health.IsHealthy && health.InstanceProcessId == ownerProcessId)
                {
                    var opened = !noOpen && _browserOpener.TryOpen(apiUri);
                    Print(LauncherMessages.AlreadyRunning(
                        apiUri.ToString(),
                        launcherCommand,
                        opened,
                        _locale));
                    return 0;
                }

                Print(LauncherMessages.StartFailed(_locale));
                return 1;
            }

            await Task.Delay(HealthPollInterval, cancellationToken);
        }

        try
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The child can exit between the check and Kill.
        }

        Print(LauncherMessages.StartFailed(_locale));
        return 1;
    }

    private static Process? StartDetachedChild(string executablePath, string[] hostArguments)
    {
        var setsid = LinuxXdgBrowserOpener.FindExecutable("setsid");
        var processPath = Environment.ProcessPath ?? executablePath;
        var commandLine = Environment.GetCommandLineArgs();
        var command = WebReplacementCommand.Create(
            setsid,
            processPath,
            WebReplacementCommand.GetManagedEntryPoint(processPath, commandLine),
            hostArguments);
        if (command is null) return null;

        try
        {
            return Process.Start(command.ToProcessStartInfo());
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void Print(string message) => Console.Out.WriteLine(message);
}

public static class BackgroundChildProcess
{
    public static void DetachStandardHandles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var nullDescriptor = Open("/dev/null", OpenReadWrite);
        if (nullDescriptor < 0)
        {
            return;
        }

        _ = Dup2(nullDescriptor, 0);
        _ = Dup2(nullDescriptor, 1);
        _ = Dup2(nullDescriptor, 2);
        if (nullDescriptor > 2)
        {
            _ = Close(nullDescriptor);
        }

        Console.SetIn(TextReader.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
    }

    private const int OpenReadWrite = 2;

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);

    [DllImport("libc", EntryPoint = "dup2", SetLastError = true)]
    private static extern int Dup2(int oldDescriptor, int newDescriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int descriptor);
}
