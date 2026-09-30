using v2rayN.Web.Contracts;
using v2rayN.Web.Security;

namespace v2rayN.Web.Api;

public static class WebAuthEndpoints
{
    public static void MapWebAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", (WebLoginRequest request, WebAuthService auth, WebSessionService sessions) =>
        {
            if (string.IsNullOrEmpty(request.Key)
                || request.Key.Length > WebAuthService.MaximumKeyLength
                || !auth.ValidateManagementKey(request.Key))
            {
                return Results.Json(
                    ApiEnvelope<object>.Fail("management_key_invalid", ApiMessageKeys.CommonUnauthorized),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var session = sessions.CreateSession();
            return Results.Ok(new
            {
                success = true,
                data = new { token = session.Token, expiresAt = session.ExpiresAt },
            });
        }).RequireRateLimiting(WebAuthRateLimiting.LoginPolicy);

        app.MapPost("/api/auth/logout", (HttpContext context, WebSessionService sessions) =>
        {
            sessions.Revoke(WebSessionService.ExtractPresentedToken(context));
            return Results.Ok(new { success = true });
        });

        app.MapPost("/api/auth/sse-ticket", (HttpContext context, WebSessionService sessions) =>
        {
            if (!sessions.TryCreateSseTicket(WebSessionService.ExtractPresentedToken(context), out var ticket))
            {
                return Results.Json(
                    ApiEnvelope<object>.Fail("unauthorized", ApiMessageKeys.CommonUnauthorized),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new
            {
                success = true,
                data = new { ticket = ticket!.Token, expiresAt = ticket.ExpiresAt },
            });
        });
    }

    public sealed record WebLoginRequest(string? Key);
}
