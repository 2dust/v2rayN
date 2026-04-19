using ServiceLib.UdpTest.Tester;

namespace ServiceLib.UdpTest;

public class UdpTestService
{
    private const string DefaultUdpTestType = "ntp";
    private readonly IUdpTest _udpTest;

    private static readonly IReadOnlyDictionary<string, Func<IUdpTest>> UdpTestFactories =
        new Dictionary<string, Func<IUdpTest>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ntp"] = () => new NtpService(),
            ["dns"] = () => new DnsService(),
            ["stun"] = () => new StunService(),
            ["mcbe"] = () => new McBeService(),
        };

    private UdpTestService(IUdpTest udpTest)
    {
        _udpTest = udpTest;
    }

    public static UdpTestService Create(string? udpTestType)
    {
        if (string.IsNullOrEmpty(udpTestType))
        {
            return new UdpTestService(UdpTestFactories[DefaultUdpTestType]());
        }

        return UdpTestFactories.TryGetValue(udpTestType, out var factory)
            ? new UdpTestService(factory())
            : new UdpTestService(UdpTestFactories[DefaultUdpTestType]());
    }

    public static UdpTestService CreateFromTarget(string? udpTestTarget, out string targetServerHost)
    {
        var parts = udpTestTarget?.Split(':', 2);
        var udpTestType = parts?.Length > 0 ? parts[0] : DefaultUdpTestType;

        var udpService = Create(udpTestType);
        targetServerHost = parts?.Length > 1 && !string.IsNullOrEmpty(parts[1])
            ? parts[1]
            : udpService._udpTest.GetDefaultTargetHost();

        return udpService;
    }

    private (string host, ushort port) ParseHostAndPort(string targetServerHost)
    {
        if (string.IsNullOrEmpty(targetServerHost))
        {
            return (_udpTest.GetDefaultTargetHost(), _udpTest.GetDefaultTargetPort());
        }

        // Handle IPv6 format: [::1]:port or [2001:db8::1]:port
        if (targetServerHost.StartsWith('['))
        {
            var closeBracketIndex = targetServerHost.IndexOf(']');
            if (closeBracketIndex > 0)
            {
                var host = targetServerHost.Substring(1, closeBracketIndex - 1);
                if (closeBracketIndex < targetServerHost.Length - 1 && targetServerHost[closeBracketIndex + 1] == ':')
                {
                    var portStr = targetServerHost.Substring(closeBracketIndex + 2);
                    if (ushort.TryParse(portStr, out var port))
                    {
                        return (host, port);
                    }
                }
                return (host, _udpTest.GetDefaultTargetPort());
            }
        }

        // Handle IPv4 or domain format: 1.1.1.1:53 or exam.com:333
        var lastColonIndex = targetServerHost.LastIndexOf(':');
        if (lastColonIndex > 0)
        {
            var host = targetServerHost.Substring(0, lastColonIndex);
            var portStr = targetServerHost.Substring(lastColonIndex + 1);
            if (ushort.TryParse(portStr, out var port))
            {
                return (host, port);
            }
        }

        // No port specified, use default
        return (targetServerHost, _udpTest.GetDefaultTargetPort());
    }

    public async Task<TimeSpan> SendUdpRequestAsync(string targetServerHost, int socks5Port, TimeSpan operationTimeout)
    {
        using var cts = new CancellationTokenSource(operationTimeout);
        var cancellationToken = cts.Token;
        var udpRequestPacket = _udpTest.BuildUdpRequestPacket();
        if (udpRequestPacket == null || udpRequestPacket.Length == 0)
        {
            throw new InvalidOperationException("Failed to build UDP request packet.");
        }
        using var channel = new Socks5UdpChannel("127.0.0.1", socks5Port);
        if (!await channel.EstablishUdpAssociationAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new Exception("Failed to establish UDP association with SOCKS5 proxy.");
        }

        var (targetHost, targetPort) = ParseHostAndPort(targetServerHost);

        byte[] udpReceiveResult = null;

        // Get minimum round trip time from two attempts
        var roundTripTime = TimeSpan.MaxValue;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                await channel.SendAsync(targetHost, targetPort, udpRequestPacket).ConfigureAwait(false);
                var (_, receiveResult) = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                udpReceiveResult = receiveResult;

                var currentRoundTripTime = stopwatch.Elapsed;
                if (currentRoundTripTime < roundTripTime)
                {
                    roundTripTime = currentRoundTripTime;
                }
            }
            catch
            {
                if (attempt == 1 && roundTripTime == TimeSpan.MaxValue)
                {
                    throw;
                }
            }
        }

        if ((udpReceiveResult?.Length ?? 0) < 4 + 1 + 4 + 2)
        {
            throw new Exception("Received NTP response is too short.");
        }

        if (udpReceiveResult != null && _udpTest.VerifyAndExtractUdpResponse(udpReceiveResult))
        {
            return roundTripTime;
        }
        else
        {
            throw new Exception("Failed to verify and extract UDP response.");
        }
    }
}
