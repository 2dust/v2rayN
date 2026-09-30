using System.Net;
using Microsoft.AspNetCore.Http;

namespace v2rayN.Web.Security;

public static class WebSetupAccessPolicy
{
    public static bool IsAllowed(
        IPAddress? remoteAddress,
        string? host,
        bool forwardedHeadersPresent,
        IPAddress? localAddress = null) =>
        !forwardedHeadersPresent
        && ((IsLoopbackAddress(remoteAddress) && IsLocalHost(host))
            || (IsPrivateNetworkAddress(remoteAddress)
                && IsPrivateNetworkAddress(localAddress)
                && HostMatchesAddress(host, localAddress)));

    public static bool IsLoopbackAddress(IPAddress? remoteAddress)
    {
        if (remoteAddress is null)
        {
            return false;
        }

        if (remoteAddress.Equals(IPAddress.Loopback) || remoteAddress.Equals(IPAddress.IPv6Loopback))
        {
            return true;
        }

        return remoteAddress.IsIPv4MappedToIPv6
            && remoteAddress.MapToIPv4().Equals(IPAddress.Loopback);
    }

    public static bool IsLocalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim();
        if (normalized.Length > 1 && normalized[0] == '[' && normalized[^1] == ']')
        {
            normalized = normalized[1..^1];
        }

        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(normalized, out var address)
            && (address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback));
    }

    public static bool IsPrivateNetworkAddress(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && ((bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80));
    }

    private static bool HostMatchesAddress(string? host, IPAddress? address)
    {
        if (address is null || string.IsNullOrWhiteSpace(host)) return false;
        var normalized = host.Trim();
        if (normalized.Length > 1 && normalized[0] == '[' && normalized[^1] == ']')
        {
            normalized = normalized[1..^1];
        }

        if (!IPAddress.TryParse(normalized, out var hostAddress)) return false;
        if (hostAddress.IsIPv4MappedToIPv6) hostAddress = hostAddress.MapToIPv4();
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return hostAddress.Equals(address);
    }

    public static bool HasForwardedHeaders(IHeaderDictionary headers) =>
        headers.Keys.Any(key =>
            key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
            || key.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase));
}
