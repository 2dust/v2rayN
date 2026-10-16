namespace ServiceLib.Tests.Manager;

public class ProfileExManagerConcurrencyTests
{
    [Test]
    public async Task SetTestDelay_FromManyThreads_ShouldNotThrow()
    {
        var manager = CreateManager();
        var exceptions = new List<Exception>();
        var threads = Environment.ProcessorCount * 4;

        //Hammer the same managers from several streams at once: the profile list grows,
        //ids repeat and the queue is mutated while it is being scanned
        await Parallel.ForEachAsync(Enumerable.Range(0, 20000), new ParallelOptions { MaxDegreeOfParallelism = threads },
            async (i, ct) =>
            {
                try
                {
                    manager.SetTestDelay($"index{i % 500}", i);
                    if (i % 3 == 0)
                    {
                        manager.SetTestSpeed($"index{i % 700}", i);
                    }
                    if (i % 5 == 0)
                    {
                        manager.SetTestMessage($"index{i % 900}", "x");
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
                await Task.CompletedTask;
            });

        await exceptions.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task SetTestDelay_ShouldCreateOneEntryPerIndex()
    {
        var manager = CreateManager();

        await Parallel.ForEachAsync(Enumerable.Range(0, 500), new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                manager.SetTestDelay($"same{i % 10}", i);
                await Task.CompletedTask;
            });

        var items = await manager.GetProfileExs();
        await items.Count.Should().BeEqualTo(10);
        await items.Select(t => t.IndexId).Distinct().Count().Should().BeEqualTo(10);
    }

    [Test]
    public async Task IndexIdEnqueue_ShouldNotDuplicateIds()
    {
        var manager = CreateManager();
        var ids = 500;
        var threads = Environment.ProcessorCount * 2;
        var barrier = new Barrier(threads);

        Parallel.For(0, threads, t =>
        {
            //line all threads up so they really collide inside the queue check
            barrier.SignalAndWait();
            for (var i = 0; i < 5000; i++)
            {
                manager.SetTestDelay($"id{i % ids}", 1);
            }
        });

        var queue = (Queue<string>)typeof(ProfileExManager)
            .GetField("_queIndexIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;
        //"contains + enqueue" must be atomic, otherwise the same profile is queued twice
        await queue.Count.Should().BeEqualTo(ids);
    }

    private static ProfileExManager CreateManager()
    {
        return (ProfileExManager)Activator.CreateInstance(typeof(ProfileExManager), nonPublic: true)!;
    }
}