namespace ServiceLib.Tests.Manager;

public class EndpointResolveManagerTests
{
    private static readonly IPAddress ExpectedAddress = IPAddress.Parse("203.0.113.7");
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(20);

    [Test]
    public async Task TcpDnsResolver_ResolveAsync_SendsLengthPrefixedQuery()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverTask = ServeOneTcpQueryAsync(listener);

        var resolver = new EndpointResolveManager.TcpDnsResolver((IPEndPoint)listener.LocalEndpoint);
        var addresses = await resolver.ResolveAsync("example.com", false).WaitAsync(TestTimeout);

        await addresses.Should().Contain(ExpectedAddress);
        await serverTask.WaitAsync(TestTimeout);
    }

    [Test]
    public async Task TcpDnsResolver_TryResolveIpv46Async_UnansweredQueryTimesOut()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var acceptTask = listener.AcceptTcpClientAsync();

        EndpointResolveManager.IDnsResolver resolver = new EndpointResolveManager.TcpDnsResolver(
            (IPEndPoint)listener.LocalEndpoint, TimeSpan.FromMilliseconds(200));
        var addresses = await resolver.TryResolveIpv46Async("example.com").WaitAsync(TestTimeout);

        await addresses.Should().BeEmpty();
        using var acceptedClient = await acceptTask.WaitAsync(TestTimeout);
    }

    [Test]
    public async Task UdpDnsResolver_TryResolveIpv46Async_UnansweredQueryTimesOut()
    {
        using var silentServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        EndpointResolveManager.IDnsResolver resolver = new EndpointResolveManager.UdpDnsResolver(
            (IPEndPoint)silentServer.Client.LocalEndPoint!, TimeSpan.FromMilliseconds(200));
        var addresses = await resolver.TryResolveIpv46Async("example.com").WaitAsync(TestTimeout);

        await addresses.Should().BeEmpty();
    }

    /// <summary>
    ///     Accepts one connection and answers one RFC 1035 length-prefixed query with an A record.
    /// </summary>
    private static async Task ServeOneTcpQueryAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();

        var lengthBuffer = new byte[2];
        await stream.ReadExactlyAsync(lengthBuffer);
        var query = new byte[(lengthBuffer[0] << 8) | lengthBuffer[1]];
        await stream.ReadExactlyAsync(query);

        var response = new List<byte>();
        response.AddRange(query[..2]); // ID
        response.AddRange([0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00]);
        response.AddRange(query[12..]); // Question
        // Answer: name pointer to the question, type A, class IN, TTL 60, 4-byte address.
        response.AddRange([0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04]);
        response.AddRange(ExpectedAddress.GetAddressBytes());

        await stream.WriteAsync(new[] { (byte)(response.Count >> 8), (byte)(response.Count & 0xFF) });
        await stream.WriteAsync(response.ToArray());
    }
}
