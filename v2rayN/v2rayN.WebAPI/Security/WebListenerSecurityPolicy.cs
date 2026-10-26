using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace v2rayN.WebAPI.Security;

/// <summary>Checks server binding configuration, never request Host/forwarding headers.</summary>
public static class WebListenerSecurityPolicy
{
    public const string DefaultUrl = "http://127.0.0.1:5080";
    public const string MissingManagementKeyMessage =
        "Management Key is required for non-loopback Web listeners. Set V2RAYN_WEB_API_KEY before starting v2rayN.WebAPI, or bind only to 127.0.0.1, localhost, or [::1] for local first-run setup.";

    public static void ApplyDefault(WebApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey])
            && string.IsNullOrWhiteSpace(builder.Configuration["http_ports"])
            && string.IsNullOrWhiteSpace(builder.Configuration["https_ports"]))
        {
            builder.WebHost.UseUrls(DefaultUrl);
        }
    }

    public static string? GetStartupError(IConfiguration configuration, string? managementKey) =>
        string.IsNullOrWhiteSpace(managementKey) && GetEffectiveUrls(configuration).Any(url => !IsLoopbackUrl(url))
            ? MissingManagementKeyMessage
            : null;

    public static IReadOnlyList<string> GetEffectiveUrls(IConfiguration configuration)
    {
        var urls = Split(configuration[WebHostDefaults.ServerUrlsKey]);
        if (urls.Length == 0)
        {
            // ASP.NET Core expands port-only bindings to wildcard addresses, not loopback.
            urls = Split(configuration["http_ports"]).Select(port => $"http://*:{port}")
                .Concat(Split(configuration["https_ports"]).Select(port => $"https://*:{port}"))
                .ToArray();
        }

        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren()
            .Select(endpoint => endpoint["Url"] ?? string.Empty).ToArray();
        // Kestrel endpoints override hosting URLs unless PreferHostingUrls is enabled.
        if (endpoints.Length > 0
            && !(bool.TryParse(configuration[WebHostDefaults.PreferHostingUrlsKey], out var preferUrls)
                && preferUrls && urls.Length > 0))
        {
            return endpoints;
        }

        return urls.Length > 0 ? urls : [DefaultUrl];
    }

    public static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var host = uri.Host.Trim('[', ']');
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }

    private static string[] Split(string? value) =>
        (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
