using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Hosting;
using v2rayN.WebAPI.Security;

namespace v2rayN.WebAPI.Tests;

public class ReplaceableWebUiHostingTests
{
    [Test]
    public async Task MissingOrEmptyWebUiDoesNotPreventApiStartup()
    {
        using var directory = new TemporaryDirectory();
        var missingRoot = Path.Combine(directory.Path, "missing");
        await using (var missing = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, "missing")))
        {
            using var response = await missing.Client.GetAsync("/api/status");
            await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
            await (await response.Content.ReadAsStringAsync()).Should().BeEqualTo("API_STATUS");
            using var root = await missing.Client.GetAsync("/");
            await (await root.Content.ReadAsStringAsync()).Should().Contain("No WebUI is installed.");
        }

        Directory.CreateDirectory(missingRoot);
        await using (var empty = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, "missing")))
        {
            using var response = await empty.Client.GetAsync("/api/status");
            await (response.StatusCode == HttpStatusCode.OK).Should().BeTrue();
            using var root = await empty.Client.GetAsync("/");
            await (await root.Content.ReadAsStringAsync()).Should().Contain("No WebUI is installed.");
        }
    }

    [Test]
    public async Task FolderWithoutIndexAndExplicitlyDisabledUiRemainApiOnly()
    {
        using var directory = new TemporaryDirectory();
        var incompleteRoot = Path.Combine(directory.Path, "incomplete");
        Directory.CreateDirectory(incompleteRoot);
        await File.WriteAllTextAsync(Path.Combine(incompleteRoot, "orphan.js"), "window.orphan = true;");

        await using var incomplete = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, "incomplete"));
        using var page = await incomplete.Client.GetAsync("/settings");
        await (page.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
        using var api = await incomplete.Client.GetAsync("/api/status");
        await (api.StatusCode == HttpStatusCode.OK).Should().BeTrue();

        await using var disabled = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, string.Empty));
        using var root = await disabled.Client.GetAsync("/");
        await (await root.Content.ReadAsStringAsync()).Should().Contain("No WebUI is installed.");
        using var disabledApi = await disabled.Client.GetAsync("/api/status");
        await (disabledApi.StatusCode == HttpStatusCode.OK).Should().BeTrue();
    }

    [Test]
    public async Task ThirdPartyUiServesIndexAssetsAndSpaRoutesWithoutShadowingApi()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "third-party-ui");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "api"));
        await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "THIRD_PARTY_UI_TEST");
        await File.WriteAllTextAsync(Path.Combine(root, "assets", "test.js"), "window.thirdPartyUi = true;");
        await File.WriteAllTextAsync(Path.Combine(root, "api", "status"), "STATIC_FILE_MUST_NOT_SHADOW_API");

        await using var api = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, root));

        using var home = await api.Client.GetAsync("/");
        await (await home.Content.ReadAsStringAsync()).Should().BeEqualTo("THIRD_PARTY_UI_TEST");
        using var asset = await api.Client.GetAsync("/assets/test.js");
        await (await asset.Content.ReadAsStringAsync()).Should().BeEqualTo("window.thirdPartyUi = true;");
        using var spaRoute = await api.Client.GetAsync("/settings/subscriptions");
        await (await spaRoute.Content.ReadAsStringAsync()).Should().BeEqualTo("THIRD_PARTY_UI_TEST");

        using var missingAsset = await api.Client.GetAsync("/assets/not-found.js");
        await (missingAsset.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
        await (await missingAsset.Content.ReadAsStringAsync()).Should().NotContain("THIRD_PARTY_UI_TEST");

        using var status = await api.Client.GetAsync("/api/status");
        await (await status.Content.ReadAsStringAsync()).Should().BeEqualTo("API_STATUS");
        using var missingApi = await api.Client.GetAsync("/api/not-found");
        await (missingApi.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
        await (await missingApi.Content.ReadAsStringAsync()).Should().Contain("route_not_found");
        await (await missingApi.Content.ReadAsStringAsync()).Should().NotContain("THIRD_PARTY_UI_TEST");

        using var head = await api.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/settings"));
        await (head.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await (await head.Content.ReadAsByteArrayAsync()).Length.Should().BeEqualTo(0);
        using var post = await api.Client.PostAsync("/settings", new StringContent("ignored"));
        await (post.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
    }

    [Test]
    public async Task StaticUiIsPublicButApiAuthenticationRemainsRequired()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "ui");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "PUBLIC_UI");
        using var sessions = new WebSessionService();
        await using var api = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, root), sessions);

        using var page = await api.Client.GetAsync("/");
        await (page.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await (await page.Content.ReadAsStringAsync()).Should().BeEqualTo("PUBLIC_UI");

        using var anonymousApiRequest = await api.Client.GetAsync("/api/status");
        await (anonymousApiRequest.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue();

        var session = sessions.CreateSession();
        using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        authenticatedRequest.Headers.Authorization = new("Bearer", session.Token);
        using var authenticatedApi = await api.Client.SendAsync(authenticatedRequest);
        await (authenticatedApi.StatusCode == HttpStatusCode.OK).Should().BeTrue();
        await (await authenticatedApi.Content.ReadAsStringAsync()).Should().BeEqualTo("API_STATUS");
    }

    [Test]
    public async Task HostShutsDownGracefullyWithStaticProviderInstalled()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "ui"));
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "ui", "index.html"), "UI");
        await using var api = await ApiHarness.StartAsync(WebUiHostOptions.Resolve(directory.Path, "ui"));

        await api.StopAsync();

        await api.ApplicationStopped.Should().BeTrue();
    }

    private sealed class ApiHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ApiHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }
        public bool ApplicationStopped => _app.Lifetime.ApplicationStopped.IsCancellationRequested;

        public static async Task<ApiHarness> StartAsync(WebUiHostOptions options, WebSessionService? sessions = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            if (sessions is not null) builder.Services.AddSingleton(sessions);

            var app = builder.Build();
            app.UseReplaceableWebUi(options);
            app.UseRouting();
            if (sessions is not null) app.UseMiddleware<WebSessionAuthenticationMiddleware>();
            app.MapGet("/api/status", () => Results.Text("API_STATUS"));
            app.MapFallback("/api/{**path}", () => Results.NotFound(
                ApiEnvelope<object>.Fail("route_not_found", ApiMessageKeys.CommonRouteNotFound)));
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromSeconds(5),
            };
            return new ApiHarness(app, client);
        }

        public Task StopAsync() => _app.StopAsync();

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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-webui-host-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
