using Microsoft.AspNetCore.Cors.Infrastructure;
using v2rayN.WebAPI.Contracts;

namespace v2rayN.WebAPI.Security;

public sealed class WebCorsPolicy
{
    public const string EnvironmentVariable = "V2RAYN_WEB_ALLOWED_ORIGINS";
    public const string PolicyName = "WebApiOrigins";
    private readonly HashSet<string> _origins;

    public WebCorsPolicy(string? configuration)
    {
        _origins = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(configuration)) return;
        foreach (var item in configuration.Split(','))
        {
            _origins.Add(NormalizeOrigin(item.Trim()));
        }
    }

    public static string NormalizeOrigin(string value, string variable = EnvironmentVariable)
    {
        var authorityStart = value.IndexOf("://", StringComparison.Ordinal);
        var slash = authorityStart < 0 ? -1 : value.IndexOf('/', authorityStart + 3);
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace)
            || authorityStart < 0 || (slash >= 0 && slash != value.Length - 1)
            || value.IndexOfAny(['*', '\\', '?', '#']) >= 0
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host)
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/")
        {
            // Do not echo configuration: an invalid value might contain credentials.
            throw new ArgumentException($"{variable} must contain only comma-separated http(s) origins (scheme, host and port), without credentials, paths, queries, fragments or wildcards.");
        }
        return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
    }

    public bool Allows(string origin) => _origins.Contains(origin);

    // A same-origin browser may reach HTTP Kestrel through a TLS/path-prefix
    // reverse proxy. Its public origin need not equal Kestrel's internal URL.
    // Fetch Metadata is browser-controlled (JS cannot forge Sec-Fetch-Site).
    // This is NOT used to grant setup access or internal health diagnostics.
    public static bool IsBrowserSameOriginRequest(HttpRequest request)
    {
        if (request.Headers["Sec-Fetch-Site"].Count != 1
            || request.Headers["Sec-Fetch-Site"] != "same-origin"
            || request.Headers.Origin.Count != 1) return false;
        try
        {
            var origin = request.Headers.Origin.ToString();
            return NormalizeOrigin(origin) == origin;
        }
        catch (ArgumentException) { return false; }
    }

    public static bool IsSameOriginRequest(HttpRequest request)
    {
        if (!request.Headers.ContainsKey("Origin")) return true; // Native clients.
        if (request.Headers.Origin.Count != 1) return false;
        try
        {
            return string.Equals(request.Headers.Origin.ToString(),
                NormalizeOrigin($"{request.Scheme}://{request.Host}"), StringComparison.Ordinal);
        }
        catch (ArgumentException) { return false; }
    }

    public void Configure(CorsPolicyBuilder builder) => builder
        .SetIsOriginAllowed(Allows)
        .WithMethods("GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type")
        .WithExposedHeaders("Content-Disposition");
}

// CORS alone controls readability, not execution. Reject a disallowed Origin
// before authentication, rate limiting, SSE-ticket consumption or API mutations.
public sealed class WebOriginGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WebCorsPolicy cors)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && !WebCorsPolicy.IsSameOriginRequest(context.Request)
            && !WebCorsPolicy.IsBrowserSameOriginRequest(context.Request)
            && (context.Request.Headers.Origin.Count != 1 || !cors.Allows(context.Request.Headers.Origin.ToString())))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail("origin_not_allowed", ApiMessageKeys.CommonInvalidInput));
            return;
        }
        await next(context);
    }
}
