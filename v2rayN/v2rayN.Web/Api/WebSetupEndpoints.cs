using v2rayN.Web.Security;

namespace v2rayN.Web.Api;

public static class WebSetupEndpoints
{
    public static void MapWebSetupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/setup/status", (HttpContext context, WebAuthService auth) => Results.Ok(new
        {
            setupRequired = auth.SetupRequired,
            environmentKeyConfigured = auth.EnvironmentKeyConfigured,
            setupAllowedFromThisRequest = WebSetupAccessPolicy.IsAllowed(
                context.Connection.RemoteIpAddress,
                context.Request.Host.Host,
                WebSetupAccessPolicy.HasForwardedHeaders(context.Request.Headers),
                context.Connection.LocalIpAddress),
        }));

        app.MapPost("/api/setup", async (WebSetupRequest request, HttpContext context, WebAuthService auth, WebSessionService sessions) =>
        {
            var setupAccessAllowed = WebSetupAccessPolicy.IsAllowed(
                context.Connection.RemoteIpAddress,
                context.Request.Host.Host,
                WebSetupAccessPolicy.HasForwardedHeaders(context.Request.Headers),
                context.Connection.LocalIpAddress);
            var result = await auth.SetupAsync(
                request.Key,
                request.ConfirmKey,
                setupAccessAllowed,
                context.RequestAborted);

            return result switch
            {
                WebSetupResult.Created => CreateSetupResponse(sessions),
                WebSetupResult.Forbidden => Results.Json(new { error = "private_network_required" }, statusCode: StatusCodes.Status403Forbidden),
                WebSetupResult.KeyTooShort => Results.BadRequest(new { error = "key_too_short", minimumLength = WebAuthService.MinimumKeyLength }),
                WebSetupResult.KeyTooLong => Results.BadRequest(new { error = "key_too_long", maximumLength = WebAuthService.MaximumKeyLength }),
                WebSetupResult.KeysDoNotMatch => Results.BadRequest(new { error = "keys_do_not_match" }),
                WebSetupResult.AlreadyConfigured => Results.Conflict(new { error = "already_configured" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting(WebAuthRateLimiting.SetupPolicy);
    }

    private static IResult CreateSetupResponse(WebSessionService sessions)
    {
        var session = sessions.CreateSession();
        return Results.Ok(new { setupRequired = false, token = session.Token, expiresAt = session.ExpiresAt });
    }

    public sealed record WebSetupRequest(string? Key, string? ConfirmKey);
}
