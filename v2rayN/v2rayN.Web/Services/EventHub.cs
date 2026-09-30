using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using v2rayN.Web.Contracts;

namespace v2rayN.Web.Services;

public sealed class EventHub
{
    private const int EventBufferCapacity = 256;
    private readonly ConcurrentDictionary<Guid, EventSubscriber> _subscribers = new();

    public void Publish(string type, object? data)
    {
        var message = new WebEvent(type, data, DateTimeOffset.UtcNow);
        foreach (var subscriber in _subscribers.Values)
        {
            if (message.Type != "log" || subscriber.IncludeLogs)
            {
                subscriber.Channel.Writer.TryWrite(message);
            }
        }
    }

    public async IAsyncEnumerable<WebEvent> Subscribe(
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        bool includeLogs = false)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<WebEvent>(new BoundedChannelOptions(EventBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers[id] = new EventSubscriber(channel, includeLogs);
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return message;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }

    private sealed record EventSubscriber(Channel<WebEvent> Channel, bool IncludeLogs);
}
