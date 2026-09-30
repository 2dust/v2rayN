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
using v2rayN.Web.Api;
using v2rayN.Web.Security;

namespace v2rayN.Web.Tests;

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
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(20) };
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-rate-limit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
