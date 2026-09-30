using v2rayN.Web.Launcher;

namespace v2rayN.Web.Tests;

public class NativeWebUpdateWorkflowTests
{
    [Test]
    public async Task UpdateHealthRequiresTheOriginalRunningOrStoppedRuntimeIntent()
    {
        var runningIntent = new v2rayN.Web.Services.RuntimeRestartIntent(true, "removed-profile");
        var recoveredWithFallback = new WebHealthProbeResult(
            true, 42, CoreProcessIds: [99], CoreState: "running", WebVersion: "7.25.2-web.9", CoreProfileId: "fallback-profile");
        await NativeWebUpdateWorkflow.RuntimeIntentIsSatisfied(runningIntent, recoveredWithFallback).Should().BeTrue();

        var stoppedIntent = new v2rayN.Web.Services.RuntimeRestartIntent(false, "old-profile");
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
