using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class SocksTests
{
    [Test]
    [Arguments(1, false)]
    [Arguments(1, true)]
    [Arguments(3, false)]
    [Arguments(3, true)]
    public async Task ActiveProfileUsesCurrentMainListenerForTcpAndUdpWithoutStaleSocksCredentials(int command, bool ipv6)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = new TcpListener(IPAddress.Loopback, 0);
        first.Start();
        var second = new TcpListener(IPAddress.Loopback, 0);
        second.Start();
        try
        {
            var rule = new AppRouteRule
            {
                Kind = AppRouteKind.ActiveProfile,
                ExecutablePath = "App.exe",
                MatchByName = true,
                SocksHost = "stale.invalid",
                SocksPort = 1,
                SocksUsername = "old-user",
                SocksPassword = "old-password"
            };
            AppRoutingManager.Validate([rule]);
            var destination = new IPEndPoint(PacketTests.Flow(ipv6).RemoteAddress, 443);
            foreach (var listener in new[] { first, second })
            {
                var server = Task.Run(async () =>
                {
                    using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                    using var stream = client.GetStream();
                    var greeting = new byte[3];
                    await stream.ReadExactlyAsync(greeting, timeout.Token);
                    await greeting.SequenceEqual(new byte[] { 5, 1, 0 }).Should().BeTrue();
                    await stream.WriteAsync(new byte[] { 5, 0 }, timeout.Token);
                    var expected = new byte[] { 5, (byte)command, 0 }.Concat(RouteConnector.EncodeAddress(destination)).ToArray();
                    var request = new byte[expected.Length];
                    await stream.ReadExactlyAsync(request, timeout.Token);
                    await request.SequenceEqual(expected).Should().BeTrue();
                    await stream.WriteAsync(new byte[] { 5, 0, 0 }.Concat(RouteConnector.EncodeAddress(destination)).ToArray(), timeout.Token);
                });
                using var socket = await RouteConnector.ConnectProxy(rule, timeout.Token, ((IPEndPoint)listener.LocalEndpoint).Port);
                var reply = await RouteConnector.Request(socket, (byte)command, destination, timeout.Token);
                await reply.Should().BeEqualTo(destination);
                await server;
            }
            await rule.Kind.Should().BeEqualTo(AppRouteKind.ActiveProfile);
            await rule.SocksHost.Should().BeEqualTo("stale.invalid");
            await rule.SocksPort.Should().BeEqualTo(1);
        }
        finally { first.Stop(); second.Stop(); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DatagramFramesRoundTripAndRejectWrongPeerOrFragments(bool ipv6)
    {
        var flow = PacketTests.Flow(ipv6);
        var endpoint = new IPEndPoint(flow.RemoteAddress, flow.RemotePort);
        var wrapped = RouteConnector.WrapDatagram(endpoint, [1, 2, 3]);
        var offset = RouteConnector.UnwrapDatagram(wrapped, endpoint);
        await wrapped.AsSpan(offset).SequenceEqual(new byte[] { 1, 2, 3 }).Should().BeTrue();
        await RouteConnector.UnwrapDatagram(wrapped, new(endpoint.Address, 444)).Should().BeEqualTo(-1);
        wrapped[2] = 1;
        await RouteConnector.UnwrapDatagram(wrapped, endpoint).Should().BeEqualTo(-1);
        await RouteConnector.UnwrapDatagram([0, 0], endpoint).Should().BeEqualTo(-1);
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, true, true)]
    public async Task TcpHandshakeHandlesSplitRepliesAuthenticationAndIpv6(bool authentication, bool ipv6, bool domainReply)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var destination = new IPEndPoint(PacketTests.Flow(ipv6).RemoteAddress, 443);
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream = client.GetStream();
                var greeting = new byte[3];
                await stream.ReadExactlyAsync(greeting, timeout.Token);
                await greeting.SequenceEqual(new byte[] { 5, 1, authentication ? (byte)2 : (byte)0 }).Should().BeTrue();
                await stream.WriteAsync(new byte[] { 5 }, timeout.Token);
                await Task.Delay(10, timeout.Token);
                await stream.WriteAsync(new byte[] { authentication ? (byte)2 : (byte)0 }, timeout.Token);
                if (authentication)
                {
                    var credentials = new byte[5];
                    await stream.ReadExactlyAsync(credentials, timeout.Token);
                    await credentials.SequenceEqual(new byte[] { 1, 1, (byte)'u', 1, (byte)'p' }).Should().BeTrue();
                    await stream.WriteAsync(new byte[] { 1, 0 }, timeout.Token);
                }
                var expected = new byte[] { 5, 1, 0 }.Concat(RouteConnector.EncodeAddress(destination)).ToArray();
                var request = new byte[expected.Length];
                await stream.ReadExactlyAsync(request, timeout.Token);
                await request.SequenceEqual(expected).Should().BeTrue();
                // CONNECT's bind address describes the server's connection; the client
                // must consume it, but does not need to resolve or connect to it.
                var name = Encoding.ASCII.GetBytes("proxy-bind.invalid");
                var boundAddress = domainReply
                    ? new byte[] { 3, (byte)name.Length }.Concat(name).Concat(new byte[] { 0x30, 0x39 }).ToArray()
                    : RouteConnector.EncodeAddress(new(IPAddress.Loopback, 12345));
                var reply = new byte[] { 5, 0, 0 }.Concat(boundAddress).ToArray();
                foreach (var value in reply)
                {
                    await stream.WriteAsync(new byte[] { value }, timeout.Token);
                }

                await stream.WriteAsync(new byte[] { 42 }, timeout.Token);
            });
            var rule = new AppRouteRule
            {
                Kind = AppRouteKind.Socks5,
                SocksHost = "127.0.0.1",
                SocksPort = ((IPEndPoint)listener.LocalEndpoint).Port,
                SocksUsername = authentication ? "u" : "",
                SocksPassword = authentication ? "p" : ""
            };
            using var socket = await RouteConnector.ConnectTcp(rule, destination, timeout.Token);
            var received = new byte[1];
            using var stream = new NetworkStream(socket, false);
            await stream.ReadExactlyAsync(received, timeout.Token);
            await received[0].Should().BeEqualTo((byte)42);
            await server;
        }
        finally { listener.Stop(); }
    }

    [Test]
    public async Task RejectedSocksMethodDoesNotFallbackToDirect()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream = client.GetStream();
                var greeting = new byte[3];
                await stream.ReadExactlyAsync(greeting, timeout.Token);
                await stream.WriteAsync(new byte[] { 5, 255 }, timeout.Token);
            });
            var rejected = false;
            try
            {
                using var socket = await RouteConnector.ConnectTcp(new AppRouteRule
                {
                    Kind = AppRouteKind.Socks5,
                    SocksPort = ((IPEndPoint)listener.LocalEndpoint).Port
                }, new(IPAddress.Loopback, 9), timeout.Token);
            }
            catch (IOException) { rejected = true; }
            await rejected.Should().BeTrue();
            await server;
        }
        finally { listener.Stop(); }
    }
}
