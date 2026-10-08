using System.Net;
using Microsoft.AspNetCore.Http;

namespace v2rayN.WebAPI.Security;

public static class WebSetupAccessPolicy
{
    public static bool IsAllowed(
        IPAddress? remoteAddress,
        string? host,
        bool forwardedHeadersPresent) =>
        !forwardedHeadersPresent
        && IsLoopbackAddress(remoteAddress) && IsLocalHost(host);

    public static bool IsAllowedFromRequest(HttpContext context) =>
        WebCorsPolicy.IsSameOriginRequest(context.Request)
        && IsAllowed(context.Connection.RemoteIpAddress, context.Request.Host.Host,
            HasForwardedHeaders(context.Request.Headers));

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

    public static bool HasForwardedHeaders(IHeaderDictionary headers) =>
        headers.Keys.Any(key =>
            key.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
            || key.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase));
}
