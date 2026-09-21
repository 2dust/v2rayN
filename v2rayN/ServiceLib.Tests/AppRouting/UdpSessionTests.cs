using System.Buffers.Binary;
using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class UdpSessionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OneAssociationCarriesMultiplePeersAndReportsActualReplySource(bool ipv6)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        tcp.Start();
        var first = new IPEndPoint(PacketTests.Flow(ipv6).RemoteAddress, 1234);
        var second = new IPEndPoint(first.Address, 5678);
        var replyingPeer = new IPEndPoint(first.Address, 9999);
        var received = new TaskCompletionSource<IPEndPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var client = await tcp.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            await stream.ReadExactlyAsync(new byte[3], timeout.Token);
            await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
            await stream.ReadExactlyAsync(new byte[10], timeout.Token);
            await stream.WriteAsync(new byte[] { 5, 0, 0 }.Concat(RouteConnector.EncodeAddress((IPEndPoint)udp.Client.LocalEndPoint!)).ToArray(), timeout.Token);
            var one = await udp.ReceiveAsync(timeout.Token);
            var two = await udp.ReceiveAsync(timeout.Token);
            await one.RemoteEndPoint.Should().BeEqualTo(two.RemoteEndPoint);
            var offset = RouteConnector.UnwrapDatagram(one.Buffer, first);
            await one.Buffer[offset].Should().BeEqualTo((byte)1);
            offset = RouteConnector.UnwrapDatagram(two.Buffer, second);
            await two.Buffer[offset].Should().BeEqualTo((byte)2);
            await udp.SendAsync(RouteConnector.WrapDatagram(replyingPeer, new byte[] { 3 }), one.RemoteEndPoint, timeout.Token);
            await (await stream.ReadAsync(new byte[1], timeout.Token)).Should().BeEqualTo(0);
        });
        using var session = new RouteUdpSession(new() { Kind = AppRouteKind.Socks5, SocksPort = ((IPEndPoint)tcp.LocalEndpoint).Port }, first,
            (peer, _) => received.TrySetResult(peer), timeout.Token, ex => received.TrySetException(ex), () => true);
        try
        {
            session.Send(first, [1]);
            session.Send(second, [2]);
            await (await received.Task.WaitAsync(timeout.Token)).Should().BeEqualTo(replyingPeer);
        }
        finally { session.Dispose(); await session.Completion.WaitAsync(timeout.Token); await server.WaitAsync(timeout.Token); }
    }

    [Test]
    [Arguments(8_000, 8)]
    [Arguments(32_000, 2)]
    public async Task PendingDatagramsHaveAByteBudgetThatIsReleasedAfterSending(int payloadSize, int expectedCount)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        udp.Client.ReceiveBufferSize = 1024 * 1024;
        tcp.Start();
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = new TaskCompletionSource<List<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        var destination = new IPEndPoint(IPAddress.Loopback, 12345);
        var server = Task.Run(async () =>
        {
            using var client = await tcp.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            await stream.ReadExactlyAsync(new byte[3], timeout.Token);
            await queued.Task.WaitAsync(timeout.Token);
            await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
            await stream.ReadExactlyAsync(new byte[10], timeout.Token);
            await stream.WriteAsync(new byte[] { 5, 0, 0 }.Concat(RouteConnector.EncodeAddress((IPEndPoint)udp.Client.LocalEndPoint!)).ToArray(), timeout.Token);
            var received = new List<byte>();
            while (true)
            {
                var packet = await udp.ReceiveAsync(timeout.Token);
                var offset = RouteConnector.UnwrapDatagram(packet.Buffer, destination);
                if (packet.Buffer[offset] == 255)
                {
                    break;
                }
                received.Add(packet.Buffer[offset]);
            }
            drained.SetResult(received);
            var next = await udp.ReceiveAsync(timeout.Token);
            resumed.SetResult(next.Buffer[RouteConnector.UnwrapDatagram(next.Buffer, destination)]);
            await (await stream.ReadAsync(new byte[1], timeout.Token)).Should().BeEqualTo(0);
        });
        using var session = new RouteUdpSession(new()
        {
            Kind = AppRouteKind.Socks5,
            SocksPort = ((IPEndPoint)tcp.LocalEndpoint).Port
        }, destination, (_, _) => { }, timeout.Token, ex => drained.TrySetException(ex), () => true);
        try
        {
            for (byte index = 0; index < 8; index++)
            {
                var payload = new byte[payloadSize];
                payload[0] = index;
                session.Send(destination, payload);
            }
            session.Send(destination, [255]); // Fits the remaining byte budget; marks the end of this burst.
            queued.SetResult();
            var received = await drained.Task.WaitAsync(timeout.Token);
            var next = new byte[payloadSize];
            next[0] = 42;
            session.Send(destination, next);
            await (await resumed.Task.WaitAsync(timeout.Token)).Should().BeEqualTo((byte)42);
            await received.SequenceEqual(Enumerable.Range(0, expectedCount).Select(i => (byte)i)).Should().BeTrue();
        }
        finally
        {
            session.Dispose();
            await session.Completion.WaitAsync(timeout.Token);
            await server.WaitAsync(timeout.Token);
        }
    }

    [Test]
    public async Task PacketsQueuedDuringConnectionAreDroppedWhenTheirOwnerExits()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        tcp.Start();
        var ownsFlow = 1;
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var client = await tcp.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            var greeting = new byte[3];
            await stream.ReadExactlyAsync(greeting, timeout.Token);
            await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
            var header = new byte[4];
            await stream.ReadExactlyAsync(header, timeout.Token);
            var address = new byte[(header[3] == 1 ? 4 : 16) + 2];
            await stream.ReadExactlyAsync(address, timeout.Token);
            await queued.Task.WaitAsync(timeout.Token);
            Volatile.Write(ref ownsFlow, 0);
            await stream.WriteAsync(new byte[] { 5, 0, 0 }.Concat(RouteConnector.EncodeAddress((IPEndPoint)udp.Client.LocalEndPoint!)).ToArray(), timeout.Token);
            await (await stream.ReadAsync(new byte[1], timeout.Token)).Should().BeEqualTo(0);
        });
        var errors = new List<Exception>();
        using var session = new RouteUdpSession(new()
        {
            Kind = AppRouteKind.Socks5,
            SocksPort = ((IPEndPoint)tcp.LocalEndpoint).Port
        }, new(IPAddress.Loopback, 12345), (_, _) => { }, timeout.Token, errors.Add, () => Volatile.Read(ref ownsFlow) != 0);
        session.Send(new(IPAddress.Loopback, 12345), [1, 2, 3]);
        queued.SetResult();
        await session.Completion.WaitAsync(timeout.Token);
        await server;
        await udp.Available.Should().BeEqualTo(0);
        await errors.Count.Should().BeEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SessionOwnershipExpiresForDifferentProcessOrReusedPid(bool reusedPid)
    {
        var owner = new RouteProcessKey(10, 100);
        RouteProcessKey? current = owner;
        var reads = 0;
        var lifetime = new RouteFlowOwner(owner, () => { reads++; return current; });
        await lifetime.IsCurrent().Should().BeTrue();
        current = reusedPid ? new(10, 200) : new(20, 100);
        await lifetime.IsCurrent().Should().BeFalse();
        current = owner;
        await lifetime.IsCurrent().Should().BeFalse();
        await reads.Should().BeEqualTo(2);
    }

    [Test]
    public async Task SessionOwnershipLossAlsoCoversClosedOrSharedEndpoints()
    {
        var lifetime = new RouteFlowOwner(new(10, 100), () => null);
        await lifetime.IsCurrent().Should().BeFalse();
    }

    [Test]
    public async Task FailedAssociationIsImmediatelyUnusable()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try
        {
            var server = Task.Run(async () =>
            {
                using var client = await tcp.AcceptTcpClientAsync(timeout.Token);
                using var stream = client.GetStream();
                var greeting = new byte[3];
                await stream.ReadExactlyAsync(greeting, timeout.Token);
                await stream.WriteAsync(new byte[] { 5, 255 }, timeout.Token);
            });
            var errors = new List<Exception>();
            using var session = new RouteUdpSession(new()
            {
                Kind = AppRouteKind.Socks5,
                SocksPort = ((IPEndPoint)tcp.LocalEndpoint).Port
            },
                new(IPAddress.Loopback, 12345), (_, _) => { }, timeout.Token, errors.Add, () => true);
            await session.Completion.WaitAsync(timeout.Token);
            await session.IsUsable.Should().BeFalse();
            await errors.Count.Should().BeEqualTo(1);
            await server;
        }
        finally { tcp.Stop(); }
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
    public async Task UdpAssociateOnlyDeliversRepliesToItsCurrentOwner(bool ipv6Destination, bool loseOwnership, bool domainRelay)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            var payload = new byte[] { 9, 8, 7, 0, 255 };
            var destination = new IPEndPoint(PacketTests.Flow(ipv6Destination).RemoteAddress, 443);
            var done = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var controlClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownsFlow = 1;
            var server = Task.Run(async () =>
            {
                using var client = await tcp.AcceptTcpClientAsync(timeout.Token);
                using var stream = client.GetStream();
                var greeting = new byte[3];
                await stream.ReadExactlyAsync(greeting, timeout.Token);
                await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
                var header = new byte[4];
                await stream.ReadExactlyAsync(header, timeout.Token);
                await header[1].Should().BeEqualTo((byte)3);
                var requestAddress = new byte[(header[3] == 1 ? 4 : 16) + 2];
                await stream.ReadExactlyAsync(requestAddress, timeout.Token);
                await (BinaryPrimitives.ReadUInt16BigEndian(requestAddress.AsSpan(requestAddress.Length - 2)) > 0).Should().BeTrue();
                var boundAddress = RouteConnector.EncodeAddress((IPEndPoint)udp.Client.LocalEndPoint!);
                if (domainRelay)
                {
                    var name = Encoding.ASCII.GetBytes("localhost");
                    boundAddress = new byte[] { 3, (byte)name.Length }.Concat(name).Concat(boundAddress.TakeLast(2)).ToArray();
                }
                var reply = new byte[] { 5, 0, 0 }.Concat(boundAddress).ToArray();
                await stream.WriteAsync(reply, timeout.Token);
                var datagram = await udp.ReceiveAsync(timeout.Token);
                var offset = RouteConnector.UnwrapDatagram(datagram.Buffer, destination);
                await datagram.Buffer.AsSpan(offset).SequenceEqual(payload).Should().BeTrue();
                if (loseOwnership)
                {
                    Interlocked.Exchange(ref ownsFlow, 0);
                }
                await udp.SendAsync(datagram.Buffer, datagram.RemoteEndPoint, timeout.Token);
                var one = new byte[1];
                await (await stream.ReadAsync(one, timeout.Token)).Should().BeEqualTo(0);
                controlClosed.SetResult();
            });
            using var session = new RouteUdpSession(new AppRouteRule { Kind = AppRouteKind.Socks5, SocksPort = ((IPEndPoint)tcp.LocalEndpoint).Port },
                destination, (_, data) => done.TrySetResult(data), timeout.Token, ex => done.TrySetException(ex), () => Volatile.Read(ref ownsFlow) != 0);
            session.Send(destination, payload);
            if (loseOwnership)
            {
                await session.Completion.WaitAsync(timeout.Token);
                await done.Task.IsCompleted.Should().BeFalse();
                await session.IsUsable.Should().BeFalse();
            }
            else
            {
                await (await done.Task.WaitAsync(timeout.Token)).SequenceEqual(payload).Should().BeTrue();
                await controlClosed.Task.IsCompleted.Should().BeFalse();
            }
            session.Dispose();
            await session.Completion.WaitAsync(timeout.Token);
            await server.WaitAsync(timeout.Token);
            await controlClosed.Task.IsCompleted.Should().BeTrue();
        }
        finally { tcp.Stop(); }
    }

    [Test]
    public async Task OwnerMatchingIncludesRemoteEndpointAndIpVersion()
    {
        var flow = PacketTests.Flow(false) with
        {
            Protocol = 6
        };
        var rows = new[]
        {
            new RouteOwnerTable.Row(flow.LocalAddress, flow.LocalPort, IPAddress.Parse("203.0.113.7"), flow.RemotePort, 100),
            new RouteOwnerTable.Row(flow.LocalAddress, flow.LocalPort, flow.RemoteAddress, flow.RemotePort, 200),
            new RouteOwnerTable.Row(IPAddress.IPv6Any, flow.LocalPort, flow.RemoteAddress, flow.RemotePort, 300)
        };
        var snapshot = new RouteAttributionSnapshot(rows, [], pid => new(RouteDecisionKind.Selected, new(pid, 0), new()), 0);
        await snapshot.Find(flow).Process!.Value.Pid.Should().BeEqualTo(200);
    }

    [Test]
    public async Task MissingInterfaceDoesNotFallbackToDefaultRoute()
    {
        var rejected = false;
        try
        {
            using var socket = RouteConnector.CreateInterfaceSocket(new AppRouteRule { Kind = AppRouteKind.Interface, InterfaceId = Guid.NewGuid().ToString() },
                new(IPAddress.Loopback, 9), ProtocolType.Udp);
        }
        catch (IOException) { rejected = true; }
        await rejected.Should().BeTrue();
    }
}
