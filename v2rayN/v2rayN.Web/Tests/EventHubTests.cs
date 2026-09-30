using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class EventHubTests
{
    [Test]
    public async Task LogEventsAreNotQueuedForSubscribersWhenTheLogsPageIsInactive()
    {
        var hub = new EventHub();
        using var cancellation = new CancellationTokenSource();
        await using var subscription = hub.Subscribe(cancellation.Token, includeLogs: false).GetAsyncEnumerator();
        var next = subscription.MoveNextAsync().AsTask();

        hub.Publish("log", new { message = "high-frequency core output" });
        hub.Publish("core-state", new { state = "running" });

        await next.WaitAsync(TimeSpan.FromSeconds(1));
        await subscription.Current.Type.Should().BeEqualTo("core-state");
        cancellation.Cancel();
    }

    [Test]
    public async Task LogEventsAreDeliveredWhenTheLogsPageSubscribesForThem()
    {
        var hub = new EventHub();
        using var cancellation = new CancellationTokenSource();
        await using var subscription = hub.Subscribe(cancellation.Token, includeLogs: true).GetAsyncEnumerator();
        var next = subscription.MoveNextAsync().AsTask();

        hub.Publish("log", new { message = "visible output" });

        await next.WaitAsync(TimeSpan.FromSeconds(1));
        await subscription.Current.Type.Should().BeEqualTo("log");
        cancellation.Cancel();
    }

    [Test]
    public async Task LogsClearedControlEventsReachSubscribersWithoutTheLogsPayloadStream()
    {
        var hub = new EventHub();
        using var cancellation = new CancellationTokenSource();
        await using var subscription = hub.Subscribe(cancellation.Token, includeLogs: false).GetAsyncEnumerator();
        var next = subscription.MoveNextAsync().AsTask();

        hub.Publish("logs-cleared", new { generation = 1 });

        await next.WaitAsync(TimeSpan.FromSeconds(1));
        await subscription.Current.Type.Should().BeEqualTo("logs-cleared");
        cancellation.Cancel();
    }
}
