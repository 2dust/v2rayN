using v2rayN.Web.Contracts;

namespace v2rayN.Web.Security;

public sealed class WebSessionAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WebSessionService sessions)
    {
        var path = context.Request.Path;
        if (path == "/api/events")
        {
            if (!sessions.TryConsumeSseTicket(context.Request.Query["sse_ticket"].ToString(), out var eventSession))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiEnvelope<object>.Fail("unauthorized", ApiMessageKeys.CommonUnauthorized));
                return;
            }

            context.Items[WebSessionService.SessionSnapshotContextKey] = eventSession!;
            await next(context);
            return;
        }

        if (!path.StartsWithSegments("/api")
            || path == "/api/health"
            || path == "/api/setup/status"
            || path == "/api/setup"
            || path == "/api/auth/login")
        {
            await next(context);
            return;
        }

        var presentedToken = WebSessionService.ExtractPresentedToken(context);
        var authenticated = path == "/api/auth/sse-ticket"
            ? sessions.TryValidateWithoutRenewal(presentedToken, out _)
            : sessions.TryValidateAndRenew(presentedToken, out _);
        if (!authenticated)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                ApiEnvelope<object>.Fail("unauthorized", ApiMessageKeys.CommonUnauthorized));
            return;
        }

        await next(context);
    }
}
