using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ServiceLib.Common;
using v2rayN.Web.Services;

namespace v2rayN.Web.Launcher;

internal sealed record NativeWebUpdatePlan(
    string InstallDirectory,
    string CandidateDirectory,
    string BackupDirectory,
    string InstanceLockPath,
    string HealthUri,
    string[] HostArguments,
    string ExpectedVersion,
    string ExpectedCommit,
    string Rid,
    string PreviousVersion,
    string RuntimeIntentPath,
    string ProgressPath);

internal sealed record WebUpdateProgressState(
    string Phase,
    bool IsComplete,
    bool Success,
    string? Version,
    string? Detail,
    bool? RollbackSucceeded = null);

internal static class NativeWebUpdateHelper
{
    private static readonly TimeSpan OwnerReleaseTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    public static async Task<int> RunAsync(string planPath)
    {
        NativeWebUpdatePlan? plan = null;
        try
        {
            plan = JsonSerializer.Deserialize<NativeWebUpdatePlan>(await File.ReadAllTextAsync(planPath), JsonOptions);
            ValidatePlan(plan);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Invalid native Web update plan: {exception.Message}");
            var previousRestarted = false;
            if (plan is not null && CanRestartPreviousWithoutApplying(plan))
            {
                try
                {
                    WriteProgress(plan, new WebUpdateProgressState("rolling-back", false, false, plan.ExpectedVersion,
                        "The staged update plan was rejected; restarting the unchanged application."));
                    using var oldProcess = StartWebInstance(plan);
                    previousRestarted = await WaitForExpectedHealthAsync(plan, oldProcess, plan.PreviousVersion, HealthTimeout);
                    if (!previousRestarted) StopOwnedProcess(oldProcess, plan.InstanceLockPath);
                    if (previousRestarted) DeleteRuntimeIntent(plan.RuntimeIntentPath);
                }
                catch (Exception restartException)
                {
                    Console.Error.WriteLine($"Could not restart the unchanged Web application: {restartException.Message}");
                }
                WriteProgress(plan, new WebUpdateProgressState("failed", true, false, plan.ExpectedVersion,
                    previousRestarted
                        ? $"The update plan was rejected ({exception.Message}); the unchanged Web build is healthy."
                        : $"The update plan was rejected ({exception.Message}); the unchanged Web build could not be verified.",
                    previousRestarted));
            }
            TryDeleteFile(planPath);
            return previousRestarted ? 1 : 2;
        }

        WriteProgress(plan!, new WebUpdateProgressState("waiting", false, false, plan!.ExpectedVersion,
            "Waiting for the previous Web instance to stop gracefully."));
        if (!await WaitForLockReleaseAsync(plan.InstanceLockPath, OwnerReleaseTimeout))
        {
            DeleteRuntimeIntent(plan.RuntimeIntentPath);
            TryDeleteDirectory(plan.CandidateDirectory);
            WriteProgress(plan, new WebUpdateProgressState("failed", true, false, plan.ExpectedVersion,
                "The previous Web instance did not release its instance lock; the old application was not modified.", false));
            TryDeleteFile(planPath);
            return 1;
        }

        var backupCreated = false;
        Process? replacementProcess = null;
        try
        {
            WriteProgress(plan, new WebUpdateProgressState("installing", false, false, plan.ExpectedVersion, null));
            CopyCurrentAppToBackup(plan);
            backupCreated = true;
            var transaction = await NativeWebUpdateWorkflow.ApplyAsync(
                () =>
                {
                    SwapCandidateAppIntoPlace(plan);
                    return Task.CompletedTask;
                },
                async () =>
                {
                    replacementProcess = StartWebInstance(plan);
                    var healthy = await WaitForExpectedHealthAsync(plan, replacementProcess, plan.ExpectedVersion, HealthTimeout);
                    if (!healthy) StopOwnedProcess(replacementProcess, plan.InstanceLockPath);
                    return healthy;
                },
                () =>
                {
                    StopOwnedProcess(replacementProcess, plan.InstanceLockPath);
                    RestorePreviousApp(plan);
                    return Task.CompletedTask;
                },
                async () =>
                {
                    replacementProcess = StartWebInstance(plan);
                    var healthy = await WaitForExpectedHealthAsync(plan, replacementProcess, plan.PreviousVersion, HealthTimeout);
                    if (!healthy) StopOwnedProcess(replacementProcess, plan.InstanceLockPath);
                    return healthy;
                },
                phase => WriteProgress(plan, new WebUpdateProgressState(phase, false, false, plan.ExpectedVersion, null)),
                () =>
                {
                    TryDeleteDirectory(plan.BackupDirectory);
                    TryDeleteDirectory(plan.CandidateDirectory);
                    return Task.CompletedTask;
                });

            if (transaction.Success || transaction.RollbackSucceeded)
                DeleteRuntimeIntent(plan.RuntimeIntentPath);
            WriteProgress(plan, new WebUpdateProgressState(
                transaction.Success ? "completed" : "failed",
                true,
                transaction.Success,
                plan.ExpectedVersion,
                transaction.Success
                    ? "The new v2rayN Web build is healthy."
                    : transaction.RollbackSucceeded
                        ? transaction.Detail
                        : $"{transaction.Detail} Previous application backup retained at {plan.BackupDirectory}.",
                transaction.RollbackSucceeded));
            return transaction.Success ? 0 : transaction.RollbackSucceeded ? 1 : 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Native Web update failed: {exception}");
            StopOwnedProcess(replacementProcess, plan.InstanceLockPath);
            var rollbackSucceeded = false;
            if (backupCreated)
            {
                try
                {
                    RestorePreviousApp(plan);
                    var restoredProcess = StartWebInstance(plan);
                    rollbackSucceeded = await WaitForExpectedHealthAsync(plan, restoredProcess, plan.PreviousVersion, HealthTimeout);
                    if (rollbackSucceeded)
                    {
                        DeleteRuntimeIntent(plan.RuntimeIntentPath);
                        TryDeleteDirectory(plan.BackupDirectory);
                    }
                }
                catch (Exception rollbackException)
                {
                    Console.Error.WriteLine($"Native Web update rollback failed: {rollbackException}");
                }
            }
            else
            {
                try
                {
                    var restoredProcess = StartWebInstance(plan);
                    rollbackSucceeded = await WaitForExpectedHealthAsync(plan, restoredProcess, plan.PreviousVersion, HealthTimeout);
                    if (rollbackSucceeded) DeleteRuntimeIntent(plan.RuntimeIntentPath);
                }
                catch (Exception restartException)
                {
                    Console.Error.WriteLine($"Could not restart the unchanged Web application: {restartException}");
                }
                TryDeleteDirectory(plan.BackupDirectory);
                TryDeleteDirectory(plan.CandidateDirectory);
            }
            WriteProgress(plan, new WebUpdateProgressState("failed", true, false, plan.ExpectedVersion,
                rollbackSucceeded
                    ? $"{exception.Message} The previous Web build was restored and is healthy."
                    : $"{exception.Message} The previous Web build could not be confirmed healthy; backup: {plan.BackupDirectory}.",
                rollbackSucceeded));
            return rollbackSucceeded ? 1 : 2;
        }
        finally
        {
            replacementProcess?.Dispose();
            TryDeleteFile(planPath);
        }
    }

    private static void ValidatePlan(NativeWebUpdatePlan? plan)
    {
        if (!OperatingSystem.IsLinux() || plan is null
            || File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")
            || WebStopper.IsManagedBySystemd(Environment.ProcessId)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVOCATION_ID"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JOURNAL_STREAM"))
            || !WebUpdatePackageStager.IsValidVersion(plan.ExpectedVersion)
            || !WebUpdatePackageStager.IsValidVersion(plan.PreviousVersion)
            || plan.Rid is not ("linux-x64" or "linux-arm64")
            || plan.Rid != System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier
            || string.IsNullOrWhiteSpace(plan.ExpectedCommit)
            || !Uri.TryCreate(plan.HealthUri, UriKind.Absolute, out var healthUri)
            || healthUri.Scheme != Uri.UriSchemeHttp
            || healthUri.Host is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidDataException("The update plan has an unsupported target or identity.");
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidDataException("The update helper process path is unavailable.");
        var install = Path.GetFullPath(plan.InstallDirectory);
        if (Path.GetFullPath(Path.GetDirectoryName(executable) ?? string.Empty) != install
            || !Path.GetFileName(executable).Equals("v2rayN.Web", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The update plan does not target this native v2rayN Web installation.");
        }
        var parent = Path.GetDirectoryName(install)
            ?? throw new InvalidDataException("The installation directory has no parent.");
        EnsureSiblingDirectory(plan.CandidateDirectory, parent, ".v2rayn-web-candidate-");
        EnsureSiblingDirectory(plan.BackupDirectory, parent, ".v2rayn-web-backup-");
        if (!Path.IsPathFullyQualified(plan.InstanceLockPath)
            || !Path.IsPathFullyQualified(plan.RuntimeIntentPath)
            || !Path.IsPathFullyQualified(plan.ProgressPath)
            || Path.GetFullPath(plan.InstanceLockPath) != Path.GetFullPath(Path.Combine(Utils.StartupPath(), "v2rayN.Web.instance.lock"))
            || Path.GetFullPath(plan.RuntimeIntentPath) != Path.GetFullPath(V2rayRuntime.WebUpdateRuntimeStatePath)
            || Path.GetFullPath(plan.ProgressPath) != Path.GetFullPath(Utils.GetTempPath("web-update-progress.json"))
            || !Directory.Exists(plan.CandidateDirectory)
            || !File.Exists(Path.Combine(plan.CandidateDirectory, "v2rayN.Web"))
            || !File.Exists(Path.Combine(plan.CandidateDirectory, "v2rayN.Web.build.json")))
        {
            throw new InvalidDataException("The update plan paths or staged application files are invalid.");
        }
        EnsureNoLinks(plan.CandidateDirectory);
        var identity = JsonSerializer.Deserialize<WebUpdatePackageIdentity>(
            File.ReadAllText(Path.Combine(plan.CandidateDirectory, "v2rayN.Web.build.json")), JsonOptions);
        if (identity is null || identity.Product != "v2rayN.Web"
            || identity.Version != plan.ExpectedVersion || identity.Commit != plan.ExpectedCommit
            || identity.Rid != plan.Rid)
        {
            throw new InvalidDataException("The staged executable identity does not match the verified update plan.");
        }
        var writableProbe = Path.Combine(install, $".v2rayn-web-helper-write-probe-{Guid.NewGuid():N}");
        using (new FileStream(writableProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(writableProbe);
        if (new DirectoryInfo(install).LinkTarget is not null
            || new FileInfo(Path.Combine(install, "v2rayN.Web")).LinkTarget is not null)
        {
            throw new InvalidDataException("Self-update is disabled for symlink-managed installations.");
        }
    }

    private static bool CanRestartPreviousWithoutApplying(NativeWebUpdatePlan plan)
    {
        try
        {
            var executable = Environment.ProcessPath;
            return OperatingSystem.IsLinux()
                && !WebStopper.IsManagedBySystemd(Environment.ProcessId)
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVOCATION_ID"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JOURNAL_STREAM"))
                && !File.Exists("/.dockerenv")
                && !File.Exists("/run/.containerenv")
                && !string.IsNullOrWhiteSpace(executable)
                && Path.GetFileName(executable).Equals("v2rayN.Web", StringComparison.Ordinal)
                && Path.GetFullPath(Path.GetDirectoryName(executable) ?? string.Empty) == Path.GetFullPath(plan.InstallDirectory)
                && Path.IsPathFullyQualified(plan.InstanceLockPath)
                && Path.IsPathFullyQualified(plan.ProgressPath)
                && Path.IsPathFullyQualified(plan.RuntimeIntentPath)
                && Path.GetFullPath(plan.InstanceLockPath) == Path.GetFullPath(Path.Combine(Utils.StartupPath(), "v2rayN.Web.instance.lock"))
                && Path.GetFullPath(plan.RuntimeIntentPath) == Path.GetFullPath(V2rayRuntime.WebUpdateRuntimeStatePath)
                && Path.GetFullPath(plan.ProgressPath) == Path.GetFullPath(Utils.GetTempPath("web-update-progress.json"))
                && Uri.TryCreate(plan.HealthUri, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttp && uri.Host is "127.0.0.1" or "localhost"
                && WebUpdatePackageStager.IsValidVersion(plan.PreviousVersion)
                && plan.HostArguments is not null
                && !plan.HostArguments.Any(argument => argument is "--stop" or "--apply-web-update" or "--background" or "--background-child");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void EnsureSiblingDirectory(string value, string parent, string prefix)
    {
        var full = Path.GetFullPath(value);
        if (Path.GetDirectoryName(full) != parent
            || !Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A staged update directory is outside the installation parent.");
        }
    }

    internal static void CopyCurrentAppToBackup(NativeWebUpdatePlan plan)
    {
        Directory.CreateDirectory(plan.BackupDirectory);
        if (new FileInfo(Path.Combine(plan.InstallDirectory, "v2rayN.Web")).LinkTarget is not null)
            throw new InvalidDataException("The installed Web executable is a symlink and cannot be transactionally replaced.");
        File.Copy(Path.Combine(plan.InstallDirectory, "v2rayN.Web"), Path.Combine(plan.BackupDirectory, "v2rayN.Web"));
        var oldIdentity = Path.Combine(plan.InstallDirectory, "v2rayN.Web.build.json");
        if (File.Exists(oldIdentity))
        {
            if (new FileInfo(oldIdentity).LinkTarget is not null)
                throw new InvalidDataException("The installed Web build identity is a symlink.");
            File.Copy(oldIdentity, Path.Combine(plan.BackupDirectory, "v2rayN.Web.build.json"));
        }
    }

    internal static void SwapCandidateAppIntoPlace(NativeWebUpdatePlan plan)
    {
        var install = plan.InstallDirectory;
        var backup = plan.BackupDirectory;
        var candidate = plan.CandidateDirectory;

        File.Move(Path.Combine(candidate, "v2rayN.Web"), Path.Combine(install, "v2rayN.Web"), overwrite: true);
        File.Move(Path.Combine(candidate, "v2rayN.Web.build.json"), Path.Combine(install, "v2rayN.Web.build.json"), overwrite: true);
        SetExecutableMode(Path.Combine(install, "v2rayN.Web"));
    }

    internal static void RestorePreviousApp(NativeWebUpdatePlan plan)
    {
        var install = plan.InstallDirectory;
        var backup = plan.BackupDirectory;
        if (!Directory.Exists(backup)) throw new DirectoryNotFoundException("The previous Web application backup is missing.");

        File.Copy(Path.Combine(backup, "v2rayN.Web"), Path.Combine(install, "v2rayN.Web"), overwrite: true);
        var previousIdentity = Path.Combine(backup, "v2rayN.Web.build.json");
        if (File.Exists(previousIdentity))
            File.Copy(previousIdentity, Path.Combine(install, "v2rayN.Web.build.json"), overwrite: true);
        else
            TryDeleteFile(Path.Combine(install, "v2rayN.Web.build.json"));
        SetExecutableMode(Path.Combine(install, "v2rayN.Web"));
    }

    private static Process StartWebInstance(NativeWebUpdatePlan plan)
    {
        var executable = Path.Combine(plan.InstallDirectory, "v2rayN.Web");
        var setsid = LinuxXdgBrowserOpener.FindExecutable("setsid")
            ?? throw new InvalidOperationException("The setsid executable is required for a detached native Web restart.");
        var startInfo = new ProcessStartInfo
        {
            FileName = setsid,
            WorkingDirectory = plan.InstallDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(executable);
        startInfo.ArgumentList.Add(WebLaunchOptions.BackgroundChildFlag);
        foreach (var argument in plan.HostArguments) startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the updated Web application.");
    }

    private static async Task<bool> WaitForExpectedHealthAsync(
        NativeWebUpdatePlan plan,
        Process process,
        string expectedVersion,
        TimeSpan timeout)
    {
        var healthProbe = new HttpWebHealthProbe();
        var healthUri = new Uri(plan.HealthUri);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (WebInstanceLock.IsHeld(plan.InstanceLockPath))
            {
                var owner = WebInstanceLock.ReadOwnerProcessId(plan.InstanceLockPath);
                var health = await healthProbe.ProbeAsync(healthUri, CancellationToken.None);
                if (owner.HasValue && health.IsHealthy && health.InstanceProcessId == owner
                    && string.Equals(health.WebVersion, expectedVersion, StringComparison.Ordinal)
                    && RuntimeIntentIsSatisfied(plan.RuntimeIntentPath, health))
                {
                    return true;
                }
            }
            if (process.HasExited && !WebInstanceLock.IsHeld(plan.InstanceLockPath)) return false;
            await Task.Delay(PollInterval);
        }
        return false;
    }

    private static bool RuntimeIntentIsSatisfied(string intentPath, WebHealthProbeResult health)
    {
        try
        {
            if (!File.Exists(intentPath)) return false;
            var intent = JsonSerializer.Deserialize<RuntimeRestartIntent>(File.ReadAllText(intentPath), JsonOptions);
            return intent is not null && NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(intent, health);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForLockReleaseAsync(string lockPath, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!WebInstanceLock.IsHeld(lockPath)) return true;
            await Task.Delay(PollInterval);
        }
        return !WebInstanceLock.IsHeld(lockPath);
    }

    private static void StopOwnedProcess(Process? process, string lockPath)
    {
        if (process is null) return;
        try
        {
            if (process.HasExited) return;
            if (WebInstanceLock.ReadOwnerProcessId(lockPath) == process.Id)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The managed update child may exit during health verification.
        }
    }

    private static void SetExecutableMode(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static void EnsureNoLinks(string directory)
    {
        if (new DirectoryInfo(directory).LinkTarget is not null)
            throw new InvalidDataException("The staged update directory is a symlink.");
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (new FileInfo(file).LinkTarget is not null)
                throw new InvalidDataException("The staged Web update contains a symbolic link.");
        }
        foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (new DirectoryInfo(childDirectory).LinkTarget is not null)
                throw new InvalidDataException("The staged Web update contains a symbolic link.");
            EnsureNoLinks(childDirectory);
        }
    }

    private static void WriteProgress(NativeWebUpdatePlan plan, WebUpdateProgressState state)
    {
        try
        {
            var temporary = plan.ProgressPath + $".{Environment.ProcessId}.tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporary, plan.ProgressPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not persist native Web update progress: {exception.Message}");
        }
    }

    private static void DeleteRuntimeIntent(string path) => TryDeleteFile(path);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not clean a Web update directory {path}: {exception.Message}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not clean a Web update file {path}: {exception.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
