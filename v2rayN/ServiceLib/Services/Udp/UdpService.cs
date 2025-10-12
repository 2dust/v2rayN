using System.Diagnostics;

namespace ServiceLib.Services.Udp;
public abstract class UdpService
{
    protected abstract byte[] BuildUdpRequestPacket();
    protected abstract bool VerifyAndExtractUdpResponse(byte[] udpResponseBytes);
    protected abstract ushort GetDefaultTargetPort();
    protected abstract string GetDefaultTargetHost();

    private (string host, ushort port) ParseHostAndPort(string targetServerHost)
    {
        if (targetServerHost.IsNullOrEmpty())
        {
            return (GetDefaultTargetHost(), GetDefaultTargetPort());
        }

        // Handle IPv6 format: [::1]:port or [2001:db8::1]:port
        if (targetServerHost.StartsWith("["))
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
                return (host, GetDefaultTargetPort());
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
        return (targetServerHost, GetDefaultTargetPort());
    }

    public async Task<TimeSpan> SendUdpRequestAsync(string targetServerHost, int socks5Port, TimeSpan operationTimeout)
    {
        using var cts = new CancellationTokenSource(operationTimeout);
        var cancellationToken = cts.Token;
        var udpRequestPacket = BuildUdpRequestPacket();
        if (udpRequestPacket == null || udpRequestPacket.Length == 0)
        {
            throw new InvalidOperationException("Failed to build UDP request packet.");
        }
        using var channel = new Socks5UdpChannel(Global.Loopback, socks5Port);
        try
        {
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

            if (VerifyAndExtractUdpResponse(udpReceiveResult))
            {
                return roundTripTime;
            }
            else
            {
                throw new Exception("Failed to verify and extract UDP response.");
            }
        }
        catch
        {
            throw;
        }
    }
}
