namespace v2rayN.WebAPI.Security;

// The hosted UI retains its self-only CSP by default. Connecting it to other
// Backends is a separate opt-in from granting incoming API CORS access.
public sealed class WebUiConnectPolicy
{
    public const string EnvironmentVariable = "V2RAYN_WEB_UI_CONNECT_ORIGINS";
    public string ContentSecurityPolicy { get; }

    public WebUiConnectPolicy(string? configuration)
    {
        var origins = string.IsNullOrWhiteSpace(configuration) ? [] : configuration.Split(',')
            .Select(value => WebCorsPolicy.NormalizeOrigin(value.Trim(), EnvironmentVariable))
            .Distinct(StringComparer.Ordinal).ToArray();
        ContentSecurityPolicy = WebSecurityHeadersMiddleware.ContentSecurityPolicy
            + (origins.Length == 0 ? string.Empty : " " + string.Join(' ', origins));
    }
}
