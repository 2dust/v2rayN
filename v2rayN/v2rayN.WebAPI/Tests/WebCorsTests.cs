using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using v2rayN.WebAPI.Api;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class WebCorsTests
{
    private const string Origin = "https://good.example.com";
    private const string Key = "isolated-cors-management-key";

    [Test]
    public async Task ConfigurationRejectsUnsafeOriginsAndMatchesExactSchemeHostPort()
    {
        foreach (var invalid in new[] { "*", "null", "file://host", "https://*.example.com", "https://good.example.com/path",
            "https://good.example.com?", "https://good.example.com#", "https://user:password@good.example.com", "https://good.example.com,", "https://good.example.com\\evil", "https://good.example.com/other/..", "https://good.example.com/%2e" })
        {
            var rejected = false;
            try { _ = new WebCorsPolicy(invalid); } catch (ArgumentException) { rejected = true; }
            await rejected.Should().BeTrue();
        }
        var policy = new WebCorsPolicy(" HTTPS://GOOD.EXAMPLE.COM:443, http://localhost:5173 ");
        await policy.Allows(Origin).Should().BeTrue();
        await policy.Allows("http://good.example.com").Should().BeFalse();
        await policy.Allows("https://good.example.com:444").Should().BeFalse();
        await policy.Allows("https://good.example.com.evil.com").Should().BeFalse();
    }

    [Test]
    public async Task DefaultDeniesCrossOriginIncludingSimpleLoginWithoutExecutingIt()
    {
        await using var api = await Harness.StartAsync(null);
        using var request = Request(HttpMethod.Post, "/api/auth/login", Origin);
        request.Content = JsonContent.Create(new { key = Key });
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
        await response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await document.RootElement.GetProperty("code").GetString().Should().BeEqualTo("origin_not_allowed");
    }

    [Test]
    public async Task SimilarOriginAndNullOriginAreDeniedEvenWithAValidBearer()
    {
        await using var api = await Harness.StartAsync(Origin);
        foreach (var origin in new[] { Origin + ".evil.com", "http://good.example.com", Origin + ":444", "null" })
        {
            using var request = Request(HttpMethod.Get, "/api/test/session", origin, api.Sessions.CreateSession().Token);
            using var response = await api.Client.SendAsync(request);
            await (response.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
            await response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        }
    }

    [Test]
    public async Task PreflightCompletesWithoutBearerAndDoesNotEnableCookiesOrPrivateNetworkBypass()
    {
        await using var api = await Harness.StartAsync(Origin);
        using var request = Request(HttpMethod.Options, "/api/test/session", Origin);
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        request.Headers.Add("Access-Control-Request-Private-Network", "true");
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.NoContent).Should().BeTrue();
        await AssertCorsAsync(response);
        await response.Headers.GetValues("Access-Control-Allow-Methods").Single().Contains("PUT").Should().BeTrue();
        await response.Headers.GetValues("Access-Control-Allow-Headers").Single().Contains("authorization", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        await response.Headers.Contains("Access-Control-Allow-Private-Network").Should().BeFalse();
    }

    [Test]
    public async Task LoginAndBearerRestWorkAndManagementKeyIsStillNotAnApiToken()
    {
        await using var api = await Harness.StartAsync(Origin);
        using var login = Request(HttpMethod.Post, "/api/auth/login", Origin);
        login.Content = JsonContent.Create(new { key = Key });
        using var loggedIn = await api.Client.SendAsync(login);
        await (loggedIn.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await AssertCorsAsync(loggedIn);
        using var document = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        using var request = Request(HttpMethod.Get, "/api/test/session", Origin, token);
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await AssertCorsAsync(response);
        using var wrong = Request(HttpMethod.Get, "/api/test/session", Origin, Key);
        using var unauthorized = await api.Client.SendAsync(wrong);
        await (unauthorized.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
        await AssertCorsAsync(unauthorized);
    }

    [Test]
    public async Task TicketAndRealSseEndpointWorkAcrossOriginAndTicketCannotBeConsumedByDeniedOrigin()
    {
        await using var api = await Harness.StartAsync(Origin);
        var token = api.Sessions.CreateSession().Token;
        using var ticketRequest = Request(HttpMethod.Post, "/api/auth/sse-ticket", Origin, token);
        using var ticketResponse = await api.Client.SendAsync(ticketRequest);
        await AssertCorsAsync(ticketResponse);
        using var document = JsonDocument.Parse(await ticketResponse.Content.ReadAsStringAsync());
        var ticket = document.RootElement.GetProperty("data").GetProperty("ticket").GetString()!;
        var path = "/api/events?sse_ticket=" + Uri.EscapeDataString(ticket);
        using var deniedRequest = Request(HttpMethod.Get, path, Origin + ".evil.com");
        using var denied = await api.Client.SendAsync(deniedRequest);
        await (denied.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
        using var request = Request(HttpMethod.Get, path, Origin);
        var responseTask = api.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        for (var attempt = 0; attempt < 100 && !responseTask.IsCompleted; attempt++)
        {
            api.Events.Publish("status", new { ready = true });
            await Task.Delay(10);
        }
        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await AssertCorsAsync(response);
        await (response.Content.Headers.ContentType?.MediaType == "text/event-stream").Should().BeTrue();
        api.Sessions.Revoke(token);
    }

    [Test]
    public async Task DownloadExposesOnlyRequiredPublicHeaders()
    {
        await using var api = await Harness.StartAsync(Origin);
        using var request = Request(HttpMethod.Get, "/api/test/download", Origin, api.Sessions.CreateSession().Token);
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await AssertCorsAsync(response);
        await (response.Content.Headers.ContentDisposition?.FileNameStar == "backup.zip").Should().BeTrue();
        await response.Headers.GetValues("Access-Control-Expose-Headers").Single().Should().BeEqualTo("Content-Disposition");
    }

    [Test]
    public async Task AllowedOriginCannotSetupAndStatusExplainsWhyWhileSameOriginStillCan()
    {
        await using var api = await Harness.StartAsync(Origin, configured: false);
        using var statusRequest = Request(HttpMethod.Get, "/api/setup/status", Origin);
        using var status = await api.Client.SendAsync(statusRequest);
        await AssertCorsAsync(status);
        using var statusDocument = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        await statusDocument.RootElement.GetProperty("setupRequired").GetBoolean().Should().BeTrue();
        await statusDocument.RootElement.GetProperty("setupAllowedFromThisRequest").GetBoolean().Should().BeFalse();
        using var setupRequest = Request(HttpMethod.Post, "/api/setup", Origin);
        setupRequest.Content = JsonContent.Create(new { key = Key, confirmKey = Key });
        using var denied = await api.Client.SendAsync(setupRequest);
        await (denied.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
        await api.Auth.SetupRequired.Should().BeTrue();
        using var sameOrigin = Request(HttpMethod.Post, "/api/setup", api.Client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        sameOrigin.Content = JsonContent.Create(new { key = Key, confirmKey = Key });
        using var created = await api.Client.SendAsync(sameOrigin);
        await (created.StatusCode == HttpStatusCode.OK).Should().BeTrue();
    }

    [Test]
    public async Task NativeAndSameOriginRequestsAreUnchangedWithoutAllowedOrigins()
    {
        await using var api = await Harness.StartAsync(null);
        foreach (var origin in new[] { null, api.Client.BaseAddress!.GetLeftPart(UriPartial.Authority) })
        {
            using var request = Request(HttpMethod.Get, "/api/test/session", origin, api.Sessions.CreateSession().Token);
            using var response = await api.Client.SendAsync(request);
            await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        }
    }

    [Test]
    public async Task SameOriginBrowserBehindTlsProxyKeepsRestButCannotRelaySetup()
    {
        await using var api = await Harness.StartAsync(null, configured: false);
        var token = api.Sessions.CreateSession().Token;
        using var request = Request(HttpMethod.Get, "/api/test/session", "https://proxy.example.com", token);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        // Genuine cross-origin browser requests cannot forge same-origin metadata.
        using var crossSite = Request(HttpMethod.Get, "/api/test/session", "https://proxy.example.com", token);
        crossSite.Headers.Add("Sec-Fetch-Site", "same-site");
        using var denied = await api.Client.SendAsync(crossSite);
        await (denied.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();
        using var statusRequest = Request(HttpMethod.Get, "/api/setup/status", "https://proxy.example.com");
        statusRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var status = await api.Client.SendAsync(statusRequest);
        using var document = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        await document.RootElement.GetProperty("setupAllowedFromThisRequest").GetBoolean().Should().BeFalse();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? origin, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        return request;
    }

    private static async Task AssertCorsAsync(HttpResponseMessage response)
    {
        await response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().BeEqualTo(Origin);
        await response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    private sealed class Harness(WebApplication app, HttpClient client, WebAuthService auth, string directory) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public WebAuthService Auth { get; } = auth;
        public WebSessionService Sessions => app.Services.GetRequiredService<WebSessionService>();
        public EventHub Events => app.Services.GetRequiredService<EventHub>();

        public static async Task<Harness> StartAsync(string? origins, bool configured = true)
        {
            var directory = Path.Combine(Path.GetTempPath(), "webapi-cors-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var auth = new WebAuthService(Path.Combine(directory, "web-auth.json"), configured ? Key : null);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Logging.ClearProviders();
            var cors = new WebCorsPolicy(origins);
            builder.Services.AddSingleton(cors);
            builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy.PolicyName, cors.Configure));
            builder.Services.AddSingleton(auth);
            builder.Services.AddSingleton<WebSessionService>();
            builder.Services.AddSingleton<EventHub>();
            builder.Services.AddSingleton<LogBuffer>();
            builder.Services.AddSingleton<RuntimeOperationCoordinator>();
            builder.Services.AddSingleton<V2rayRuntime>();
            builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);
            var app = builder.Build();
            app.UseMiddleware<WebSecurityHeadersMiddleware>();
            app.UseMiddleware<WebAuthRequestBodyLimitMiddleware>();
            app.UseRouting();
            app.UseMiddleware<WebOriginGuardMiddleware>();
            app.UseCors(WebCorsPolicy.PolicyName);
            app.UseRateLimiter();
            app.UseMiddleware<WebSessionAuthenticationMiddleware>();
            app.MapWebApi();
            app.MapWebAuthEndpoints();
            app.MapWebSetupEndpoints();
            app.MapGet("/api/test/session", () => Results.Ok(new { authorized = true }));
            app.MapGet("/api/test/download", () => Results.File(new byte[] { 1, 2, 3 }, "application/zip", "backup.zip"));
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Harness(app, new HttpClient(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = new Uri(address) }, auth, directory);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
