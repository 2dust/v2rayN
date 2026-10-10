using System.Diagnostics;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class CoreProcessTrackerTests
{
    [Test]
    public async Task WebTrackerCapturesNewCoreProcessesAndPrunesExitedIdentities()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var executable = CopySleepExecutable(directory.Path);
        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        using var process = StartSleepProcess(executable);
        try
        {
            var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, [executable]);
            await tracked.Any(identity => identity.ProcessId == process.Id).Should().BeTrue();
            await CoreProcessTracker.GetActiveProcesses(tracked)
                .Any(identity => identity.ProcessId == process.Id)
                .Should().BeTrue();

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await CoreProcessTracker.GetActiveProcesses(tracked).Length.Should().BeEqualTo(0);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    public async Task WebTrackerDoesNotClaimProcessesThatPredateItsLaunchSnapshot()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var directory = new TemporaryDirectory();
        var executable = CopySleepExecutable(directory.Path);
        using var process = StartSleepProcess(executable);
        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, [executable]);

        try
        {
            await tracked.Any(identity => identity.ProcessId == process.Id).Should().BeFalse();
        }
        finally
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    [Test]
    public async Task WebTrackerRecognizesAnExecutableRunningThroughItsInterpreter()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "test-core-shim");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nsleep 60\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })
            ?? throw new InvalidOperationException("Could not start the Core shim test process.");
        try
        {
            var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, [executable]);
            await tracked.Any(identity => identity.ProcessId == process.Id).Should().BeTrue();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    public async Task WebTrackerDoesNotClaimSameNamedExecutableFromAnotherPath()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var unrelatedExecutable = Path.Combine(directory.Path, "sleep");
        File.Copy("/bin/sleep", unrelatedExecutable);

        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        using var process = StartSleepProcess(unrelatedExecutable);
        try
        {
            var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, ["/bin/sleep"]);
            await tracked.Any(identity => identity.ProcessId == process.Id).Should().BeFalse();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    public async Task LinuxTrackerRequiresTheWebCoreConfigPathWhenProvided()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "test-core-shim");
        var configPath = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nsleep 60\n");
        await File.WriteAllTextAsync(configPath, "{}");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        startInfo.ArgumentList.Add(configPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Core shim test process.");
        try
        {
            var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, [executable], [configPath]);
            await tracked.Any(identity => identity.ProcessId == process.Id).Should().BeTrue();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Test]
    public async Task WebTrackerRejectsReusedPidWithDifferentStartTimeOrExecutable()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var executable = CopySleepExecutable(directory.Path);
        var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
        using var process = StartSleepProcess(executable);
        try
        {
            var tracked = CoreProcessTracker.CaptureStartedProcesses(existingProcessIds, [executable]);
            var identity = tracked.Single(item => item.ProcessId == process.Id);

            await CoreProcessTracker.GetActiveProcesses([identity with { StartTimeUtcTicks = identity.StartTimeUtcTicks + 1 }])
                .Length.Should().BeEqualTo(0);
            await CoreProcessTracker.GetActiveProcesses([identity with { ExecutablePath = "/tmp/unrelated-core" }])
                .Length.Should().BeEqualTo(0);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static Process StartSleepProcess(string executable = "/bin/sleep")
    {
        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        startInfo.ArgumentList.Add("60");
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the test process.");
    }

    private static string CopySleepExecutable(string directory)
    {
        var executable = Path.Combine(directory, "sleep-core-test");
        File.Copy("/bin/sleep", executable);
        return executable;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-core-tracker-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
