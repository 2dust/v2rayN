namespace ServiceLib.Tests.Services;

public class SpeedtestServiceRetryTests
{
    [Test]
    [Arguments(-1, true)]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(300, false)]
    public async Task ShouldRetryRealPing_ShouldOnlyRetryNonPositiveDelay(int delay, bool expected)
    {
        var actual = (bool)InvokeStatic("ShouldRetryRealPing", delay)!;

        await actual.Should().BeEqualTo(expected);
    }

    [Test]
    public async Task GetRetestItems_WhenCoreStarted_ShouldOnlyReturnAllowedFailedItems()
    {
        var timeoutItem = new ServerTestItem { AllowTest = true, Delay = -1 };
        var zeroDelayItem = new ServerTestItem { AllowTest = true, Delay = 0 };
        var successItem = new ServerTestItem { AllowTest = true, Delay = 120 };
        var skippedItem = new ServerTestItem { AllowTest = false, Delay = -1 };
        var items = new List<ServerTestItem> { timeoutItem, zeroDelayItem, successItem, skippedItem };

        var result = InvokeGetRetestItems(items, true);

        await result.Count.Should().BeEqualTo(2);
        await result.Any(it => ReferenceEquals(it, timeoutItem)).Should().BeTrue();
        await result.Any(it => ReferenceEquals(it, zeroDelayItem)).Should().BeTrue();
    }

    [Test]
    public async Task GetRetestItems_WhenCoreFailedToStart_ShouldReturnWholeBatch()
    {
        var items = new List<ServerTestItem>
        {
            new() { AllowTest = true, Delay = 50 },
            new() { AllowTest = false, Delay = -1 },
        };

        var result = InvokeGetRetestItems(items, false);

        await result.Count.Should().BeEqualTo(items.Count);
        await result.SequenceEqual(items).Should().BeTrue();
    }

    private static object? InvokeStatic(string name, params object?[] args)
    {
        var method = typeof(SpeedtestService).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, args);
    }

    private static List<ServerTestItem> InvokeGetRetestItems(List<ServerTestItem> items, bool coreStarted)
    {
        return (List<ServerTestItem>)InvokeStatic("GetRetestItems", items, coreStarted)!;
    }
}
