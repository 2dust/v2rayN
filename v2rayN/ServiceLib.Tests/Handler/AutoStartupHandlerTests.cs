namespace ServiceLib.Tests.Handler;

// launchd terminates the processes of a LaunchAgent job when the job is unloaded. When v2rayN was
// started by its own LaunchAgent at login, "launchctl unload" on that agent kills v2rayN itself.
// These tests run the macOS code paths against a fake launchctl that only records its arguments.
[NotInParallel]
public class AutoStartupHandlerTests
{
    [Test]
    public async Task MacOS_SaveWithAutoRunEnabled_ShouldNotUnloadLaunchAgent()
    {
        await WithFakeLaunchctl(async (plistPath, readCalls) =>
        {
            await InvokeMacOS("SetTaskOSX");

            // UpdateTask on macOS when settings are saved with auto-start still enabled
            await InvokeMacOS("ClearTaskOSX");
            await InvokeMacOS("SetTaskOSX");

            var calls = await readCalls();
            await calls.Any(call => call.StartsWith("unload")).Should().BeFalse();
            await calls.Should().HaveCount(2);
            await calls[1].Should().BeEqualTo($"load -w {plistPath}");
            await File.ReadAllText(plistPath).Contains(Utils.GetExePath()).Should().BeTrue();
        });
    }

    [Test]
    public async Task MacOS_DisableAutoRun_ShouldRemovePlistWithoutUnload()
    {
        await WithFakeLaunchctl(async (plistPath, readCalls) =>
        {
            await InvokeMacOS("SetTaskOSX");

            // UpdateTask on macOS when auto-start is turned off
            await InvokeMacOS("ClearTaskOSX");

            var calls = await readCalls();
            await calls.Any(call => call.StartsWith("unload")).Should().BeFalse();
            await File.Exists(plistPath).Should().BeFalse();
        });
    }

    private static async Task InvokeMacOS(string methodName)
    {
        var method = typeof(AutoStartupHandler).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(null, null)!;
    }

    private static async Task WithFakeLaunchctl(Func<string, Func<Task<string[]>>, Task> test)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("v2rayN-autostart-").FullName;
        var binDir = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        var logPath = Path.Combine(root, "launchctl.log");
        var launchctl = Path.Combine(binDir, "launchctl");
        await File.WriteAllTextAsync(launchctl, $"#!/bin/sh\necho \"$*\" >> \"{logPath}\"\n");
        File.SetUnixFileMode(launchctl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var oldHome = Environment.GetEnvironmentVariable("HOME");
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("HOME", root);
            Environment.SetEnvironmentVariable("PATH", $"{binDir}:{oldPath}");
            var plistPath = Path.Combine(root, "Library", "LaunchAgents", $"{Global.AppName}-LaunchAgent.plist");

            await test(plistPath, async () => File.Exists(logPath) ? await File.ReadAllLinesAsync(logPath) : []);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", oldHome);
            Environment.SetEnvironmentVariable("PATH", oldPath);
            Directory.Delete(root, true);
        }
    }
}
