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
        await pending.TryStart(false).Should().BeTrue();
        await pending.TryStart(first).Should().BeFalse();
        await pending.TryStart(second).Should().BeFalse();

        var next = pending.Complete();
        await next.HasValue.Should().BeTrue();
        await next.Value.Should().BeEqualTo(expectedForce);
        await pending.Complete().HasValue.Should().BeFalse();
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
        var pending = new PendingCoreReload();
        await pending.TryStart(false).Should().BeTrue();
        typeof(MainWindowViewModel).GetField("_pendingReload", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, pending);

        await vm.Reload(first);
        await vm.Reload(second);

        var next = pending.Complete();
        await next.HasValue.Should().BeTrue();
        await next.Value.Should().BeEqualTo(expectedForce);
        await pending.Complete().HasValue.Should().BeFalse();
    }

    [Test]
    public async Task NoPendingRequestDoesNotReload()
    {
        var pending = new PendingCoreReload();
        await pending.TryStart(false).Should().BeTrue();
        await pending.Complete().HasValue.Should().BeFalse();
        await pending.TryStart(true).Should().BeTrue();
        await pending.Complete().HasValue.Should().BeFalse();
    }

    [Test]
    public async Task ConcurrentSubscriptionRequestsRemainSoft()
    {
        var pending = new PendingCoreReload();
        pending.TryStart(false);
        Parallel.For(0, 64, _ => pending.TryStart(false));

        await pending.Complete().Should().BeEqualTo((bool?)false);
        await pending.Complete().HasValue.Should().BeFalse();
    }

    [Test]
    public async Task ConcurrentManualRequestCannotBeOverwrittenByASubscription()
    {
        var pending = new PendingCoreReload();
        pending.TryStart(false);
        Parallel.For(0, 64, i => pending.TryStart(i == 0));
        pending.TryStart(false);

        await pending.Complete().Should().BeEqualTo((bool?)true);
        await pending.Complete().HasValue.Should().BeFalse();
    }

    [Test]
    public async Task ARequestArrivingDuringHandoffIsNotClearedByTheEarlierRequest()
    {
        var pending = new PendingCoreReload();
        pending.TryStart(false);
        pending.TryStart(false);
        await pending.Complete().Should().BeEqualTo((bool?)false);
        // Ownership is retained for the pending soft reload until that reload completes.
        await pending.TryStart(true).Should().BeFalse();
        await pending.Complete().Should().BeEqualTo((bool?)true);
        await pending.Complete().HasValue.Should().BeFalse();
    }

    [Test]
    public async Task CompletingWhileACallerWaitsForTheStateLockDoesNotStrandTheCaller()
    {
        var pending = new PendingCoreReload();
        pending.TryStart(false);
        var sync = typeof(PendingCoreReload).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pending)!;
        using var entered = new ManualResetEventSlim();
        bool? becameOwner = null;
        Exception? failure = null;
        var caller = new Thread(() =>
        {
            try { entered.Set(); becameOwner = pending.TryStart(true); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        bool? oldOwnerNext;
        lock (sync)
        {
            caller.Start();
            if (!entered.Wait(TimeSpan.FromSeconds(5))
                || !SpinWait.SpinUntil(() => (caller.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The owned caller did not reach the controlled state lock.");
            }
            oldOwnerNext = pending.Complete();
        }
        if (!caller.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("The owned caller did not finish.");
        if (failure is not null) throw failure;

        await oldOwnerNext.HasValue.Should().BeFalse();
        await becameOwner.Should().BeEqualTo((bool?)true);
        await pending.Complete().HasValue.Should().BeFalse();
    }
}
