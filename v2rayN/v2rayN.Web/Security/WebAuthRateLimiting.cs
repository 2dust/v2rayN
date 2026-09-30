using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace v2rayN.Web.Security;

public static class WebAuthRateLimiting
{
    public const string LoginPolicy = "web-auth-login";
    public const string SetupPolicy = "web-auth-setup";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = Math.Ceiling(retryAfter.TotalSeconds);
                context.HttpContext.Response.Headers["Retry-After"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return ValueTask.CompletedTask;
        };
        options.AddPolicy(LoginPolicy, context => CreateIpPartition(context, permitLimit: 5));
        options.AddPolicy(SetupPolicy, context => CreateIpPartition(context, permitLimit: 10));
    }

    private static RateLimitPartition<string> CreateIpPartition(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromMinutes(1),
            });
}
