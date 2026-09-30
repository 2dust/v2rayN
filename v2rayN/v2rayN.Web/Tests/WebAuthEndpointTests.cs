using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using v2rayN.Web.Api;
using v2rayN.Web.Contracts;
using v2rayN.Web.Security;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class WebAuthEndpointTests
{
    private const string ManagementKey = "test-management-key-2026";

    [Test]
    public async Task EnvironmentManagementKeyLogsInAndOnlyTheReturnedSessionAuthenticates()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);

        using var invalidLogin = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = "wrong-management-key" });
        await (invalidLogin.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
        using var invalidLoginDocument = JsonDocument.Parse(await invalidLogin.Content.ReadAsStringAsync());
        await invalidLoginDocument.RootElement.GetProperty("code").GetString()
            .Should().BeEqualTo("management_key_invalid");
        using var validLogin = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = ManagementKey });
        await (validLogin.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var loginDocument = JsonDocument.Parse(await validLogin.Content.ReadAsStringAsync());
        var sessionToken = loginDocument.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        await (sessionToken.Length == 43).Should().BeTrue();
        await (loginDocument.RootElement.GetProperty("data").GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow).Should().BeTrue();

        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        sessionRequest.Headers.Authorization = new("Bearer", sessionToken);
        using var sessionResponse = await api.Client.SendAsync(sessionRequest);
        await (sessionResponse.StatusCode == HttpStatusCode.OK).Should().BeTrue();

        using var managementKeyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        managementKeyRequest.Headers.Authorization = new("Bearer", ManagementKey);
        using var managementKeyResponse = await api.Client.SendAsync(managementKeyRequest);
        await (managementKeyResponse.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        using var managementTicketRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sse-ticket");
        managementTicketRequest.Headers.Authorization = new("Bearer", ManagementKey);
        using var managementTicketResponse = await api.Client.SendAsync(managementTicketRequest);
        await (managementTicketResponse.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        using var managementKeySse = await api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(ManagementKey)}",
            HttpCompletionOption.ResponseHeadersRead);
        await (managementKeySse.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        using var legacySessionUrl = await api.Client.GetAsync(
            $"/api/events?access_token={Uri.EscapeDataString(sessionToken)}",
            HttpCompletionOption.ResponseHeadersRead);
        await (legacySessionUrl.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
    }

    [Test]
    public async Task PersistedManagementKeyExchangesForSessionAtLogin()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        var seedAuth = new WebAuthService(configPath, null);
        await ((await seedAuth.SetupAsync(ManagementKey, ManagementKey, setupAccessAllowed: true)) == WebSetupResult.Created).Should().BeTrue();

        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(new WebAuthService(configPath, null), sessions);

        using var response = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = ManagementKey });
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        await sessions.TryValidateWithoutRenewal(token, out _).Should().BeTrue();
    }

    [Test]
    public async Task RuntimeDataFailureAfterLoginDoesNotInvalidateTheEstablishedSession()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);

        using var login = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = ManagementKey });
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDocument.RootElement.GetProperty("data").GetProperty("token").GetString()!;
        using var failedDataRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/runtime-failure");
        failedDataRequest.Headers.Authorization = new("Bearer", token);

        using var failure = await api.Client.SendAsync(failedDataRequest);

        await (failure.StatusCode == HttpStatusCode.InternalServerError).Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(token, out _).Should().BeTrue();
    }

    [Test]
    public async Task CorrectManagementKeyWithSessionCreationFailureIsNotReportedAsWrongPassword()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        sessions.Dispose();

        using var response = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = ManagementKey });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        await (response.StatusCode == HttpStatusCode.InternalServerError).Should().BeTrue();
        await document.RootElement.GetProperty("code").GetString().Should().BeEqualTo("internal_error");
        await sessions.TryValidateWithoutRenewal(ManagementKey, out _).Should().BeFalse();
    }

    [Test]
    public async Task SetupRequiresLocalAddressAndAllowedHostAndReturnsAUsableSession()
    {
        using var directory = new TemporaryDirectory();
        var configPath = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(new WebAuthService(configPath, null), sessions);

        using var status = await api.Client.GetAsync("/api/setup/status");
        using var statusDocument = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        await statusDocument.RootElement.GetProperty("setupRequired").GetBoolean().Should().BeTrue();
        await statusDocument.RootElement.GetProperty("setupAllowedFromThisRequest").GetBoolean().Should().BeTrue();

        using var maliciousHostRequest = new HttpRequestMessage(HttpMethod.Get, "/api/setup/status");
        maliciousHostRequest.Headers.Host = "attacker.example";
        using var maliciousHostStatus = await api.Client.SendAsync(maliciousHostRequest);
        using var maliciousStatusDocument = JsonDocument.Parse(await maliciousHostStatus.Content.ReadAsStringAsync());
        await maliciousStatusDocument.RootElement.GetProperty("setupAllowedFromThisRequest").GetBoolean().Should().BeFalse();

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/setup")
        {
            Content = JsonContent.Create(new { key = ManagementKey, confirmKey = ManagementKey }),
        };
        deniedRequest.Headers.Host = "attacker.example";
        using var deniedResponse = await api.Client.SendAsync(deniedRequest);
        await (deniedResponse.StatusCode == HttpStatusCode.Forbidden).Should().BeTrue();

        using var setupResponse = await api.Client.PostAsJsonAsync("/api/setup", new { key = ManagementKey, confirmKey = ManagementKey });
        await (setupResponse.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var setupDocument = JsonDocument.Parse(await setupResponse.Content.ReadAsStringAsync());
        await setupDocument.RootElement.GetProperty("setupRequired").GetBoolean().Should().BeFalse();
        var sessionToken = setupDocument.RootElement.GetProperty("token").GetString()!;
        await sessions.TryValidateWithoutRenewal(sessionToken, out _).Should().BeTrue();

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        protectedRequest.Headers.Authorization = new("Bearer", sessionToken);
        using var protectedResponse = await api.Client.SendAsync(protectedRequest);
        await (protectedResponse.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        var persistedConfig = await File.ReadAllTextAsync(configPath);
        await persistedConfig.Contains(ManagementKey, StringComparison.Ordinal).Should().BeFalse();
    }

    [Test]
    public async Task LogoutRevokesOnlyTheCurrentSession()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var first = sessions.CreateSession();
        var second = sessions.CreateSession();

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Authorization = new("Bearer", first.Token);
        using var logoutResponse = await api.Client.SendAsync(logout);
        await (logoutResponse.StatusCode == HttpStatusCode.OK).Should().BeTrue();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        firstRequest.Headers.Authorization = new("Bearer", first.Token);
        using var firstResponse = await api.Client.SendAsync(firstRequest);
        await (firstResponse.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        secondRequest.Headers.Authorization = new("Bearer", second.Token);
        using var secondResponse = await api.Client.SendAsync(secondRequest);
        await (secondResponse.StatusCode == HttpStatusCode.OK).Should().BeTrue();
    }

    [Test]
    public async Task SseTicketIsOneTimeAndCannotAuthenticateRest()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var session = sessions.CreateSession();
        var ticket = await GetSseTicketAsync(api, session.Token);

        using var restWithTicket = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        restWithTicket.Headers.Authorization = new("Bearer", ticket);
        using var restResponse = await api.Client.SendAsync(restWithTicket);
        await (restResponse.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        var responseTask = api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(ticket)}",
            HttpCompletionOption.ResponseHeadersRead);
        for (var attempt = 0; attempt < 100 && !responseTask.IsCompleted; attempt++)
        {
            api.Events.Publish("status", new { ready = true });
            await Task.Delay(10);
        }

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(3));
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var replay = await api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(ticket)}",
            HttpCompletionOption.ResponseHeadersRead);
        await (replay.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
        sessions.Revoke(session.Token);
    }

    [Test]
    public async Task IssuingSseTicketDoesNotRenewTheOwningSession()
    {
        using var directory = new TemporaryDirectory();
        var startedAt = DateTimeOffset.Parse("2026-03-10T00:00:00Z");
        var time = new ManualTimeProvider(startedAt);
        using var sessions = new WebSessionService(time);
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var session = sessions.CreateSession();
        time.Advance(TimeSpan.FromDays(6));

        _ = await GetSseTicketAsync(api, session.Token);

        await sessions.TryValidateWithoutRenewal(session.Token, out var snapshot).Should().BeTrue();
        await (snapshot!.LastSeenAt == startedAt).Should().BeTrue();
        await (snapshot.ExpiresAt == session.ExpiresAt).Should().BeTrue();
    }

    [Test]
    public async Task RevokingAnActiveSseSessionClosesItsStream()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var session = sessions.CreateSession();
        var sseTicket = await GetSseTicketAsync(api, session.Token);

        var responseTask = api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(sseTicket)}",
            HttpCompletionOption.ResponseHeadersRead);
        for (var attempt = 0; attempt < 100 && !responseTask.IsCompleted; attempt++)
        {
            api.Events.Publish("status", new { ready = true });
            await Task.Delay(10);
        }

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(3));
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var firstRead = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await (firstRead > 0).Should().BeTrue();

        sessions.Revoke(session.Token);
        var closed = await ReadSseStreamUntilClosedAsync(stream);
        await closed.Should().BeTrue();
    }

    [Test]
    public async Task ExpiringAnActiveSseSessionClosesItsStream()
    {
        using var directory = new TemporaryDirectory();
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var session = sessions.CreateSession();
        var sseTicket = await GetSseTicketAsync(api, session.Token);
        var responseTask = api.Client.GetAsync(
            $"/api/events?sse_ticket={Uri.EscapeDataString(sseTicket)}",
            HttpCompletionOption.ResponseHeadersRead);
        for (var attempt = 0; attempt < 100 && !responseTask.IsCompleted; attempt++)
        {
            api.Events.Publish("status", new { ready = true });
            await Task.Delay(10);
        }

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(3));
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var firstRead = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await (firstRead > 0).Should().BeTrue();

        time.Advance(WebSessionService.SlidingLifetime + TimeSpan.FromSeconds(1));
        await sessions.TryValidateWithoutRenewal(session.Token, out _).Should().BeFalse();
        var closed = await ReadSseStreamUntilClosedAsync(stream);
        await closed.Should().BeTrue();
    }

    [Test]
    public async Task AuthenticatedRestRequestRenewsSessionAfterSixDays()
    {
        using var directory = new TemporaryDirectory();
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var session = sessions.CreateSession();
        time.Advance(TimeSpan.FromDays(6));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/session");
        request.Headers.Authorization = new("Bearer", session.Token);
        using var response = await api.Client.SendAsync(request);

        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(session.Token, out var renewed).Should().BeTrue();
        await (renewed!.LastSeenAt == time.GetUtcNow()).Should().BeTrue();
        await (renewed.ExpiresAt == time.GetUtcNow() + WebSessionService.SlidingLifetime).Should().BeTrue();
    }

    [Test]
    public async Task LoginRejectsOverlongKeyUniformlyAndAcceptsMaximumLengthKey()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        var maximumLengthKey = new string('k', WebAuthService.MaximumKeyLength);
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), maximumLengthKey),
            sessions);

        using var valid = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = maximumLengthKey });
        using var overlong = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = maximumLengthKey + "k" });
        using var empty = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = string.Empty });
        using var missing = await api.Client.PostAsJsonAsync("/api/auth/login", new { key = (string?)null });

        await (valid.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await (overlong.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
        await (empty.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
        await (missing.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();
    }

    [Test]
    public async Task OversizedLoginAndSetupBodiesAreRejectedBeforeJsonBinding()
    {
        using var directory = new TemporaryDirectory();
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(
            new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), ManagementKey),
            sessions);
        var oversizedJson = new string(' ', checked((int)WebAuthRequestBodyLimitMiddleware.MaximumRequestBodyBytes + 1));

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(oversizedJson, Encoding.UTF8, "application/json"),
        };
        loginRequest.Headers.ConnectionClose = true;
        using var login = await api.Client.SendAsync(loginRequest);
        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/setup")
        {
            Content = new StringContent(oversizedJson, Encoding.UTF8, "application/json"),
        };
        setupRequest.Headers.ConnectionClose = true;
        using var setup = await api.Client.SendAsync(setupRequest);

        await (login.StatusCode == HttpStatusCode.RequestEntityTooLarge).Should().BeTrue();
        await (setup.StatusCode == HttpStatusCode.RequestEntityTooLarge).Should().BeTrue();
    }

    [Test]
    public async Task SseHeartbeatDoesNotRenewSessionAndExpiryCancelsItsRevocationToken()
    {
        var startedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var time = new ManualTimeProvider(startedAt);
        using var sessions = new WebSessionService(time);
        var session = sessions.CreateSession();
        await sessions.TryValidateAndRenew(session.Token, out var snapshot).Should().BeTrue();
        var context = new DefaultHttpContext();
        context.Response.Body = Stream.Null;

        for (var minute = 1; minute < 7 * 24 * 60; minute++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            if (!await WebApiEndpoints.TryWriteSseHeartbeatAsync(context, sessions, snapshot!, CancellationToken.None))
            {
                throw new InvalidOperationException("SSE heartbeat expired the session before its sliding deadline.");
            }
        }

        time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        var heartbeatSent = await WebApiEndpoints.TryWriteSseHeartbeatAsync(
            context,
            sessions,
            snapshot!,
            CancellationToken.None);

        await heartbeatSent.Should().BeFalse();
        await (snapshot!.LastSeenAt == startedAt).Should().BeTrue();
        await snapshot.RevocationToken.IsCancellationRequested.Should().BeTrue();
    }

    private static async Task<bool> ReadSseStreamUntilClosedAsync(Stream stream)
    {
        try
        {
            // A network read may return only part of already-buffered SSE frames.
            await stream.CopyToAsync(Stream.Null).WaitAsync(TimeSpan.FromSeconds(3));
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static async Task<string> GetSseTicketAsync(ApiHarness api, string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sse-ticket");
        request.Headers.Authorization = new("Bearer", sessionToken);
        using var response = await api.Client.SendAsync(request);
        await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("ticket").GetString()!;
    }

    private sealed class ApiHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ApiHarness(WebApplication app, HttpClient client, EventHub events)
        {
            _app = app;
            Client = client;
            Events = events;
        }

        public HttpClient Client { get; }
        public EventHub Events { get; }

        public static async Task<ApiHarness> StartAsync(WebAuthService auth, WebSessionService sessions)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
            builder.Services.AddSingleton(auth);
            builder.Services.AddSingleton(sessions);
            builder.Services.AddSingleton<EventHub>();
            builder.Services.AddSingleton<LogBuffer>();
            builder.Services.AddSingleton<RuntimeOperationCoordinator>();
            builder.Services.AddSingleton<V2rayRuntime>();
            builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);

            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail("internal_error", ApiMessageKeys.CommonInternal));
                }
            });
            app.UseRouting();
            app.UseRateLimiter();
            app.UseMiddleware<WebAuthRequestBodyLimitMiddleware>();
            app.UseMiddleware<WebSessionAuthenticationMiddleware>();
            app.MapWebApi();
            app.MapWebAuthEndpoints();
            app.MapWebSetupEndpoints();
            app.MapGet("/api/test/session", () => Results.Ok(new { authorized = true }));
            app.MapGet("/api/test/runtime-failure", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses;
            var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri(addresses.Single()),
                Timeout = TimeSpan.FromSeconds(5),
            };
            return new ApiHarness(app, client, app.Services.GetRequiredService<EventHub>());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-auth-api-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _now = initialTime;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }
}
