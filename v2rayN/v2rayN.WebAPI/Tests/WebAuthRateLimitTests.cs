using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using v2rayN.WebAPI.Api;
using v2rayN.WebAPI.Security;

namespace v2rayN.WebAPI.Tests;

public class WebAuthRateLimitTests
{
    [Test]
    public async Task LoginRateLimiterRejectsExcessAttemptsWithTooManyRequests()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);
        builder.Services.AddSingleton(new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), null));
        builder.Services.AddSingleton(sessions);

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseMiddleware<WebSessionAuthenticationMiddleware>();
        app.MapWebAuthEndpoints();
        await app.StartAsync();
        try
        {
            using var client = CreateClient(app);
            var statuses = new List<HttpStatusCode>();
            int? retryAfterSeconds = null;

            for (var attempt = 0; attempt < 6; attempt++)
            {
                using var response = await client.PostAsJsonAsync("/api/auth/login", new { key = "incorrect-management-key" });
                statuses.Add(response.StatusCode);
                if (attempt == 5 && response.Headers.TryGetValues("Retry-After", out var values)
                    && int.TryParse(values.SingleOrDefault(), out var seconds))
                {
                    retryAfterSeconds = seconds;
                }
            }

            await statuses.Take(5).All(status => status == HttpStatusCode.Unauthorized).Should().BeTrue();
            await (statuses[5] == HttpStatusCode.TooManyRequests).Should().BeTrue();
            await (retryAfterSeconds > 0).Should().BeTrue();
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    [Arguments("/login-limited")]
    [Arguments("/setup-limited")]
    public async Task AllLoopbackAddressesShareOneRateLimitPartition(string endpoint)
    {
        await using var app = CreatePartitionTestApp();
        await app.StartAsync();
        try
        {
            using var client = CreateClient(app);
            var loopbackAddresses = new[] { "127.0.0.1", "127.0.0.2", "::1", "::ffff:127.0.0.1" };
            var permitLimit = endpoint == "/login-limited" ? 5 : 10;

            for (var index = 0; index < permitLimit; index++)
            {
                await (await GetLimitedStatusAsync(client, endpoint, loopbackAddresses[index % loopbackAddresses.Length], index))
                    .Should().BeEqualTo(HttpStatusCode.OK);
            }

            await (await GetLimitedStatusAsync(client, endpoint, loopbackAddresses[permitLimit % loopbackAddresses.Length], permitLimit))
                .Should().BeEqualTo(HttpStatusCode.TooManyRequests);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    [Arguments("/login-limited")]
    [Arguments("/setup-limited")]
    public async Task DifferentNonLoopbackAddressesKeepIndependentRateLimitPartitions(string endpoint)
    {
        await using var app = CreatePartitionTestApp();
        await app.StartAsync();
        try
        {
            using var client = CreateClient(app);
            var permitLimit = endpoint == "/login-limited" ? 5 : 10;

            for (var index = 0; index < permitLimit; index++)
            {
                await (await GetLimitedStatusAsync(client, endpoint, "192.0.2.1", index))
                    .Should().BeEqualTo(HttpStatusCode.OK);
            }

            await (await GetLimitedStatusAsync(client, endpoint, "192.0.2.2", permitLimit))
                .Should().BeEqualTo(HttpStatusCode.OK);
            await (await GetLimitedStatusAsync(client, endpoint, "192.0.2.1", permitLimit + 1))
                .Should().BeEqualTo(HttpStatusCode.TooManyRequests);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    [NotInParallel]
    public async Task LocalRateLimitRequestsDoNotUseTheDefaultProxy()
    {
        var proxyBuilder = WebApplication.CreateBuilder();
        proxyBuilder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var proxy = proxyBuilder.Build();
        proxy.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return Task.CompletedTask;
        });
        await proxy.StartAsync();
        await using var app = CreatePartitionTestApp();
        await app.StartAsync();
        var proxyAddress = proxy.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var previousProxy = HttpClient.DefaultProxy;
        try
        {
            HttpClient.DefaultProxy = new FixedProxy(new Uri(proxyAddress));
            using var client = CreateClient(app);
            await (await GetLimitedStatusAsync(client, "/setup-limited", "192.0.2.1", 0))
                .Should().BeEqualTo(HttpStatusCode.OK);
        }
        finally
        {
            HttpClient.DefaultProxy = previousProxy;
            await app.StopAsync();
            await proxy.StopAsync();
        }
    }
    private static WebApplication CreatePartitionTestApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);

        var app = builder.Build();
        app.UseRouting();
        // Test-only injection lets the integration tests exercise multiple peer addresses
        // without trusting forwarded headers in the WebAPI pipeline.
        app.Use(async (context, next) =>
        {
            if (IPAddress.TryParse(context.Request.Headers["X-Test-Remote-Ip"].FirstOrDefault(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await next();
        });
        app.UseRateLimiter();
        app.MapGet("/login-limited", () => Results.Ok())
            .RequireRateLimiting(WebAuthRateLimiting.LoginPolicy);
        app.MapGet("/setup-limited", () => Results.Ok())
            .RequireRateLimiting(WebAuthRateLimiting.SetupPolicy);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private static async Task<HttpStatusCode> GetLimitedStatusAsync(HttpClient client, string endpoint, string remoteIp, int requestIndex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("X-Test-Remote-Ip", remoteIp);
        request.Headers.Add("X-Forwarded-For", $"198.51.100.{requestIndex + 1}");
        request.Headers.Add("Forwarded", $"for=198.51.100.{requestIndex + 1}");
        request.Headers.Add("X-Real-IP", $"198.51.100.{requestIndex + 1}");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private sealed class FixedProxy(Uri address) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }
        public Uri GetProxy(Uri destination) => address;
        public bool IsBypassed(Uri host) => false;
    }
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-rate-limit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
