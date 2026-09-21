using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

/// <summary>
/// Windows outbound TCP reflection and UDP relay. Only explicitly selected executable paths are routed.
/// WinDivert's streamdump example documents the TCP reflection technique; no TLS interception is used.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class AppRouteEngine : IRouteEngine
{
    private readonly CancellationTokenSource _stop = new();
    private readonly RouteAttributionSource _attribution = new();
    private readonly RoutePendingPackets _pending = new();
    private readonly SemaphoreSlim _refreshRequest = new(0, 1);
    private readonly TaskCompletionSource<Exception?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed record Routing(RoutePolicy Policy, RouteAttributionSnapshot Owners);
    private Routing _routing;
    private readonly RouteNatTable _nat = new();
    private readonly RouteFragmentBuffer _fragments;
    private readonly record struct UdpEndpoint(RouteProcessKey Process, IPAddress Local, ushort Port, string RuleId);
    private readonly ConcurrentDictionary<UdpEndpoint, RouteUdpSession> _udp = new();
    private readonly ConcurrentDictionary<long, Task> _connections = new();
    private readonly ConcurrentDictionary<long, Task> _sessions = new();
    private readonly List<Socket> _listeners = [];
    private readonly Dictionary<AddressFamily, ushort> _ports = [];
    private readonly List<Task> _workers = [];
    private readonly Action<string> _error;
    private readonly object _sendGate = new();
    private readonly object _packetGate = new();
    private IntPtr _handle;
    private long _connectionId;
    private long _lastError;
    public Task<Exception?> Completion => _completion.Task;

    public AppRouteEngine(IEnumerable<AppRouteRule> rules, IEnumerable<int> excludedProcesses, Action<string> error)
    {
        _routing = new(new(rules.ToList(), excludedProcesses), new([], [], _ => RouteDecision.Unresolved, 0));
        _error = error;
        _fragments = new(ClassifyFragment);
    }

    internal RouteDecisionKind ClassifyFragment(RouteFlow flow)
    {
        // Reflected listener packets belong to this process, but must still pass
        // through reverse NAT. Existing selected streams keep their chosen route.
        if (flow.Protocol == 6 && (_ports.Values.Contains(flow.LocalPort) || _nat.Find(flow) != null))
        { return RouteDecisionKind.Selected; }
        return Match(flow, 0, false).Kind;
    }

    public async Task ApplyAsync(IReadOnlyList<AppRouteRule> rules, IEnumerable<int> excludedProcesses, CancellationToken token)
    {
        var policy = new RoutePolicy(rules, excludedProcesses);
        var owners = await Task.Run(() => _attribution.Read(policy));
        lock (_packetGate)
        {
            _stop.Token.ThrowIfCancellationRequested();
            token.ThrowIfCancellationRequested();
            Volatile.Write(ref _routing, new(policy, owners));
            _fragments.ClearDecisions();
            _nat.Retain(policy);
            foreach (var pair in _udp.Where(p => !policy.Retains(p.Value.Rule)).ToArray())
            {
                if (_udp.TryRemove(pair))
                { pair.Value.Dispose(); }
            }
        }
    }

    public void Start()
    {
        foreach (var family in new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 })
        {
            if (family == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
            {
                continue;
            }

            var listener = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
            _listeners.Add(listener);
            if (family == AddressFamily.InterNetworkV6)
            {
                listener.DualMode = false;
            }

            listener.Bind(new IPEndPoint(family == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0));
            listener.Listen(128);
            _ports[family] = (ushort)((IPEndPoint)listener.LocalEndPoint!).Port;
        }
        // NETWORK has no PID field; resolve new flows against the complete Windows owner tuple.
        // Include non-initial fragments, which have no transport header for the tcp/udp filter.
        _handle = WinDivertApi.WinDivertOpen("outbound and !loopback and (tcp or udp or fragment)", 0, 100, 0);
        if (_handle == IntPtr.Zero || _handle == new IntPtr(-1))
        {
            _handle = IntPtr.Zero;
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        foreach (var listener in _listeners)
        {
            _workers.Add(Accept(listener.AcceptAsync));
        }

        _workers.Add(Task.Factory.StartNew(Capture, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
        _workers.Add(Cleanup());
        _workers.Add(Task.Run(WatchAttribution));
    }

    private async Task WatchAttribution()
    {
        var lastRead = Environment.TickCount64;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _refreshRequest.WaitAsync(100, _stop.Token);
                // Coalesce bursts of new flows instead of letting unknown packets drive a busy loop.
                var delay = 25 - (Environment.TickCount64 - lastRead);
                if (delay > 0) { await Task.Delay((int)delay, _stop.Token); }
                Routing previous;
                lock (_packetGate)
                { previous = _routing; }
                var snapshot = _attribution.Read(previous.Policy);
                lastRead = Environment.TickCount64;
                lock (_packetGate)
                {
                    if (!ReferenceEquals(previous.Policy, _routing.Policy))
                    { continue; }
                    Volatile.Write(ref _routing, new(previous.Policy, snapshot));
                    foreach (var packet in _pending.Drain())
                    {
                        try
                        { Process(packet.Bytes, packet.Bytes.Length, packet.Address, packet.Fragments, packet.Arrived); }
                        catch (Exception ex) { Report(ex); }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { Fail(ex); }
    }

    private void RequestRefresh()
    {
        if (_refreshRequest.CurrentCount == 0)
        {
            try
            { _refreshRequest.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    private RouteDecision Match(RouteFlow flow, long arrived, bool fresh)
    {
        var snapshot = Volatile.Read(ref _routing).Owners;
        if (Environment.TickCount64 - snapshot.ReadAt > 500 || fresh && snapshot.ReadAt < arrived)
        {
            return RouteDecision.Unresolved;
        }
        return snapshot.Find(flow);
    }
    private void Capture()
    {
        var bytes = new byte[65575];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                if (!WinDivertApi.WinDivertRecv(_handle, bytes, (uint)bytes.Length, out var count, out var address))
                {
                    if (_stop.IsCancellationRequested)
                    {
                        break;
                    }

                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                try
                {
                    lock (_packetGate)
                    {
                        if (_fragments.Add(bytes.AsSpan(0, checked((int)count)), address, out var batch))
                        {
                            if (batch != null)
                            {
                                if (batch.PassThrough)
                                {
                                    foreach (var original in batch.Originals)
                                    { Send(original.Packet, original.Packet.Length, original.Address, false); }
                                }
                                else
                                { Process(batch.Packet, batch.Packet.Length, batch.Address, batch.Originals); }
                            }
                        }
                        else
                        {
                            Process(bytes, checked((int)count), address);
                        }
                    }
                }
                catch (Exception ex) { Report(ex); } // An attributed packet never falls back to a direct send on failure.
            }
        }
        catch (Exception ex) { Fail(ex); }
        finally
        {
            // Never leave an unserviced capture handle blocking unrelated applications.
            lock (_sendGate)
            {
                if (_handle != IntPtr.Zero)
                {
                    WinDivertApi.WinDivertClose(_handle);
                }

                _handle = IntPtr.Zero;
            }
        }
    }

    private void Process(byte[] bytes, int count, DivertAddress address,
        List<(byte[] Packet, DivertAddress Address)>? fragments = null, long? arrival = null)
    {
        var arrived = arrival ?? Environment.TickCount64;
        bool Defer(RouteDecision decision)
        {
            if (decision.Kind is not (RouteDecisionKind.Unresolved or RouteDecisionKind.Ambiguous))
            { return false; }
            if (decision.Kind == RouteDecisionKind.Ambiguous)
            { throw new IOException("Ambiguous shared endpoint; packet blocked."); }
            if (Environment.TickCount64 - arrived >= RoutePendingPackets.WaitMilliseconds ||
                !_pending.Add(bytes.AsSpan(0, count), address, arrived, fragments))
            {
                throw new IOException("Could not identify the application owning a packet; packet blocked.");
            }
            if (!arrival.HasValue)
            { RequestRefresh(); }
            return true;
        }
        void PassThrough()
        {
            if (fragments == null)
            {
                Send(bytes, count, address, false);
            }
            else
            {
                foreach (var fragment in fragments)
                {
                    Send(fragment.Packet, fragment.Packet.Length, fragment.Address, false);
                }
            }
        }
        var packet = RoutePacket.Parse(bytes.AsSpan(0, count), address.InterfaceIndex);
        if (packet == null)
        {
            PassThrough();
            return;
        }
        var flow = packet.Flow;
        if (flow.Protocol == 6 && _ports.TryGetValue(flow.LocalAddress.AddressFamily, out var listenerPort))
        {
            if (flow.LocalPort == listenerPort)
            {
                var reverse = _nat.Reverse(flow.LocalAddress, flow.RemoteAddress, flow.RemotePort);
                if (reverse == null)
                {
                    return; // Never let a reflected response escape onto the physical network.
                }

                reverse.LastActivity = Environment.TickCount64;
                packet.Rewrite(bytes, reverse.Flow.RemoteAddress, reverse.Flow.RemotePort, reverse.Flow.LocalAddress, reverse.Flow.LocalPort);
                address.InterfaceIndex = reverse.OriginalAddress.InterfaceIndex;
                address.SubInterfaceIndex = reverse.OriginalAddress.SubInterfaceIndex;
                address.Outbound = false;
                Send(bytes, count, address, true);
                return;
            }
            var entry = _nat.Find(flow, packet.IsTcpSyn ? packet.TcpSequence : null);
            if (entry == null)
            {
                // A new connection can reuse a closed tuple. Require a snapshot
                // begun after its SYN before choosing the route for the stream.
                var match = Match(flow, arrived, packet.IsTcpSyn);
                if (Defer(match))
                { return; }
                if (match.Kind == RouteDecisionKind.Unselected)
                {
                    PassThrough();
                    return;
                }
                // Existing connections need an application reconnect; do not leak their remaining packets.
                if (!packet.IsTcpSyn)
                {
                    return;
                }

                entry = _nat.GetOrAdd(flow, match.Rule!, packet.TcpSequence);
                entry.OriginalAddress = address;
            }
            entry.LastActivity = Environment.TickCount64;
            packet.Rewrite(bytes, flow.RemoteAddress, entry.TranslatedPort, flow.LocalAddress, listenerPort);
            address.Outbound = false;
            Send(bytes, count, address, true);
            return;
        }
        if (flow.Protocol == 17)
        {
            var match = Match(flow, arrived, false);
            if (Defer(match))
            { return; }
            if (match.Kind == RouteDecisionKind.Unselected)
            { PassThrough(); return; }
            var key = new UdpEndpoint(match.Process!.Value, flow.LocalAddress, flow.LocalPort, match.Rule!.Id);
            _udp.TryGetValue(key, out var session);
            if (session != null && !session.IsUsable)
            {
                if (_udp.TryRemove(key, out var expired))
                {
                    expired.Dispose();
                }

                session = null;
            }
            if (session == null)
            {
                if (_udp.Count >= 2048)
                {
                    throw new IOException("UDP routing session limit reached.");
                }

                var replyAddress = address;
                replyAddress.Outbound = false;
                var owner = new RouteFlowOwner(match.Process!.Value, () =>
                {
                    var current = Match(flow, 0, false);
                    return current.Kind == RouteDecisionKind.Selected && current.Rule?.Id == match.Rule!.Id ? current.Process : null;
                });
                session = new(match.Rule!, new(flow.RemoteAddress, flow.RemotePort),
                    (peer, payload) =>
                    {
                        var reply = RoutePacket.CreateUdpReply(flow with { RemoteAddress = peer.Address, RemotePort = (ushort)peer.Port }, payload);
                        Send(reply, reply.Length, replyAddress, true);
                    },
                    _stop.Token, Report, owner.IsCurrent);
                _udp[key] = session;
                var id = Interlocked.Increment(ref _connectionId);
                _sessions[id] = session.Completion;
                _ = session.Completion.ContinueWith(_ => { _sessions.TryRemove(id, out var ignored); }, TaskScheduler.Default);
            }
            var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(packet.TransportOffset + 4)) - 8;
            session.Send(new(flow.RemoteAddress, flow.RemotePort), bytes.AsSpan(packet.TransportOffset + 8, payloadLength));
            return;
        }
        PassThrough();
    }

    private void Send(byte[] bytes, int count, DivertAddress address, bool checksum)
    {
        lock (_sendGate)
        {
            if (_handle == IntPtr.Zero || _stop.IsCancellationRequested)
            {
                return;
            }

            if (checksum && !WinDivertApi.WinDivertHelperCalcChecksums(bytes, (uint)count, ref address, 0))
            {
                throw new IOException("Could not calculate redirected packet checksums.");
            }

            if (!WinDivertApi.WinDivertSend(_handle, bytes, (uint)count, out _, ref address))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    internal async Task Accept(Func<CancellationToken, ValueTask<Socket>> accept)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                Socket? client = null;
                try
                {
                    client = await accept(_stop.Token);
                    var local = (IPEndPoint)client.LocalEndPoint!;
                    var remote = (IPEndPoint)client.RemoteEndPoint!;
                    var entry = _nat.Reverse(local.Address, remote.Address, (ushort)remote.Port);
                    if (entry == null || entry.Closed || entry.Accepted || _connections.Count >= 2048)
                    {
                        continue;
                    }
                    entry.Accepted = true;
                    var id = Interlocked.Increment(ref _connectionId);
                    var task = Relay(client, entry);
                    client = null; // Relay now owns the accepted socket.
                    _connections[id] = task;
                    _ = task.ContinueWith(_ => { _connections.TryRemove(id, out var ignored); }, TaskScheduler.Default);
                }
                // A peer can cancel while its connection is still in the accept queue.
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted) { }
                finally { client?.Dispose(); }
            }
        }
        catch (Exception ex) when (_stop.IsCancellationRequested && ex is OperationCanceledException or ObjectDisposedException) { }
        catch (Exception ex) { Fail(ex); }
    }

    private async Task Relay(Socket client, RouteNatEntry entry)
    {
        using (client)
        using (var timeout = entry.BeginRelay(_stop.Token))
        {
            try
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var outbound = await RouteConnector.ConnectTcp(entry.Rule, new(entry.Flow.RemoteAddress, entry.Flow.RemotePort), timeout.Token);
                timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                using var input = new NetworkStream(client, false);
                using var output = new NetworkStream(outbound, false);
                async Task Copy(NetworkStream source, NetworkStream target, Socket targetSocket)
                {
                    try
                    {
                        await source.CopyToAsync(target, timeout.Token);
                        targetSocket.Shutdown(SocketShutdown.Send);
                    }
                    catch { client.Dispose(); outbound.Dispose(); throw; }
                }
                await Task.WhenAll(Copy(input, output, outbound), Copy(output, input, client));
            }
            catch (OperationCanceledException) when (!_stop.IsCancellationRequested && !entry.Closed) { Report(new TimeoutException("Application route connection timed out.")); }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
            catch (Exception ex) { Report(ex); }
            finally { entry.Closed = true; entry.LastActivity = Environment.TickCount64; }
        }
    }

    private async Task Cleanup()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                _nat.Expire(Environment.TickCount64);
                foreach (var pair in _udp)
                {
                    if (Environment.TickCount64 - pair.Value.LastActivity > 60_000 || pair.Value.Completion.IsCompleted)
                    {
                        // The capture thread may have replaced this completed session already.
                        if (_udp.TryRemove(pair))
                        {
                            pair.Value.Dispose();
                            await pair.Value.Completion;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        Stop(ex);
        _error("Application routing stopped: " + ex.Message);
    }

    private void Report(Exception ex)
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastError) < 1000)
        {
            return;
        }

        Interlocked.Exchange(ref _lastError, now);
        _error(ex.Message);
    }

    private void StopCapture()
    {
        // Capture closes the handle under this same lock; never shut down a stale handle.
        lock (_sendGate)
        {
            if (_handle != IntPtr.Zero)
            {
                WinDivertApi.WinDivertShutdown(_handle, 1);
            }
        }
    }

    private void Stop(Exception? error = null)
    {
        _completion.TrySetResult(error);
        _stop.Cancel();
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        StopCapture();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        foreach (var session in _udp.Values)
        {
            session.Dispose();
        }

        try
        {
            // Stop producers first: an accept/capture iteration already in flight
            // may still register a connection after cancellation was requested.
            await Task.WhenAll(_workers);
        }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        try
        {
            await Task.WhenAll(_connections.Values.Concat(_sessions.Values));
        }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        lock (_sendGate)
        {
            if (_handle != IntPtr.Zero)
            {
                WinDivertApi.WinDivertClose(_handle);
            }
            _handle = IntPtr.Zero;
        }
        _attribution.Dispose();
        _refreshRequest.Dispose();
        _stop.Dispose();
    }
}
