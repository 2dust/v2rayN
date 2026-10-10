using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceLib.Common;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Launcher;

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
    string ProgressPath,
    bool CoreWasRunning = false);

internal sealed record WebUpdateProgressState(
    string Phase,
    bool IsComplete,
    bool Success,
    string? Version,
    string? Detail,
    bool? RollbackSucceeded = null,
    bool CoreWasRunning = false);

internal static class NativeWebUpdateHelper
{
    private static readonly TimeSpan OwnerReleaseTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan FileReplaceRetryWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FileReplaceRetryDelay = TimeSpan.FromMilliseconds(250);
    private const string IsolatedHelperPrefix = ".v2rayn-WebAPI-update-helper-";

    public static async Task<int> RunAsync(string planPath)
    {
        var executable = Environment.ProcessPath;
        try
        {
            return await RunCoreAsync(planPath);
        }
        finally
        {
            ScheduleWindowsHelperCleanup(executable);
        }
    }

    private static async Task<int> RunCoreAsync(string planPath)
    {
        NativeWebUpdatePlan? plan = null;
        try
        {
            plan = JsonSerializer.Deserialize<NativeWebUpdatePlan>(await File.ReadAllTextAsync(planPath), JsonOptions);
            ValidatePlan(plan);
            if (OperatingSystem.IsLinux() && Path.GetFileName(Environment.ProcessPath) == "v2rayN.WebAPI")
            {
                return RunFromIsolatedBundle(planPath, Environment.ProcessPath!);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"Could not prepare the native Web update: {exception.Message}");
            var previousRestarted = false;
            if (plan is not null && CanRestartPreviousWithoutApplying(plan))
            {
                try
                {
                    WriteProgress(plan, new WebUpdateProgressState("rolling-back", false, false, plan.ExpectedVersion,
                        "The update could not be prepared; restarting the unchanged application."));
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
                        ? $"The update could not be prepared ({exception.Message}); the unchanged Web build is healthy."
                        : $"The update could not be prepared ({exception.Message}); the unchanged Web build could not be verified.",
                    previousRestarted));
            }
            TryDeleteFile(planPath);
            return previousRestarted ? 1 : 2;
        }

        Process? windowsOwnerProcess;
        try
        {
            windowsOwnerProcess = CaptureWindowsOwnerProcess(plan!);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not identify the previous Windows Web process: {exception.Message}");
            DeleteRuntimeIntent(plan!.RuntimeIntentPath);
            TryDeleteDirectory(plan.CandidateDirectory);
            WriteProgress(plan, new WebUpdateProgressState("failed", true, false, plan.ExpectedVersion,
                "The previous Windows Web process could not be identified; the installed application was not modified."));
            TryDeleteFile(planPath);
            return 1;
        }
        using var ownedWindowsProcess = windowsOwnerProcess;
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
        if (windowsOwnerProcess is not null
            && !await WaitForProcessExitAsync(windowsOwnerProcess, OwnerReleaseTimeout))
        {
            DeleteRuntimeIntent(plan.RuntimeIntentPath);
            TryDeleteDirectory(plan.CandidateDirectory);
            WriteProgress(plan, new WebUpdateProgressState("failed", true, false, plan.ExpectedVersion,
                "The previous Windows Web process did not exit after releasing its instance lock; the old application was not modified.", false));
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
                    ? "The new v2rayN.WebAPI build is healthy."
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

    private static int RunFromIsolatedBundle(string planPath, string executable)
    {
        var helperPath = CreateIsolatedHelperExecutable(executable);
        try
        {
            // The worker must keep an unchanged executable path for the entire
            // transaction: the single-file loader resolves assemblies lazily
            // from that path, even after the application executable is swapped.
            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                WorkingDirectory = Path.GetDirectoryName(helperPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--apply-WebAPI-update");
            startInfo.ArgumentList.Add(planPath);
            using var worker = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The isolated native Web update helper could not be started.");
            worker.WaitForExit();
            return worker.ExitCode;
        }
        finally
        {
            // The outer helper waits for exit before deleting the bundle. The
            // worker never deletes its own executable while it can still load it.
            TryDeleteFile(helperPath);
        }
    }

    internal static string CreateIsolatedHelperExecutable(string executable)
    {
        if (new FileInfo(executable).LinkTarget is not null)
            throw new InvalidDataException("The native update helper cannot be copied from a symlink.");
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var helperPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable))!,
            $"{IsolatedHelperPrefix}{Guid.NewGuid():N}{extension}");
        try
        {
            File.Copy(executable, helperPath, overwrite: false);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(helperPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return helperPath;
        }
        catch
        {
            TryDeleteFile(helperPath);
            throw;
        }
    }

    internal static bool IsUpdateHelperExecutableName(string name)
    {
        if (name is "v2rayN.WebAPI" or "v2rayN.WebAPI.exe") return true;
        return IsIsolatedHelperExecutableName(name);
    }

    private static bool IsIsolatedHelperExecutableName(string name)
    {
        var helperName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        return helperName.StartsWith(IsolatedHelperPrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(helperName[IsolatedHelperPrefix.Length..], "N", out _);
    }

    internal static void ScheduleWindowsHelperCleanup(string? executable)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executable)
            || !Path.GetFileName(executable).StartsWith(IsolatedHelperPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            var escapedPath = Path.GetFullPath(executable).Replace("'", "''", StringComparison.Ordinal);
            var script = $"$targetPid={Environment.ProcessId};$targetPath='{escapedPath}';"
                + "for($i=0;$i -lt 600;$i++){if(-not(Get-Process -Id $targetPid -ErrorAction SilentlyContinue)){break};Start-Sleep -Milliseconds 100};"
                + "Remove-Item -LiteralPath $targetPath -Force -ErrorAction SilentlyContinue";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var startInfo = new ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-EncodedCommand");
            startInfo.ArgumentList.Add(encoded);
            Process.Start(startInfo)?.Dispose();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"Could not schedule cleanup of the Windows update helper: {exception.Message}");
            _ = MoveFileEx(Path.GetFullPath(executable), null, MoveFileDelayUntilReboot);
        }
    }

    private const int MoveFileDelayUntilReboot = 0x00000004;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);

    private static void ValidatePlan(NativeWebUpdatePlan? plan)
    {
        var isLinux = OperatingSystem.IsLinux();
        var isWindows = OperatingSystem.IsWindows();
        var isManagedLinuxService = isLinux && LauncherEnvironment.IsSystemdManagedDeployment();
        if ((!isLinux && !isWindows) || plan is null
            || File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("container"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
            || isManagedLinuxService
            || !WebUpdatePackageStager.IsValidVersion(plan.ExpectedVersion)
            || !WebUpdatePackageStager.IsValidVersion(plan.PreviousVersion)
            || !WebUpdatePackageStager.IsSupportedRid(plan.Rid)
            || plan.Rid != System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier
            || string.IsNullOrWhiteSpace(plan.ExpectedCommit)
            || !WebProbeUriResolver.IsProbeableLoopbackUri(plan.HealthUri))
        {
            throw new InvalidDataException("The update plan has an unsupported target or identity.");
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidDataException("The update helper process path is unavailable.");
        var install = Path.GetFullPath(plan.InstallDirectory);
        var executableName = WebUpdatePackageStager.ExecutableNameForRid(plan.Rid);
        var processFileName = Path.GetFileName(executable);
        var pathComparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(Path.GetDirectoryName(executable) ?? string.Empty), install, pathComparison)
            || !IsUpdateHelperExecutableName(processFileName)
            || (isWindows && !IsIsolatedHelperExecutableName(processFileName))
            || new FileInfo(executable).LinkTarget is not null)
        {
            throw new InvalidDataException("The update plan does not target this native v2rayN.WebAPI installation.");
        }
        var parent = Path.GetDirectoryName(install)
            ?? throw new InvalidDataException("The installation directory has no parent.");
        EnsureSiblingDirectory(plan.CandidateDirectory, parent, ".v2rayn-WebAPI-candidate-");
        EnsureSiblingDirectory(plan.BackupDirectory, parent, ".v2rayn-WebAPI-backup-");
        if (!Path.IsPathFullyQualified(plan.InstanceLockPath)
            || !Path.IsPathFullyQualified(plan.RuntimeIntentPath)
            || !Path.IsPathFullyQualified(plan.ProgressPath)
            || !string.Equals(Path.GetFullPath(plan.InstanceLockPath),
                Path.GetFullPath(Path.Combine(Utils.StartupPath(), "v2rayN.WebAPI.instance.lock")), pathComparison)
            || !string.Equals(Path.GetFullPath(plan.RuntimeIntentPath),
                Path.GetFullPath(V2rayRuntime.WebUpdateRuntimeStatePath), pathComparison)
            || !string.Equals(Path.GetFullPath(plan.ProgressPath),
                Path.GetFullPath(Utils.GetTempPath("WebAPI-update-progress.json")), pathComparison)
            || !Directory.Exists(plan.CandidateDirectory)
            || !File.Exists(Path.Combine(plan.CandidateDirectory, executableName))
            || !File.Exists(Path.Combine(plan.CandidateDirectory, "v2rayN.WebAPI.build.json")))
        {
            throw new InvalidDataException("The update plan paths or staged application files are invalid.");
        }
        EnsureNoLinks(plan.CandidateDirectory);
        WebUpdatePackageStager.RequireNativeExecutable(Path.Combine(plan.CandidateDirectory, executableName), plan.Rid);
        var identity = JsonSerializer.Deserialize<WebUpdatePackageIdentity>(
            File.ReadAllText(Path.Combine(plan.CandidateDirectory, "v2rayN.WebAPI.build.json")), JsonOptions);
        if (identity is null || identity.Product != "v2rayN.WebAPI"
            || identity.Version != plan.ExpectedVersion || identity.Commit != plan.ExpectedCommit
            || identity.Rid != plan.Rid)
        {
            throw new InvalidDataException("The staged executable identity does not match the verified update plan.");
        }
        var writableProbe = Path.Combine(install, $".v2rayn-WebAPI-helper-write-probe-{Guid.NewGuid():N}");
        using (new FileStream(writableProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(writableProbe);
        if (new DirectoryInfo(install).LinkTarget is not null
            || new FileInfo(Path.Combine(install, executableName)).LinkTarget is not null)
        {
            throw new InvalidDataException("Self-update is disabled for symlink-managed installations.");
        }
    }

    private static bool CanRestartPreviousWithoutApplying(NativeWebUpdatePlan plan)
    {
        try
        {
            var executable = Environment.ProcessPath;
            var isLinux = OperatingSystem.IsLinux();
            var isWindows = OperatingSystem.IsWindows();
            var pathComparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return (isLinux || isWindows)
                && (!isLinux || !LauncherEnvironment.IsSystemdManagedDeployment())
                && !File.Exists("/.dockerenv")
                && !File.Exists("/run/.containerenv")
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("container"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
                && !string.IsNullOrWhiteSpace(executable)
                && IsUpdateHelperExecutableName(Path.GetFileName(executable))
                && new FileInfo(executable).LinkTarget is null
                && string.Equals(Path.GetFullPath(Path.GetDirectoryName(executable) ?? string.Empty),
                    Path.GetFullPath(plan.InstallDirectory), pathComparison)
                && Path.IsPathFullyQualified(plan.InstanceLockPath)
                && Path.IsPathFullyQualified(plan.ProgressPath)
                && Path.IsPathFullyQualified(plan.RuntimeIntentPath)
                && string.Equals(Path.GetFullPath(plan.InstanceLockPath),
                    Path.GetFullPath(Path.Combine(Utils.StartupPath(), "v2rayN.WebAPI.instance.lock")), pathComparison)
                && string.Equals(Path.GetFullPath(plan.RuntimeIntentPath),
                    Path.GetFullPath(V2rayRuntime.WebUpdateRuntimeStatePath), pathComparison)
                && string.Equals(Path.GetFullPath(plan.ProgressPath),
                    Path.GetFullPath(Utils.GetTempPath("WebAPI-update-progress.json")), pathComparison)
                && WebProbeUriResolver.IsProbeableLoopbackUri(plan.HealthUri)
                && WebUpdatePackageStager.IsValidVersion(plan.PreviousVersion)
                && plan.HostArguments is not null
                && !plan.HostArguments.Any(argument => argument is "--stop" or "--apply-WebAPI-update" or "--background" or "--background-child");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void EnsureSiblingDirectory(string value, string parent, string prefix)
    {
        var full = Path.GetFullPath(value);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(full), parent, comparison)
            || !Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A staged update directory is outside the installation parent.");
        }
    }

    // Windows can transiently deny replacing a freshly written executable while
    // antivirus or the search indexer holds it open. Retry for a bounded window so
    // a transient lock cannot fail the update or its rollback; permanent failures
    // surface with the operation and cause instead of a bare access-denied code.
    internal static void WithTransientFileRetry(
        Action operation,
        string description,
        TimeSpan? retryWindow = null,
        TimeSpan? retryDelay = null)
    {
        var window = retryWindow ?? FileReplaceRetryWindow;
        var delay = retryDelay ?? FileReplaceRetryDelay;
        var deadline = DateTime.UtcNow + window;
        while (true)
        {
            try
            {
                operation();
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new IOException(
                        $"{description} failed after retrying for {window.TotalSeconds:0.#}s: {exception.Message}",
                        exception);
                }

                Thread.Sleep(delay);
            }
        }
    }

    internal static void CopyCurrentAppToBackup(NativeWebUpdatePlan plan)
    {
        var executableName = WebUpdatePackageStager.ExecutableNameForRid(plan.Rid);
        Directory.CreateDirectory(plan.BackupDirectory);
        if (new FileInfo(Path.Combine(plan.InstallDirectory, executableName)).LinkTarget is not null)
            throw new InvalidDataException("The installed Web executable is a symlink and cannot be transactionally replaced.");
        File.Copy(Path.Combine(plan.InstallDirectory, executableName), Path.Combine(plan.BackupDirectory, executableName));
        var oldIdentity = Path.Combine(plan.InstallDirectory, "v2rayN.WebAPI.build.json");
        if (File.Exists(oldIdentity))
        {
            if (new FileInfo(oldIdentity).LinkTarget is not null)
                throw new InvalidDataException("The installed Web build identity is a symlink.");
            File.Copy(oldIdentity, Path.Combine(plan.BackupDirectory, "v2rayN.WebAPI.build.json"));
        }
    }

    internal static void SwapCandidateAppIntoPlace(NativeWebUpdatePlan plan)
    {
        var install = plan.InstallDirectory;
        var candidate = plan.CandidateDirectory;
        var executableName = WebUpdatePackageStager.ExecutableNameForRid(plan.Rid);
        var installedExecutable = Path.Combine(install, executableName);
        var installedIdentity = Path.Combine(install, "v2rayN.WebAPI.build.json");

        WithTransientFileRetry(
            () => File.Move(Path.Combine(candidate, executableName), installedExecutable, overwrite: true),
            $"Replacing the installed Web executable '{installedExecutable}'");
        WithTransientFileRetry(
            () => File.Move(Path.Combine(candidate, "v2rayN.WebAPI.build.json"), installedIdentity, overwrite: true),
            $"Replacing the installed Web build identity '{installedIdentity}'");
        SetExecutableMode(installedExecutable);
    }

    internal static void RestorePreviousApp(NativeWebUpdatePlan plan)
    {
        var install = plan.InstallDirectory;
        var backup = plan.BackupDirectory;
        var executableName = WebUpdatePackageStager.ExecutableNameForRid(plan.Rid);
        if (!Directory.Exists(backup)) throw new DirectoryNotFoundException("The previous Web application backup is missing.");

        var previousIdentity = Path.Combine(backup, "v2rayN.WebAPI.build.json");
        var restoredExecutable = Path.Combine(install, $".v2rayn-WebAPI-restore-{Guid.NewGuid():N}{Path.GetExtension(executableName)}");
        var restoredIdentity = restoredExecutable + ".build.json";
        var installedExecutable = Path.Combine(install, executableName);
        var installedIdentity = Path.Combine(install, "v2rayN.WebAPI.build.json");
        try
        {
            // Never truncate an executable inode: the single-file helper may still
            // have bundled assemblies mapped from it. Restore by atomic rename,
            // just like the forward swap, so existing mappings remain unchanged.
            WithTransientFileRetry(
                () => File.Copy(Path.Combine(backup, executableName), restoredExecutable),
                $"Staging the previous Web executable '{restoredExecutable}'");
            if (File.Exists(previousIdentity))
            {
                WithTransientFileRetry(
                    () => File.Copy(previousIdentity, restoredIdentity),
                    $"Staging the previous Web build identity '{restoredIdentity}'");
            }

            SetExecutableMode(restoredExecutable);
            WithTransientFileRetry(
                () => File.Move(restoredExecutable, installedExecutable, overwrite: true),
                $"Restoring the previous Web executable '{installedExecutable}'");
            if (File.Exists(restoredIdentity))
            {
                WithTransientFileRetry(
                    () => File.Move(restoredIdentity, installedIdentity, overwrite: true),
                    $"Restoring the previous Web build identity '{installedIdentity}'");
            }
            else
            {
                TryDeleteFile(installedIdentity);
            }
        }
        finally
        {
            TryDeleteFile(restoredExecutable);
            TryDeleteFile(restoredIdentity);
        }
    }

    private static Process StartWebInstance(NativeWebUpdatePlan plan)
    {
        var executable = Path.Combine(plan.InstallDirectory, WebUpdatePackageStager.ExecutableNameForRid(plan.Rid));
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsLinux())
        {
            var setsid = LinuxXdgBrowserOpener.FindExecutable("setsid")
                ?? throw new InvalidOperationException("The setsid executable is required for a detached native Web restart.");
            startInfo = new ProcessStartInfo
            {
                FileName = setsid,
                WorkingDirectory = plan.InstallDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(executable);
            startInfo.ArgumentList.Add(WebLaunchOptions.BackgroundChildFlag);
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = plan.InstallDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
        }
        else
        {
            throw new PlatformNotSupportedException("Native Web self-update is supported on Linux and Windows x64.");
        }
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
        int? lastOwnerProcessId = null;
        var lastHealth = new WebHealthProbeResult(false, null);
        var lastRuntimeIntentSatisfied = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (WebInstanceLock.IsHeld(plan.InstanceLockPath))
            {
                lastOwnerProcessId = WebInstanceLock.ReadOwnerProcessId(plan.InstanceLockPath);
                lastHealth = await healthProbe.ProbeAsync(healthUri, CancellationToken.None);
                lastRuntimeIntentSatisfied = RuntimeIntentIsSatisfied(plan.RuntimeIntentPath, lastHealth);
                if (lastOwnerProcessId.HasValue && lastHealth.IsHealthy && lastHealth.InstanceProcessId == lastOwnerProcessId
                    && string.Equals(lastHealth.WebVersion, expectedVersion, StringComparison.Ordinal)
                    && lastRuntimeIntentSatisfied)
                {
                    return true;
                }
            }
            if (process.HasExited && !WebInstanceLock.IsHeld(plan.InstanceLockPath))
            {
                WriteHealthMismatchDiagnostic(plan, expectedVersion, lastOwnerProcessId, lastHealth, lastRuntimeIntentSatisfied);
                return false;
            }
            await Task.Delay(PollInterval);
        }
        WriteHealthMismatchDiagnostic(plan, expectedVersion, lastOwnerProcessId, lastHealth, lastRuntimeIntentSatisfied);
        return false;
    }

    private static void WriteHealthMismatchDiagnostic(
        NativeWebUpdatePlan plan,
        string expectedVersion,
        int? ownerProcessId,
        WebHealthProbeResult health,
        bool runtimeIntentSatisfied) =>
        Console.Error.WriteLine(
            $"Native Web update health mismatch: expectedVersion={expectedVersion}; lockOwner={ownerProcessId}; "
            + $"healthy={health.IsHealthy}; healthPid={health.InstanceProcessId}; observedVersion={health.WebVersion}; "
            + $"coreState={health.CoreState}; corePids={string.Join(',', health.CoreProcessIds ?? [])}; "
            + $"runtimeIntentSatisfied={runtimeIntentSatisfied}; progress={plan.ProgressPath}.");

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

    private static Process? CaptureWindowsOwnerProcess(NativeWebUpdatePlan plan)
    {
        if (!OperatingSystem.IsWindows() || !WebInstanceLock.IsHeld(plan.InstanceLockPath)) return null;
        var ownerProcessId = WebInstanceLock.ReadOwnerProcessId(plan.InstanceLockPath)
            ?? throw new InvalidDataException("The running Windows Web process identity could not be read from its instance lock.");
        try
        {
            return Process.GetProcessById(ownerProcessId);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The running Windows Web process no longer matches its instance lock.", exception);
        }
    }

    private static async Task<bool> WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if (process.HasExited) return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            await Task.Delay(PollInterval);
        }
        try { return process.HasExited; }
        catch (InvalidOperationException) { return true; }
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
            File.WriteAllText(temporary, JsonSerializer.Serialize(state with { CoreWasRunning = plan.CoreWasRunning }, JsonOptions));
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
