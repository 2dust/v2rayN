using System.Threading.Channels;

namespace ServiceLib.Services.AppRouting;

internal sealed class RouteUdpSession : IDisposable
{
    private const int MaxQueuedBytes = 64 * 1024;
    private readonly CancellationTokenSource _stop;
    private sealed record Datagram(IPEndPoint Destination, byte[] Bytes);
    private readonly Channel<Datagram> _queue = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    });
    private int _queuedBytes;
    private readonly AppRouteRule _rule;
    public AppRouteRule Rule => _rule;
    private readonly IPEndPoint _destination;
    private readonly Action<IPEndPoint, byte[]> _reply;
    private readonly Func<bool> _ownsFlow;
    private Socket? _socket;
    private Socket? _control;
    public Task Completion
    {
        get;
    }
    public bool IsUsable => !Completion.IsCompleted && _ownsFlow();
    private long _lastActivity = Environment.TickCount64;
    public long LastActivity => Interlocked.Read(ref _lastActivity);

    public RouteUdpSession(AppRouteRule rule, IPEndPoint destination, Action<IPEndPoint, byte[]> reply, CancellationToken token, Action<Exception> error, Func<bool> ownsFlow)
    {
        _rule = rule;
        _destination = destination;
        _reply = reply;
        _ownsFlow = ownsFlow;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        Completion = Run(error);
    }

    // Engine packet processing serializes producers. TryWrite never waits for capacity.
    public void Send(IPEndPoint destination, ReadOnlySpan<byte> payload)
    {
        if (!IsUsable)
        {
            Dispose();
            return;
        }
        Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
        if (Volatile.Read(ref _queuedBytes) + payload.Length > MaxQueuedBytes)
        {
            return;
        }
        var bytes = payload.ToArray();
        Interlocked.Add(ref _queuedBytes, bytes.Length);
        if (!_queue.Writer.TryWrite(new(destination, bytes)))
        {
            Interlocked.Add(ref _queuedBytes, -bytes.Length);
        }
    }

    private async Task Run(Action<Exception> error)
    {
        try
        {
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            if (_rule.Kind == AppRouteKind.Interface)
            {
                _socket = RouteConnector.CreateInterfaceSocket(_rule, _destination, ProtocolType.Udp);
            }
            else
            {
                _control = await RouteConnector.ConnectProxy(_rule, connectTimeout.Token);
                var local = ((IPEndPoint)_control.LocalEndPoint!).Address;
                if (local.IsIPv4MappedToIPv6)
                {
                    local = local.MapToIPv4();
                }

                _socket = new Socket(local.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(local, 0));
                var relay = await RouteConnector.Request(_control, 3, (IPEndPoint)_socket.LocalEndPoint!, connectTimeout.Token);
                if (relay is IPEndPoint ipRelay && ipRelay.AddressFamily != _socket.AddressFamily)
                {
                    throw new IOException("Invalid SOCKS5 UDP relay endpoint.");
                }

                await _socket.ConnectAsync(relay, connectTimeout.Token);
            }
            var receive = Receive();
            var send = SendLoop();
            var control = _control == null ? Task.Delay(Timeout.Infinite, _stop.Token) : WatchControl();
            try
            {
                await await Task.WhenAny(receive, send, control);
            }
            finally
            {
                _stop.Cancel();
                _socket.Dispose();
                _control?.Dispose();
                try
                {
                    await Task.WhenAll(receive, send, control);
                }
                catch (Exception) when (_stop.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (!_stop.IsCancellationRequested) { error(new TimeoutException("UDP route connection timed out.")); }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        catch (Exception ex) { error(ex); }
        finally
        {
            _socket?.Dispose();
            _control?.Dispose();
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out var pending))
            {
                Interlocked.Add(ref _queuedBytes, -pending.Bytes.Length);
            }
            _stop.Dispose();
        }
    }

    private async Task WatchControl()
    {
        var data = new byte[1];
        await _control!.ReceiveAsync(data, SocketFlags.None, _stop.Token);
        throw new IOException("SOCKS5 UDP association closed.");
    }

    private async Task SendLoop()
    {
        await foreach (var datagram in _queue.Reader.ReadAllAsync(_stop.Token))
        {
            var bytes = datagram.Bytes;
            Interlocked.Add(ref _queuedBytes, -bytes.Length);
            // An association can take time to open; queued packets must not outlive their owner.
            if (!_ownsFlow())
            {
                return;
            }
            if (_rule.Kind == AppRouteKind.Interface)
            {
                var destination = datagram.Destination;
                if (destination.Address.IsIPv6LinkLocal)
                {
                    var scope = ((IPEndPoint)_socket!.LocalEndPoint!).Address.ScopeId;
                    destination = new(new IPAddress(destination.Address.GetAddressBytes(), scope), destination.Port);
                }
                await _socket!.SendToAsync(bytes, SocketFlags.None, destination, _stop.Token);
            }
            else
            {
                await _socket!.SendAsync(RouteConnector.WrapDatagram(datagram.Destination, bytes), SocketFlags.None, _stop.Token);
            }
        }
    }

    private async Task Receive()
    {
        var bytes = new byte[65535];
        while (!_stop.IsCancellationRequested)
        {
            int count, offset;
            IPEndPoint? peer;
            if (_rule.Kind == AppRouteKind.Interface)
            {
                var any = _destination.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any;
                var received = await _socket!.ReceiveFromAsync(bytes, SocketFlags.None, new IPEndPoint(any, 0), _stop.Token);
                count = received.ReceivedBytes;
                peer = (IPEndPoint)received.RemoteEndPoint;
                offset = 0;
            }
            else
            {
                count = await _socket!.ReceiveAsync(bytes, SocketFlags.None, _stop.Token);
                offset = RouteConnector.UnwrapDatagram(bytes.AsSpan(0, count), out peer);
            }
            if (offset < 0 || peer?.AddressFamily != _destination.AddressFamily)
            {
                continue;
            }

            if (!_ownsFlow())
            {
                return;
            }

            Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
            _reply(peer, bytes.AsSpan(offset, count - offset).ToArray());
        }
    }

    public void Dispose()
    {
        try
        {
            _stop.Cancel();
        }
        catch (ObjectDisposedException) { }
        _socket?.Dispose();
        _control?.Dispose();
    }
}
