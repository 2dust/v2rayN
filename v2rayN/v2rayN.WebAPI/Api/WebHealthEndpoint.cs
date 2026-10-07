using System.Globalization;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Api;

internal sealed record WebHealthResponse(string Status);

internal static class WebHealthEndpoint
{
    public static WebHealthResponse CreateResponse(HttpContext context, V2rayRuntime runtime)
    {
        // Native launcher/updater probes connect directly to 127.0.0.1. Also require a
        // local Host and no forwarding headers so a local reverse proxy cannot relay
        // internal process diagnostics to its remote caller.
        if (WebSetupAccessPolicy.IsLoopbackAddress(context.Connection.RemoteIpAddress)
            && WebSetupAccessPolicy.IsLocalHost(context.Request.Host.Host)
            && WebCorsPolicy.IsSameOriginRequest(context.Request)
            && !WebSetupAccessPolicy.HasForwardedHeaders(context.Request.Headers))
        {
            context.Response.Headers["X-v2rayn-web-instance-pid"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            context.Response.Headers["X-v2rayn-web-version"] = WebBuildIdentity.Current.Version;
            context.Response.Headers["X-v2rayn-web-core-state"] = runtime.GetCoreRuntimeState();
            context.Response.Headers["X-v2rayn-web-core-process-ids"] = string.Join(',', runtime.GetCoreProcessIds());
            context.Response.Headers["X-v2rayn-web-core-profile-id"] = runtime.GetCoreRuntimeProfileId() ?? string.Empty;
            if (ShutdownDiagnostics.CurrentStage is { Length: > 0 } stage)
            {
                context.Response.Headers["X-v2rayn-web-shutdown-stage"] = stage;
            }
        }

        return new WebHealthResponse("ok");
    }
}
