using Microsoft.Extensions.Configuration;
using v2rayN.WebAPI.Contracts;

namespace v2rayN.WebAPI.Launcher;

public sealed record NativeWebRestoreReplacement(
    Uri HealthUri,
    WebReplacementCommand Command,
    bool CoreWasRunning);

public sealed record NativeWebRestorePreflightResult(
    bool Allowed,
    WebLifecycleOwner Owner,
    NativeWebRestoreReplacement? Replacement,
    string? FailureCode = null,
    string? MessageKey = null)
{
    public OperationView ToFailureOperation()
    {
        if (Allowed || string.IsNullOrWhiteSpace(FailureCode) || string.IsNullOrWhiteSpace(MessageKey))
        {
            throw new InvalidOperationException("A successful native restore preflight has no failure response.");
        }

        return OperationView.Fail(FailureCode, MessageKey);
    }
}

/// <summary>
/// Resolves everything needed to safely restart a native restore before Core or config state
/// is touched. Supervised deployments deliberately leave restart ownership to their supervisor.
/// </summary>
public static class NativeWebRestorePreflight
{
    public static NativeWebRestorePreflightResult Evaluate(
        IConfiguration configuration,
        bool isLinux,
        bool isWindows,
        bool externalSupervisor,
        bool containerEnvironment,
        string? setsidPath,
        string? processPath,
        string? managedEntryPoint,
        IEnumerable<string> hostArguments,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? isUnixExecutable = null,
        bool coreWasRunning = false)
    {
        var lifecycle = WebLifecyclePlanner.Create(
            WebLifecycleAction.RestoreAndRestart,
            supportsNativeRestart: isLinux || isWindows,
            daemonEnvironment: externalSupervisor,
            containerEnvironment);
        if (lifecycle.Owner != WebLifecycleOwner.NativeProcess)
        {
            return new(true, lifecycle.Owner, null);
        }

        if (!WebProbeUriResolver.TryResolve(configuration, out var endpoints))
        {
            return Refused(
                WebLifecycleOwner.NativeProcess,
                "backup_restore_health_probe_unavailable",
                ApiMessageKeys.BackupRestoreProbeUnavailable);
        }

        var exists = fileExists ?? File.Exists;
        if (string.IsNullOrWhiteSpace(processPath) || !exists(processPath))
        {
            return Refused(
                WebLifecycleOwner.NativeProcess,
                "backup_restore_executable_unavailable",
                ApiMessageKeys.BackupRestoreExecutableUnavailable);
        }

        var isDotnet = Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (isDotnet
            && (string.IsNullOrWhiteSpace(managedEntryPoint)
                || !managedEntryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || !exists(managedEntryPoint)))
        {
            return Refused(
                WebLifecycleOwner.NativeProcess,
                "backup_restore_executable_unavailable",
                ApiMessageKeys.BackupRestoreExecutableUnavailable);
        }

        if (isLinux)
        {
            var executable = isUnixExecutable ?? IsUnixExecutable;
            if (string.IsNullOrWhiteSpace(setsidPath)
                || !exists(setsidPath)
                || !executable(setsidPath))
            {
                return Refused(
                    WebLifecycleOwner.NativeProcess,
                    "backup_restore_setsid_unavailable",
                    ApiMessageKeys.BackupRestoreSetSidUnavailable);
            }
        }

        WebReplacementCommand? command;
        try
        {
            command = WebReplacementCommand.CreateNative(
                isLinux,
                isWindows,
                setsidPath,
                processPath,
                managedEntryPoint,
                hostArguments);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            command = null;
        }

        if (command is null)
        {
            return Refused(
                WebLifecycleOwner.NativeProcess,
                "backup_restore_executable_unavailable",
                ApiMessageKeys.BackupRestoreExecutableUnavailable);
        }

        return new(true, WebLifecycleOwner.NativeProcess,
            new NativeWebRestoreReplacement(endpoints.HealthUri, command, coreWasRunning));
    }

    private static NativeWebRestorePreflightResult Refused(
        WebLifecycleOwner owner,
        string code,
        string messageKey) => new(false, owner, null, code, messageKey);

    private static bool IsUnixExecutable(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            const UnixFileMode executeBits = UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & executeBits) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
