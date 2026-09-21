using System.Security.Cryptography;

namespace ServiceLib.Common;

public static class MacOSLocalNetworkPrivacy
{
    public static void TriggerPrompt()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        foreach (var address in GetLinkLocalAddresses())
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(new IPEndPoint(RandomizeHost(address), 9));
            }
            catch
            {
                // This is a best-effort privacy prompt trigger. A denied or
                // unreachable connection is expected and needs no handling.
            }
        }
    }

    private static IEnumerable<IPAddress> GetLinkLocalAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .Where(networkInterface => networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetworkV6)
            .Where(address => address.IsIPv6LinkLocal && address.ScopeId > 0);
    }

    private static IPAddress RandomizeHost(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        RandomNumberGenerator.Fill(bytes.AsSpan(8));
        return new IPAddress(bytes, address.ScopeId);
    }
}
