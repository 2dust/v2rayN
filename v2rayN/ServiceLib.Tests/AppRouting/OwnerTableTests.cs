using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class OwnerTableTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DualStackUdpSocketIsFoundForItsIpv4Packets(bool connected)
    {
        if (!OperatingSystem.IsWindows() || !Socket.OSSupportsIPv6)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp) { DualMode = true };
        client.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
        var port = (ushort)((IPEndPoint)client.LocalEndPoint!).Port;
        var remotePort = (ushort)((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var destination = new IPEndPoint(IPAddress.Loopback.MapToIPv6(), remotePort);
        if (connected)
        {
            await client.ConnectAsync(destination, timeout.Token);
            await client.SendAsync(new byte[] { 1 }, SocketFlags.None, timeout.Token);
        }
        else
        {
            await client.SendToAsync(new byte[] { 1 }, SocketFlags.None, destination, timeout.Token);
        }
        await server.ReceiveAsync(timeout.Token);
        var flow = new RouteFlow(17, IPAddress.Loopback, port, IPAddress.Loopback, remotePort);
        var snapshot = new RouteAttributionSnapshot([], RouteOwnerTable.Read(17, AddressFamily.InterNetwork),
            pid => new(RouteDecisionKind.Unselected, new(pid, 0)), 0);
        await snapshot.Find(flow).Process!.Value.Pid.Should().BeEqualTo(Environment.ProcessId);
    }

    [Test]
    public async Task DualStackTcpSocketIsFoundForItsIpv4Packets()
    {
        if (!OperatingSystem.IsWindows() || !Socket.OSSupportsIPv6)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var remotePort = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { DualMode = true };
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback.MapToIPv6(), remotePort), timeout.Token);
        using var server = await listener.AcceptSocketAsync(timeout.Token);
        var port = (ushort)((IPEndPoint)client.LocalEndPoint!).Port;
        var flow = new RouteFlow(6, IPAddress.Loopback, port, IPAddress.Loopback, remotePort);
        var snapshot = new RouteAttributionSnapshot(RouteOwnerTable.Read(6, AddressFamily.InterNetwork), [],
            pid => new(RouteDecisionKind.Unselected, new(pid, 0)), 0);
        await snapshot.Find(flow).Process!.Value.Pid.Should().BeEqualTo(Environment.ProcessId);
    }
}
