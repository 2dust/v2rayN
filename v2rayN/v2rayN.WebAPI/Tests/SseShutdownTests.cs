using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using v2rayN.WebAPI.Api;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class SseShutdownTests
{
    private const string ManagementKey = "test-management-key-2026";

    [Test]
    public async Task HostShutdownClosesActiveSseStreamsInsteadOfWaitingForTheShutdownTimeout()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await Harness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions,
            shutdownTimeout: TimeSpan.FromSeconds(4));

        var session = sessions.CreateSession();
        var ticket = await GetSseTicketAsync(api, session.Token);
        var responseTask = api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(ticket)}",
            HttpCompletionOption.ResponseHeadersRead);
        for (var attempt = 0; attempt < 100 && !responseTask.IsCompleted; attempt++)
        {
            api.Events.Publish("status", new { ready = true });
            await Task.Delay(10);
        }

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var firstRead = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await (firstRead > 0).Should().BeTrue();

        // Kestrel drains open requests during a graceful stop. Without the stream
        // reacting to ApplicationStopping the host would wait for the whole
        // ShutdownTimeout (4s here) and the runtime cleanup would start too late.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        api.App.Lifetime.StopApplication();
        await api.App.WaitForShutdownAsync().WaitAsync(TimeSpan.FromSeconds(20));
        stopwatch.Stop();

        await stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2.5));
        var closed = await ReadSseStreamUntilClosedAsync(stream);
        await closed.Should().BeTrue();
    }

    private static async Task<string> GetSseTicketAsync(Harness api, string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sse-ticket");
        request.Headers.Authorization = new("Bearer", sessionToken);
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("ticket").GetString()!;
    }

    private static async Task<bool> ReadSseStreamUntilClosedAsync(Stream stream)
    {
        try
        {
            await stream.CopyToAsync(Stream.Null).WaitAsync(TimeSpan.FromSeconds(3));
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private sealed class Harness(WebApplication app, HttpClient client, EventHub events) : IAsyncDisposable
    {
        public WebApplication App => app;
        public HttpClient Client => client;
        public EventHub Events => events;

        public static async Task<Harness> StartAsync(
            WebAuthService auth,
            WebSessionService sessions,
            TimeSpan shutdownTimeout)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = shutdownTimeout);
            builder.Services.AddSingleton(auth);
            builder.Services.AddSingleton(sessions);
            builder.Services.AddSingleton<EventHub>();
            builder.Services.AddSingleton<LogBuffer>();
            builder.Services.AddSingleton<RuntimeOperationCoordinator>();
            builder.Services.AddSingleton<V2rayRuntime>();
            builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);

            var application = builder.Build();
            application.UseRouting();
            application.UseRateLimiter();
            application.UseMiddleware<WebAuthRequestBodyLimitMiddleware>();
            application.UseMiddleware<WebSessionAuthenticationMiddleware>();
            application.MapWebApi();
            application.MapWebAuthEndpoints();
            application.MapWebSetupEndpoints();
            await application.StartAsync();

            var addresses = application.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses;
            var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri(addresses.Single()),
                Timeout = TimeSpan.FromSeconds(5),
            };
            return new Harness(
                application,
                httpClient,
                application.Services.GetRequiredService<EventHub>());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            if (!App.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                await App.StopAsync();
            }
            await App.DisposeAsync();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-sse-shutdown-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
