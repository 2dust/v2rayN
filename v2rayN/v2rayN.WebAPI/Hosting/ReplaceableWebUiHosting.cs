using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using v2rayN.WebAPI.Api;
using v2rayN.WebAPI.Contracts;

namespace v2rayN.WebAPI.Hosting;

public static class ReplaceableWebUiHosting
{
    private const string ApiOnlyMessage = "v2rayN API is running.\nNo WebUI is installed.\n";

    public static void UseReplaceableWebUi(this WebApplication app, WebUiHostOptions options)
    {
        if (options.IsAvailable)
        {
            var provider = new PhysicalFileProvider(options.RootPath!);
            app.Lifetime.ApplicationStopped.Register(provider.Dispose);

            // Static UI files are public, but the API namespace is always owned by the
            // Backend. Never let a file named api/... shadow an API or its 404 response.
            app.UseWhen(
                context => !context.Request.Path.StartsWithSegments("/api"),
                branch => branch.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = provider,
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                }));
        }

        app.MapMethods("/", [HttpMethods.Get, HttpMethods.Head], context => WriteRootAsync(context, options));
        app.MapFallback("/{**path}", context => WriteSpaFallbackAsync(context, options));
    }

    private static async Task WriteRootAsync(HttpContext context, WebUiHostOptions options)
    {
        if (!options.IsAvailable)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(ApiOnlyMessage);
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.WriteAsync(ApiOnlyMessage, context.RequestAborted);
            }
            return;
        }

        await SendIndexAsync(context, options);
    }

    private static async Task WriteSpaFallbackAsync(HttpContext context, WebUiHostOptions options)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                ApiEnvelope<object>.Fail("route_not_found", ApiMessageKeys.CommonRouteNotFound),
                context.RequestAborted);
            return;
        }

        if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            || !options.IsAvailable
            || HasFileExtension(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await SendIndexAsync(context, options);
    }

    private static bool HasFileExtension(PathString path)
    {
        var finalSegment = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return !string.IsNullOrEmpty(finalSegment) && Path.HasExtension(finalSegment);
    }

    private static async Task SendIndexAsync(HttpContext context, WebUiHostOptions options)
    {
        var indexPath = options.IndexFilePath;
        try
        {
            if (indexPath is null || !File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = new FileInfo(indexPath).Length;
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.SendFileAsync(indexPath, context.RequestAborted);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
}
