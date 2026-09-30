using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class ShutdownCleanupSequenceTests
{
    [Test]
    public async Task CleanupStopsAnActiveCoreBeforeSavingAndClosingSharedState()
    {
        var coreRunning = true;
        var databaseClosed = false;
        var visited = new List<string>();
        var steps = new ShutdownCleanupStep[]
        {
            new("scheduled operations", _ => Task.FromResult(true)),
            new("operation drain", _ => Task.FromResult(true)),
            new("Core stop", _ =>
            {
                visited.Add("Core stop");
                coreRunning = false;
                return Task.FromResult(true);
            }),
            new("profile save", _ => { visited.Add("profile save"); return Task.FromResult(true); }),
            new("statistics save/close", _ => { visited.Add("statistics save/close"); return Task.FromResult(true); }),
            new("configuration save", _ => { visited.Add("configuration save"); return Task.FromResult(true); }),
            new("database close", _ => { visited.Add("database close"); databaseClosed = true; return Task.FromResult(true); }),
        };

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            TimeSpan.FromSeconds(1),
            CancellationToken.None,
            _ => { });

        await completed.Should().BeTrue();
        await coreRunning.Should().BeFalse();
        await databaseClosed.Should().BeTrue();
        await visited.SequenceEqual(["Core stop", "profile save", "statistics save/close", "configuration save", "database close"]).Should().BeTrue();
    }

    [Test]
    public async Task CoreStopTimeoutIsReportedByNameAndPreventsDatabaseClose()
    {
        var neverCompletingCoreStop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var databaseClosed = false;
        var messages = new List<string>();
        var stages = new List<string?>();
        var steps = new ShutdownCleanupStep[]
        {
            new("Core stop", _ => neverCompletingCoreStop.Task),
            new("database close", _ => { databaseClosed = true; return Task.FromResult(true); }),
        };

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            TimeSpan.FromMilliseconds(70),
            CancellationToken.None,
            messages.Add,
            stage =>
            {
                stages.Add(stage);
                ShutdownDiagnostics.SetStage(stage);
            });

        await completed.Should().BeFalse();
        await databaseClosed.Should().BeFalse();
        await messages.Any(message => message.Contains("Core stop", StringComparison.Ordinal)).Should().BeTrue();
        await (stages.FirstOrDefault() == "Core stop").Should().BeTrue();
        await ShutdownDiagnostics.CurrentStage.Should().BeEqualTo("Core stop");
        neverCompletingCoreStop.TrySetResult(true);
        ShutdownDiagnostics.SetStage(null);
    }

    [Test]
    public async Task RuntimeHostAndLauncherShutdownBudgetsLeaveOrderedMargins()
    {
        await (RuntimeShutdownBudgets.RuntimeCleanup < RuntimeShutdownBudgets.HostShutdown).Should().BeTrue();
        await (RuntimeShutdownBudgets.HostShutdown < RuntimeShutdownBudgets.LauncherWait).Should().BeTrue();
        await (RuntimeShutdownBudgets.HostShutdown - RuntimeShutdownBudgets.RuntimeCleanup)
            .Should().BeEqualTo(TimeSpan.FromSeconds(5));
        await (RuntimeShutdownBudgets.LauncherWait - RuntimeShutdownBudgets.HostShutdown)
            .Should().BeEqualTo(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ActiveOperationDrainTimeoutSkipsDatabaseClose()
    {
        var coordinator = new RuntimeOperationCoordinator();
        await using var activeOperation = await coordinator.EnterOperationAsync();
        var databaseCloseStarted = false;
        var messages = new List<string>();
        var steps = new ShutdownCleanupStep[]
        {
            new("operation drain", async deadline =>
                await coordinator.StopAndDrainAsync(deadline.Token, deadline.Remaining)),
            new("database close", _ =>
            {
                databaseCloseStarted = true;
                return Task.FromResult(true);
            }),
        };

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            TimeSpan.FromMilliseconds(80),
            CancellationToken.None,
            messages.Add);

        await completed.Should().BeFalse();
        await databaseCloseStarted.Should().BeFalse();
        await messages.Any(message => message.Contains("operation drain", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
        await activeOperation.DisposeAsync();
    }

    [Test]
    public async Task TimedOutCleanupStepPreventsLaterSharedResourceCleanup()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletingStep = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var databaseCloseStarted = false;
        var steps = new ShutdownCleanupStep[]
        {
            new("profile save", _ =>
            {
                started.TrySetResult(true);
                return neverCompletingStep.Task;
            }),
            new("database close", _ =>
            {
                databaseCloseStarted = true;
                return Task.FromResult(true);
            }),
        };

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            TimeSpan.FromMilliseconds(80),
            CancellationToken.None,
            _ => { });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await completed.Should().BeFalse();
        await databaseCloseStarted.Should().BeFalse();
        neverCompletingStep.TrySetResult(true);
    }

    [Test]
    public async Task ShutdownUsesOneFiniteOverallBudget()
    {
        var neverCompletingStep = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var steps = new ShutdownCleanupStep[]
        {
            new("first cleanup", async _ =>
            {
                await Task.Delay(50);
                return true;
            }),
            new("second cleanup", _ => neverCompletingStep.Task),
            new("third cleanup", _ => Task.FromResult(true)),
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            _ => { });

        await completed.Should().BeFalse();
        await stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        neverCompletingStep.TrySetResult(true);
    }
}
