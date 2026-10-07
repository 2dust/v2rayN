using System.Text.Json;
using v2rayN.WebAPI.Launcher;

namespace v2rayN.WebAPI.Tests;

public class NativeWebUpdateWorkflowTests
{
    [Test, NotInParallel]
    public async Task InFlightHelperProgressIsReReadUntilTheTerminalResult()
    {
        var previousLocalData = Environment.GetEnvironmentVariable(ServiceLib.Global.LocalAppData);
        Environment.SetEnvironmentVariable(ServiceLib.Global.LocalAppData, "0");
        var path = ServiceLib.Common.Utils.GetTempPath("web-update-progress.json");
        var previous = File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var runtime = new v2rayN.WebAPI.Services.V2rayRuntime(null!, null!, null!, null!, null!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(
                new WebUpdateProgressState("verifying-health", false, false, "7.25.91", null, CoreWasRunning: true), options));
            await runtime.GetWebUpdateProgress()!.IsComplete.Should().BeFalse();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(
                new WebUpdateProgressState("failed", true, false, "7.25.91", null, true, true), options));
            var terminal = runtime.GetWebUpdateProgress()!;
            await terminal.Phase.Should().BeEqualTo("failed");
            await terminal.IsComplete.Should().BeTrue();
            await terminal.RollbackSucceeded.Should().BeEqualTo(true);
            await terminal.CoreWasRunning.Should().BeTrue();
        }
        finally
        {
            if (previous is null) File.Delete(path);
            else await File.WriteAllBytesAsync(path, previous);
            Environment.SetEnvironmentVariable(ServiceLib.Global.LocalAppData, previousLocalData);
        }
    }

    [Test]
    public async Task PersistedProgressKeepsThePreUpdateCoreStateAndAcceptsOlderProgress()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = new WebUpdateProgressState("completed", true, true, "7.25.91", null,
            CoreWasRunning: true);
        var restored = JsonSerializer.Deserialize<WebUpdateProgressState>(JsonSerializer.Serialize(state, options), options);
        await restored!.CoreWasRunning.Should().BeTrue();

        var legacy = JsonSerializer.Deserialize<WebUpdateProgressState>(
            """{"phase":"completed","isComplete":true,"success":true,"version":"7.25.90"}""", options);
        await legacy!.CoreWasRunning.Should().BeFalse();
    }

    [Test]
    public async Task UpdateHealthRequiresTheOriginalRunningOrStoppedRuntimeIntent()
    {
        var runningIntent = new v2rayN.WebAPI.Services.RuntimeRestartIntent(true, "removed-profile");
        var recoveredWithFallback = new WebHealthProbeResult(
            true, 42, CoreProcessIds: [99], CoreState: "running", WebVersion: "7.25.2-web.9", CoreProfileId: "fallback-profile");
        await NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(runningIntent, recoveredWithFallback).Should().BeTrue();

        var stoppedIntent = new v2rayN.WebAPI.Services.RuntimeRestartIntent(false, "old-profile");
        var remainsStopped = new WebHealthProbeResult(true, 43, CoreProcessIds: [], CoreState: "stopped");
        await NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(stoppedIntent, remainsStopped).Should().BeTrue();
        await NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(stoppedIntent,
            recoveredWithFallback with { CoreState = "running" }).Should().BeFalse();
        await NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(runningIntent,
            remainsStopped with { CoreState = "stopped" }).Should().BeFalse();
    }

    [Test]
    public async Task HealthyNewBuildCommitsAndCleansThePreviousFiles()
    {
        var sequence = new List<string>();
        var result = await NativeWebUpdateWorkflow.ApplyAsync(
            () => { sequence.Add("install-new"); return Task.CompletedTask; },
            () => { sequence.Add("start-new-health-ok"); return Task.FromResult(true); },
            () => { sequence.Add("restore-old"); return Task.CompletedTask; },
            () => { sequence.Add("start-old"); return Task.FromResult(true); },
            sequence.Add,
            () => { sequence.Add("cleanup-backup"); return Task.CompletedTask; });

        await result.Success.Should().BeTrue();
        await result.RollbackSucceeded.Should().BeFalse();
        await sequence.Contains("restore-old").Should().BeFalse();
        await sequence.Last().Should().BeEqualTo("cleanup-backup");
    }

    [Test]
    public async Task NewProcessExitOrHealthTimeoutRestoresTheOldHealthyBuild()
    {
        var sequence = new List<string>();
        var result = await NativeWebUpdateWorkflow.ApplyAsync(
            () => { sequence.Add("install-new"); return Task.CompletedTask; },
            () => { sequence.Add("new-health-timeout"); return Task.FromResult(false); },
            () => { sequence.Add("restore-old-files"); return Task.CompletedTask; },
            () => { sequence.Add("old-health-ok"); return Task.FromResult(true); },
            sequence.Add,
            () => { sequence.Add("cleanup-backup"); return Task.CompletedTask; });

        await result.Success.Should().BeFalse();
        await result.RollbackSucceeded.Should().BeTrue();
        await sequence.IndexOf("restore-old-files").Should().BeGreaterThan(sequence.IndexOf("new-health-timeout"));
        await sequence.IndexOf("old-health-ok").Should().BeGreaterThan(sequence.IndexOf("restore-old-files"));
    }

    [Test]
    public async Task FileReplacementFailureStillAttemptsToRestartTheOldBuild()
    {
        var sequence = new List<string>();
        var result = await NativeWebUpdateWorkflow.ApplyAsync(
            () => throw new IOException("simulated replace failure"),
            () => { sequence.Add("new-health"); return Task.FromResult(false); },
            () => { sequence.Add("restore-old-files"); return Task.CompletedTask; },
            () => { sequence.Add("old-health-ok"); return Task.FromResult(true); },
            sequence.Add,
            () => { sequence.Add("cleanup-backup"); return Task.CompletedTask; });

        await result.RollbackSucceeded.Should().BeTrue();
        await result.Detail.Should().Contain("simulated replace failure");
        await sequence.Contains("new-health").Should().BeFalse();
        await sequence.Contains("old-health-ok").Should().BeTrue();
    }

    [Test]
    public async Task FailedOldBuildHealthIsReportedWithoutDeletingTheBackup()
    {
        var backupCleaned = false;
        var result = await NativeWebUpdateWorkflow.ApplyAsync(
            () => Task.CompletedTask,
            () => Task.FromResult(false),
            () => Task.CompletedTask,
            () => Task.FromResult(false),
            _ => { },
            () => { backupCleaned = true; return Task.CompletedTask; });

        await result.Success.Should().BeFalse();
        await result.RollbackSucceeded.Should().BeFalse();
        await backupCleaned.Should().BeFalse();
        await result.Detail.Should().Contain("did not become healthy");
    }
}
