using v2rayN.WebAPI.Security;

namespace v2rayN.WebAPI.Services;

public sealed class V2rayHostedService(V2rayRuntime runtime, WebAuthService webAuth) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        runtime.InitializeAsync(cancellationToken, suppressCoreAutostart: webAuth.SetupRequired);

    public Task StopAsync(CancellationToken cancellationToken) => runtime.ShutdownAsync(cancellationToken);
}
