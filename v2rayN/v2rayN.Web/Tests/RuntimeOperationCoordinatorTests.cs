using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class RuntimeOperationCoordinatorTests
{
    [Test]
    public async Task ExclusiveLeaseBlocksNewSharedOperations()
    {
        var coordinator = new RuntimeOperationCoordinator();
        var exclusive = await coordinator.EnterExclusiveAsync();
        var pending = coordinator.EnterOperationAsync().AsTask();

        await Task.Delay(25);
        await pending.IsCompleted.Should().BeFalse();

        await exclusive.DisposeAsync();
        await using var shared = await pending;
        await shared.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Test]
    public async Task RequestUsesItsExistingSharedLeaseWhenAnExclusiveWaiterArrives()
    {
        var coordinator = new RuntimeOperationCoordinator();
        var requestLease = await coordinator.EnterOperationAsync();
        var exclusiveTask = coordinator.EnterExclusiveAsync().AsTask();
        await Task.Delay(25);
        await exclusiveTask.IsCompleted.Should().BeFalse();

        // Runtime work executes under the middleware-owned lease. It must not acquire another
        // shared lease here: an exclusive waiter would otherwise block this work from finishing
        // and the outer request could never release its lease.
        var requestWork = Task.Run(async () =>
        {
            requestLease.Token.ThrowIfCancellationRequested();
            await Task.Yield();
            return true;
        });
        await (await requestWork.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        // A later HTTP request queues behind the exclusive waiter.
        var nextRequestTask = coordinator.EnterOperationAsync().AsTask();
        await Task.Delay(25);
        await nextRequestTask.IsCompleted.Should().BeFalse();

        await requestLease.DisposeAsync();
        await using (var maintenance = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            await maintenance.Token.IsCancellationRequested.Should().BeFalse();
        }
        await using var nextRequest = await nextRequestTask.WaitAsync(TimeSpan.FromSeconds(2));
        await nextRequest.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Test]
    public async Task ReadOnlyObserversRemainAvailableDuringUpdateMaintenance()
    {
        var coordinator = new RuntimeOperationCoordinator();
        await using var maintenance = await coordinator.EnterExclusiveAsync(allowReadOnlyObservations: true);

        await using var status = await coordinator.EnterObservationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await using var operations = await coordinator.EnterObservationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await using var logs = await coordinator.EnterObservationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        var mutation = coordinator.EnterOperationAsync().AsTask();

        await (status.Token.CanBeCanceled && operations.Token.CanBeCanceled && logs.Token.CanBeCanceled).Should().BeTrue();
        await mutation.IsCompleted.Should().BeFalse();
        await maintenance.DisposeAsync();
        await using var mutationLease = await mutation.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ReadOnlyObserversStillWaitDuringMaintenanceThatDoesNotAllowThem()
    {
        var coordinator = new RuntimeOperationCoordinator();
        await using var maintenance = await coordinator.EnterExclusiveAsync();
        var observer = coordinator.EnterObservationAsync().AsTask();

        await observer.IsCompleted.Should().BeFalse();
        await maintenance.DisposeAsync();
        await using var lease = await observer.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task SharedUpdateStagingDoesNotBlockObservationRequests()
    {
        var coordinator = new RuntimeOperationCoordinator();
        await using var staging = await coordinator.EnterOperationAsync();
        await using var status = await coordinator.EnterOperationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await using var operations = await coordinator.EnterOperationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await using var logs = await coordinator.EnterOperationAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        await (!status.Token.IsCancellationRequested
               && !operations.Token.IsCancellationRequested
               && !logs.Token.IsCancellationRequested).Should().BeTrue();
    }

    [Test]
    public async Task ShutdownCancelsAndDrainsActiveLeases()
    {
        var coordinator = new RuntimeOperationCoordinator();
        var operation = await coordinator.EnterOperationAsync();
        var shutdown = coordinator.StopAndDrainAsync(CancellationToken.None);

        for (var attempt = 0; attempt < 100 && !operation.Token.IsCancellationRequested; attempt++)
        {
            await Task.Delay(10);
        }
        await operation.Token.IsCancellationRequested.Should().BeTrue();
        await operation.DisposeAsync();
        await shutdown;
    }

    [Test]
    public async Task ShutdownDrainHasAFiniteBestEffortDeadline()
    {
        var coordinator = new RuntimeOperationCoordinator();
        var operation = await coordinator.EnterOperationAsync();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var drained = await coordinator.StopAndDrainAsync(CancellationToken.None, TimeSpan.FromMilliseconds(40));

        await drained.Should().BeFalse();
        await operation.Token.IsCancellationRequested.Should().BeTrue();
        await stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        await operation.DisposeAsync();
    }
}
