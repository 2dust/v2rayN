using System.Reflection;
using System.Runtime.CompilerServices;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.ViewModels;

public class SubscriptionReloadTests
{
    [Test]
    public async Task ReusedConfigurationDoesNotReapplyTheSystemProxy()
    {
        await MainWindowViewModel.RunCoreReloadAsync(
            () => Task.FromResult(true),
            () => throw new InvalidOperationException("An unchanged connection must not reapply system proxy or PAC."));
    }

    [Test]
    public async Task ARealReloadStillAppliesTheSystemProxy()
    {
        var applied = 0;
        await MainWindowViewModel.RunCoreReloadAsync(
            () => Task.FromResult(false),
            () => { applied++; return Task.CompletedTask; });
        await applied.Should().BeEqualTo(1);
    }

    [Test]
    public async Task ALoadExceptionDoesNotApplyTheSystemProxy()
    {
        var applied = 0;
        var propagated = false;
        try
        {
            await MainWindowViewModel.RunCoreReloadAsync(
                () => throw new IOException("Owned load failure."),
                () => { applied++; return Task.CompletedTask; });
        }
        catch (IOException)
        {
            propagated = true;
        }
        await propagated.Should().BeTrue();
        await applied.Should().BeEqualTo(0);
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, true)]
    public async Task PendingReloadPreservesForcePriority(bool first, bool second, bool expectedForce)
    {
        var pending = new PendingCoreReload();
        var modes = new List<bool>();
        pending.Request(first);
        pending.Request(second);

        await pending.RunPendingAsync(force => { modes.Add(force); return Task.CompletedTask; });
        await pending.RunPendingAsync(_ => throw new InvalidOperationException("The drained request ran twice."));

        await modes.Count.Should().BeEqualTo(1);
        await modes.Single().Should().BeEqualTo(expectedForce);
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, true)]
    public async Task BusyDesktopReloadPreservesForcePriority(bool first, bool second, bool expectedForce)
    {
        // Avoid the real constructor, which starts application initialization and creates UI VMs.
        var vm = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
        using var busyGate = new SemaphoreSlim(0, 1);
        var pending = new PendingCoreReload();
        typeof(MainWindowViewModel).GetField("_reloadSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, busyGate);
        typeof(MainWindowViewModel).GetField("_pendingReload", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, pending);
        var modes = new List<bool>();

        await vm.Reload(first);
        await vm.Reload(second);
        await pending.RunPendingAsync(force => { modes.Add(force); return Task.CompletedTask; });

        await modes.Count.Should().BeEqualTo(1);
        await modes.Single().Should().BeEqualTo(expectedForce);
    }

    [Test]
    public async Task NoPendingRequestDoesNotReload()
    {
        var pending = new PendingCoreReload();
        await pending.RunPendingAsync(_ => throw new InvalidOperationException("No reload was requested."));
    }

    [Test]
    public async Task ConcurrentSubscriptionRequestsRemainSoft()
    {
        var pending = new PendingCoreReload();
        Parallel.For(0, 64, _ => pending.Request(false));
        var modes = new List<bool>();

        await pending.RunPendingAsync(force => { modes.Add(force); return Task.CompletedTask; });

        await modes.SequenceEqual(new[] { false }).Should().BeTrue();
    }

    [Test]
    public async Task ConcurrentManualRequestCannotBeOverwrittenByASubscription()
    {
        var pending = new PendingCoreReload();
        Parallel.For(0, 64, i => pending.Request(i == 0));
        pending.Request(false);
        var modes = new List<bool>();

        await pending.RunPendingAsync(force => { modes.Add(force); return Task.CompletedTask; });

        await modes.SequenceEqual(new[] { true }).Should().BeTrue();
    }

    [Test]
    public async Task ARequestArrivingDuringDrainIsNotClearedByTheEarlierRequest()
    {
        var pending = new PendingCoreReload();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var modes = new List<bool>();
        pending.Request(false);
        var first = pending.RunPendingAsync(async force =>
        {
            modes.Add(force);
            entered.SetResult();
            await release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pending.Request(true);
        release.SetResult();
        await first;

        await pending.RunPendingAsync(force => { modes.Add(force); return Task.CompletedTask; });

        await modes.SequenceEqual(new[] { false, true }).Should().BeTrue();
    }
}
