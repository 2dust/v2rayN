using System.Net;
using v2rayN.WebAPI.Security;

namespace v2rayN.WebAPI.Launcher;

/// <summary>
/// Resolves the loopback HTTP health-probe endpoint that matches an instance's effective
/// Kestrel listeners. The diagnostic endpoint is loopback-only, so a probe is produced
/// only for listeners that can be reached over a loopback address; listeners that cannot
/// (HTTPS-only, an explicit non-loopback address, or malformed configuration) are
/// rejected instead of being rewritten to a guessed address or port. The rule set is
/// shared with the health diagnostics and the update helper: every produced URI passes
/// <see cref="IsProbeableLoopbackUri"/>.
/// </summary>
public static class WebProbeUriResolver
{
    public const string HealthPath = "/api/health";

    public sealed record WebProbeEndpoints(Uri HealthUri, Uri ApiUri);

    /// <summary>
    /// True when a URI can carry a verifiable health probe: plain HTTP to a host the
    /// health diagnostics accept as local (localhost, 127.0.0.1 or ::1). The update
    /// helper and the produced probe URIs both use this single rule.
    /// </summary>
    public static bool IsProbeableLoopbackUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && WebSetupAccessPolicy.IsLocalHost(uri.Host);

    /// <summary>
    /// Resolves the health and API URLs for the first effective listener that can be probed
    /// over loopback HTTP. Returns false when no listener can be probed reliably.
    /// </summary>
    public static bool TryResolve(IConfiguration configuration, out WebProbeEndpoints endpoints)
    {
        foreach (var listenUrl in WebListenerSecurityPolicy.GetEffectiveUrls(configuration))
        {
            if (!TryCreateHealthUri(listenUrl, out var healthUri))
            {
                continue;
            }

            endpoints = new WebProbeEndpoints(healthUri, new UriBuilder(healthUri) { Path = "/" }.Uri);
            return true;
        }

        endpoints = null!;
        return false;
    }

    /// <summary>
    /// Maps one effective listen URL to its loopback health-probe URL. Kestrel treats
    /// non-loopback host names as wildcard binds, so those probe the local host name to
    /// cover both address families; explicit wildcards keep their address family.
    /// </summary>
    public static bool TryCreateHealthUri(string listenUrl, out Uri healthUri)
    {
        healthUri = null!;
        var trimmed = listenUrl?.Trim() ?? string.Empty;
        var schemeSeparator = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator <= 0)
        {
            return false;
        }

        // Kestrel's "*" and "+" wildcard hosts are not valid System.Uri hosts; replace the
        // host token so the URL can be parsed, and remember to probe the local host name.
        var authorityStart = schemeSeparator + 3;
        var wildcard = authorityStart < trimmed.Length && trimmed[authorityStart] is '*' or '+';
        var normalized = wildcard
            ? string.Concat(trimmed.AsSpan(0, authorityStart), "localhost", trimmed.AsSpan(authorityStart + 1))
            : trimmed;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        var probeHost = wildcard ? "localhost" : MapToLoopbackHost(uri.Host);
        if (probeHost is null)
        {
            return false;
        }

        healthUri = new UriBuilder(Uri.UriSchemeHttp, probeHost, uri.Port, HealthPath).Uri;
        // Gate the generated probe through the same rule the health diagnostics and the
        // update helper use; loopback aliases such as 127.0.0.2 are not verifiable there.
        return IsProbeableLoopbackUri(healthUri.ToString());
    }

    private static string? MapToLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            // Keep the configured name: resolving "localhost" covers both families and
            // avoids forcing a configured name onto one IPv4 literal.
            return "localhost";
        }

        // System.Uri keeps the brackets around IPv6 literals.
        var literal = host.Trim('[', ']');
        if (!IPAddress.TryParse(literal, out var address))
        {
            // Kestrel binds a host name that is not "localhost" as a wildcard, which
            // includes both loopback addresses where the platform provides them.
            return "localhost";
        }

        if (address.Equals(IPAddress.Any))
        {
            return IPAddress.Loopback.ToString();
        }

        if (address.Equals(IPAddress.IPv6Any))
        {
            return IPAddress.IPv6Loopback.ToString();
        }

        return IPAddress.IsLoopback(address) ? literal : null;
    }
}
