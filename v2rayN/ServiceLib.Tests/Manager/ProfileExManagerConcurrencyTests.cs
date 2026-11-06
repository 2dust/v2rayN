namespace ServiceLib.Tests.Manager;

public class ProfileExManagerConcurrencyTests
{
    [Test]
    public async Task SetTestDelay_FromManyThreads_ShouldNotThrow()
    {
        var manager = CreateManager();
        var exceptions = new List<Exception>();
        var threads = Environment.ProcessorCount * 2;

        //Hammer the same manager from several streams at once: the queue is mutated while
        //other threads keep pushing into it
        var barrier = new Barrier(threads);
        Parallel.For(0, threads, t =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 5000; i++)
            {
                try
                {
                    manager.SetTestDelay($"index{i % 500}", i);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }
        });

        await exceptions.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task SetTestDelay_FromManyThreads_ShouldNotLoseQueuedIds()
    {
        var manager = CreateManager();
        var ids = 500;

        Parallel.For(0, Environment.ProcessorCount * 2, t =>
        {
            for (var i = 0; i < 5000; i++)
            {
                manager.SetTestDelay($"id{i % ids}", i);
            }
        });

        var drained = Drain(manager);
        await drained.Count.Should().BeGreaterThanOrEqualTo(ids);
        await drained.Distinct().Count().Should().BeEqualTo(ids);
    }

    [Test]
    public async Task SetTestDelay_ShouldCreateOneEntryPerIndex()
    {
        var manager = CreateManager();

        await RunConcurrentUpdates(8, 10, (_, index) => manager.SetTestDelay($"same{index}", index));

        var items = await manager.GetProfileExs();
        await items.Count.Should().BeEqualTo(10);
        await items.Select(t => t.IndexId).Distinct().Count().Should().BeEqualTo(10);
    }

    [Test]
    public async Task SetTestResults_ConcurrentUpdates_ShouldKeepAllValuesOnOneEntry()
    {
        var manager = CreateManager();
        Action<string>[] updates =
        [
            indexId => manager.SetTestDelay(indexId, 123),
            indexId => manager.SetTestSpeed(indexId, 45.6m),
            indexId => manager.SetTestMessage(indexId, "result"),
            indexId => manager.SetTestIpInfo(indexId, "203.0.113.1"),
            indexId => manager.SetSort(indexId, 7)
        ];

        await RunConcurrentUpdates(updates.Length, 100, (worker, index) => updates[worker]($"profile{index}"));

        var items = await manager.GetProfileExs();
        await items.Count.Should().BeEqualTo(100);
        await items.Select(item => item.IndexId).Distinct().Count().Should().BeEqualTo(100);
        foreach (var item in items)
        {
            await item.Delay.Should().BeEqualTo(123);
            await item.Speed.Should().BeEqualTo(45.6m);
            await item.Message.Should().BeEqualTo("result");
            await item.IpInfo.Should().BeEqualTo("203.0.113.1");
            await item.Sort.Should().BeEqualTo(7);
        }
    }

    private static async Task RunConcurrentUpdates(int workers, int indexes, Action<int, int> update)
    {
        using var barrier = new Barrier(workers);
        var tasks = Enumerable.Range(0, workers).Select(worker => Task.Factory.StartNew(() =>
        {
            for (var index = 0; index < indexes; index++)
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Concurrent update workers did not synchronize.");
                }
                update(worker, index);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        await Task.WhenAll(tasks);
    }

    private static ProfileExManager CreateManager()
    {
        return (ProfileExManager)Activator.CreateInstance(typeof(ProfileExManager), nonPublic: true)!;
    }

    private static List<string> Drain(ProfileExManager manager)
    {
        var queue = (System.Collections.Concurrent.ConcurrentQueue<string>)typeof(ProfileExManager)
            .GetField("_queIndexIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;

        var result = new List<string>();
        while (queue.TryDequeue(out var id))
        {
            result.Add(id);
        }
        return result;
    }
}