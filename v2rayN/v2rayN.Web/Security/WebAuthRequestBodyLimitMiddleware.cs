using Microsoft.AspNetCore.Http.Features;
using v2rayN.Web.Contracts;

namespace v2rayN.Web.Security;

public sealed class WebAuthRequestBodyLimitMiddleware(RequestDelegate next)
{
    public const long MaximumRequestBodyBytes = 64 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsLimitedAuthRequest(context.Request))
        {
            await next(context);
            return;
        }

        var requestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestBodySizeFeature is { IsReadOnly: false })
        {
            requestBodySizeFeature.MaxRequestBodySize = MaximumRequestBodyBytes;
        }

        if (context.Request.ContentLength > MaximumRequestBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(
                ApiEnvelope<object>.Fail("payload_too_large", ApiMessageKeys.CommonInvalidInput),
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsLimitedAuthRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && (request.Path == "/api/auth/login" || request.Path == "/api/setup");
}
