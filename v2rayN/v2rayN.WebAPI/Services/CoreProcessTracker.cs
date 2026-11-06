using System.ComponentModel;
using System.Diagnostics;

namespace v2rayN.WebAPI.Services;

internal sealed record CoreProcessIdentity(int ProcessId, string ProcessName, long StartTimeUtcTicks, string ExecutablePath);

/// <summary>
/// Tracks Core processes started by this Web host without depending on ServiceLib internals.
/// CoreManager remains responsible for launching/stopping; Web snapshots process identities
/// around its public LoadCore call and validates those identities during runtime transitions.
/// </summary>
internal static class CoreProcessTracker
{
    public static HashSet<int> CaptureExistingProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { ids.Add(process.Id); }
                catch (InvalidOperationException) { }
            }
        }
        return ids;
    }

    public static CoreProcessIdentity[] CaptureStartedProcesses(
        IReadOnlySet<int> existingProcessIds,
        IEnumerable<string> expectedExecutablePaths,
        IEnumerable<string>? expectedConfigPaths = null)
    {
        var expected = expectedExecutablePaths
            .Select(NormalizeExecutablePath)
            .Where(path => path is not null)
            .Select(path => path!)
            .ToHashSet(PathComparer);
        var expectedConfigs = expectedConfigPaths?
            .Select(NormalizeAbsolutePath)
            .Where(path => path is not null)
            .Select(path => path!)
            .ToHashSet(PathComparer);
        if (expected.Count == 0) return [];

        var processes = new List<CoreProcessIdentity>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (existingProcessIds.Contains(process.Id) || process.HasExited) continue;
                    var startTimeTicks = GetStartTimeUtcTicks(process);
                    var executablePath = GetProcessExecutablePath(process);
                    if (startTimeTicks is null || executablePath is null
                        || !IsExpectedCoreProcess(process.Id, executablePath, expected, expectedConfigs))
                    {
                        continue;
                    }

                    processes.Add(new CoreProcessIdentity(
                        process.Id,
                        NormalizeProcessName(process.ProcessName),
                        startTimeTicks.Value,
                        executablePath));
                }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // A process may exit or become inaccessible while the process table is sampled.
                }
            }
        }

        return processes
            .DistinctBy(process => process.ProcessId)
            .OrderBy(process => process.ProcessId)
            .ToArray();
    }

    public static CoreProcessIdentity[] GetActiveProcesses(IEnumerable<CoreProcessIdentity> identities)
    {
        var active = new List<CoreProcessIdentity>();
        foreach (var identity in identities.DistinctBy(process => process.ProcessId))
        {
            try
            {
                using var process = Process.GetProcessById(identity.ProcessId);
                if (process.HasExited
                    || !string.Equals(NormalizeProcessName(process.ProcessName), identity.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var startTimeTicks = GetStartTimeUtcTicks(process);
                if (startTimeTicks is long observedStartTime && observedStartTime != identity.StartTimeUtcTicks) continue;
                var executablePath = GetProcessExecutablePath(process);
                if (executablePath is not null && !PathComparer.Equals(executablePath, identity.ExecutablePath)) continue;

                // If an otherwise matching live PID becomes inaccessible, keep it as an
                // uncertain active identity. This may report a conservative stop failure,
                // but it cannot mistake the process for stopped or authorize killing a PID.
                active.Add(identity);
            }
            catch (ArgumentException)
            {
                // The PID no longer exists.
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException or UnauthorizedAccessException)
            {
                // An inaccessible PID is uncertain, so report it as active rather than
                // claiming a clean stop. No process is ever terminated from this tracker.
                active.Add(identity);
            }
        }
        return active.OrderBy(process => process.ProcessId).ToArray();
    }

    private static string NormalizeProcessName(string name) =>
        Path.GetFileNameWithoutExtension(name.Trim());

    private static bool IsExpectedCoreProcess(
        int processId,
        string executablePath,
        HashSet<string> expectedPaths,
        HashSet<string>? expectedConfigPaths)
    {
        if (!OperatingSystem.IsLinux()) return expectedPaths.Contains(executablePath);

        try
        {
            var arguments = File.ReadAllText($"/proc/{processId}/cmdline")
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            var executableMatches = expectedPaths.Contains(executablePath);
            foreach (var expectedPath in expectedPaths)
            {
                if (arguments.Any(argument => PathComparer.Equals(NormalizeExecutablePath(argument), expectedPath))
                    && GetShebangInterpreters(expectedPath).Contains(executablePath, PathComparer))
                {
                    executableMatches = true;
                    break;
                }
            }

            return executableMatches && (expectedConfigPaths is null
                || expectedConfigPaths.Count == 0
                || CommandLineReferencesConfig(processId, arguments, expectedConfigPaths));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Process metadata can disappear between enumeration and inspection.
        }

        return false;
    }

    private static bool CommandLineReferencesConfig(int processId, IEnumerable<string> arguments, HashSet<string> expectedConfigPaths)
    {
        var workingDirectory = GetProcessWorkingDirectory(processId);
        if (workingDirectory is null) return false;

        foreach (var argument in arguments)
        {
            var configArgument = argument.StartsWith("--config=", StringComparison.Ordinal)
                ? argument["--config=".Length..]
                : argument.StartsWith("--config-file=", StringComparison.Ordinal)
                    ? argument["--config-file=".Length..]
                    : argument;
            if (configArgument.Length == 0 || configArgument.StartsWith("-", StringComparison.Ordinal)) continue;
            var fullPath = NormalizeAbsolutePath(Path.IsPathRooted(configArgument)
                ? configArgument
                : Path.Combine(workingDirectory, configArgument));
            if (fullPath is not null && expectedConfigPaths.Contains(fullPath)) return true;
        }

        return false;
    }

    private static string? GetProcessWorkingDirectory(int processId)
    {
        try
        {
            return new DirectoryInfo($"/proc/{processId}/cwd").ResolveLinkTarget(returnFinalTarget: false)?.FullName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static long? GetStartTimeUtcTicks(Process process)
    {
        try { return process.StartTime.ToUniversalTime().Ticks; }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetProcessExecutablePath(Process process)
    {
        try
        {
            var path = OperatingSystem.IsLinux()
                ? new FileInfo($"/proc/{process.Id}/exe").ResolveLinkTarget(returnFinalTarget: false)?.FullName
                : process.MainModule?.FileName;
            return path is null ? null : NormalizeExecutablePath(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    private static string? NormalizeExecutablePath(string? path)
    {
        var fullPath = NormalizeAbsolutePath(path);
        if (fullPath is null) return null;
        try
        {
            var executablePath = fullPath;
            if (OperatingSystem.IsLinux() && executablePath.EndsWith(" (deleted)", StringComparison.Ordinal))
            {
                executablePath = executablePath[..^" (deleted)".Length];
            }

            if (!OperatingSystem.IsLinux())
            {
                var target = new FileInfo(executablePath).ResolveLinkTarget(returnFinalTarget: true);
                return Path.GetFullPath(target?.FullName ?? executablePath);
            }

            var root = Path.GetPathRoot(executablePath)!;
            var current = root;
            foreach (var segment in executablePath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                var linkTarget = info.ResolveLinkTarget(returnFinalTarget: false);
                if (linkTarget is not null) current = linkTarget.FullName;
            }
            return Path.GetFullPath(current);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? NormalizeAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return null;
        try { return Path.GetFullPath(path.TrimEnd('\0')); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static HashSet<string> GetShebangInterpreters(string executablePath)
    {
        var interpreters = new HashSet<string>(PathComparer);
        try
        {
            using var stream = File.OpenRead(executablePath);
            var prefix = new byte[256];
            var length = stream.Read(prefix);
            var lineEnd = Array.IndexOf(prefix, (byte)'\n', 0, length);
            if (lineEnd < 0) lineEnd = length;
            var firstLine = System.Text.Encoding.UTF8.GetString(prefix, 0, lineEnd).TrimEnd('\r');
            if (!firstLine.StartsWith("#!", StringComparison.Ordinal)) return interpreters;

            var tokens = firstLine[2..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return interpreters;
            var interpreter = tokens[0];
            if (string.Equals(Path.GetFileName(interpreter), "env", StringComparison.Ordinal))
            {
                var index = 1;
                while (index < tokens.Length && tokens[index].StartsWith("-", StringComparison.Ordinal))
                {
                    if (tokens[index] is "-u" or "--unset" or "-C" or "--chdir" or "-a" or "--argv0") index++;
                    index++;
                }
                if (index >= tokens.Length) return interpreters;
                interpreter = FindOnPath(tokens[index]) ?? string.Empty;
            }

            var normalized = NormalizeExecutablePath(interpreter);
            if (normalized is not null) interpreters.Add(normalized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A script that cannot be inspected is not claimed based on its process name alone.
        }

        return interpreters;
    }

    private static string? FindOnPath(string executableName)
    {
        if (Path.IsPathRooted(executableName)) return executableName;
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
