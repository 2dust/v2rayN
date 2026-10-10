using System.Net;
using Microsoft.Extensions.Configuration;
using v2rayN.WebAPI.Launcher;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class WebProbeUriResolverTests
{
    [Test]
    [Arguments("http://127.0.0.1:5080", "http://127.0.0.1:5080/api/health")]
    [Arguments("http://localhost:5080", "http://localhost:5080/api/health")]
    [Arguments("http://[::1]:5080", "http://[::1]:5080/api/health")]
    [Arguments("http://0.0.0.0:5080", "http://127.0.0.1:5080/api/health")]
    [Arguments("http://[::]:5080", "http://[::1]:5080/api/health")]
    [Arguments("http://*:5080", "http://localhost:5080/api/health")]
    [Arguments("http://+:5080", "http://localhost:5080/api/health")]
    [Arguments("http://nas:5080", "http://localhost:5080/api/health")]
    [Arguments("https://127.0.0.1:5443", null)]
    [Arguments("http://192.168.1.20:5080", null)]
    [Arguments("http://[2001:db8::1]:5080", null)]
    [Arguments("not-a-url", null)]
    [Arguments("", null)]
    public async Task EveryListenUrlMapsToItsMatchingLoopbackProbe(string listenUrl, string? expected)
    {
        var resolved = WebProbeUriResolver.TryCreateHealthUri(listenUrl, out var healthUri);

        if (expected is null)
        {
            await resolved.Should().BeFalse();
            return;
        }

        await resolved.Should().BeTrue();
        await healthUri.ToString().Should().BeEqualTo(expected);
    }

    [Test]
    public async Task DefaultConfigurationResolvesToTheDefaultLoopbackProbe()
    {
        await WebProbeUriResolver.TryResolve(Config(), out var endpoints).Should().BeTrue();
        await endpoints.HealthUri.ToString().Should().BeEqualTo("http://127.0.0.1:5080/api/health");
        await endpoints.ApiUri.ToString().Should().BeEqualTo("http://127.0.0.1:5080/");
    }

    [Test]
    public async Task CustomPortsAndHttpPortsResolve()
    {
        await WebProbeUriResolver.TryResolve(Config(("urls", "http://127.0.0.1:5090")), out var custom)
            .Should().BeTrue();
        await custom.HealthUri.ToString().Should().BeEqualTo("http://127.0.0.1:5090/api/health");

        await WebProbeUriResolver.TryResolve(Config(("http_ports", "6001")), out var ports).Should().BeTrue();
        await ports.HealthUri.ToString().Should().BeEqualTo("http://localhost:6001/api/health");
    }

    [Test]
    public async Task Ipv6OnlyAndMixedListenersPickAProbeableEndpoint()
    {
        await WebProbeUriResolver.TryResolve(Config(("urls", "http://[::1]:5090")), out var ipv6).Should().BeTrue();
        await ipv6.HealthUri.ToString().Should().BeEqualTo("http://[::1]:5090/api/health");

        await WebProbeUriResolver.TryResolve(
            Config(("urls", "http://192.168.1.20:5080;http://[::1]:5091")), out var mixed).Should().BeTrue();
        await mixed.HealthUri.ToString().Should().BeEqualTo("http://[::1]:5091/api/health");
    }

    [Test]
    public async Task KestrelEndpointsOverrideHostingUrlsUnlessPreferHostingUrlsIsSet()
    {
        var configuration = Config(
            ("urls", "http://127.0.0.1:5080"),
            ("Kestrel:Endpoints:Http:Url", "http://[::1]:5081"));
        await WebProbeUriResolver.TryResolve(configuration, out var kestrel).Should().BeTrue();
        await kestrel.HealthUri.ToString().Should().BeEqualTo("http://[::1]:5081/api/health");

        configuration["preferHostingUrls"] = "true";
        await WebProbeUriResolver.TryResolve(configuration, out var hosting).Should().BeTrue();
        await hosting.HealthUri.ToString().Should().BeEqualTo("http://127.0.0.1:5080/api/health");
    }

    [Test]
    public async Task HttpsOnlyListenersAreRefusedAndMixedListenersFallBackToHttp()
    {
        await WebProbeUriResolver.TryResolve(Config(("urls", "https://127.0.0.1:5443")), out _).Should().BeFalse();
        await WebProbeUriResolver.TryResolve(Config(("https_ports", "5443")), out _).Should().BeFalse();

        await WebProbeUriResolver.TryResolve(
            Config(("urls", "https://[::1]:5443;http://0.0.0.0:5080")), out var mixed).Should().BeTrue();
        await mixed.HealthUri.ToString().Should().BeEqualTo("http://127.0.0.1:5080/api/health");
    }

    [Test]
    public async Task LauncherUrisFollowTheSameResolutionAndNeverGuess()
    {
        var (health, api) = Program.GetLauncherUris(Config(("urls", "http://[::1]:5090")));
        await health!.ToString().Should().BeEqualTo("http://[::1]:5090/api/health");
        await api!.ToString().Should().BeEqualTo("http://[::1]:5090/");

        // A configured localhost stays localhost instead of being rewritten to 127.0.0.1.
        var (localhostHealth, localhostApi) = Program.GetLauncherUris(Config(("urls", "http://localhost:5090")));
        await localhostHealth!.ToString().Should().BeEqualTo("http://localhost:5090/api/health");
        await localhostApi!.ToString().Should().BeEqualTo("http://localhost:5090/");

        var (httpsHealth, httpsApi) = Program.GetLauncherUris(Config(("urls", "https://127.0.0.1:5443")));
        await (httpsHealth is null).Should().BeTrue();
        await (httpsApi is null).Should().BeTrue();
    }

    [Test]
    [Arguments("http://127.0.0.1:5080/api/health", true)]
    [Arguments("http://localhost:5080/api/health", true)]
    [Arguments("http://[::1]:5080/api/health", true)]
    [Arguments("https://[::1]:5443/api/health", false)]
    [Arguments("http://0.0.0.0:5080/api/health", false)]
    [Arguments("http://192.168.1.20:5080/api/health", false)]
    [Arguments("not-a-uri", false)]
    public async Task UpdatePlansOnlyAcceptLoopbackHttpHealthUris(string healthUri, bool accepted)
    {
        await WebProbeUriResolver.IsProbeableLoopbackUri(healthUri).Should().BeEqualTo(accepted);
    }

    [Test]
    public async Task EveryProducedProbeUriSatisfiesTheSharedLoopbackRule()
    {
        foreach (var listenUrl in new[]
        {
            "http://127.0.0.1:5080", "http://localhost:5080", "http://[::1]:5080",
            "http://0.0.0.0:5080", "http://[::]:5080", "http://*:5080", "http://+:5080",
            "http://nas:5080",
        })
        {
            await WebProbeUriResolver.TryCreateHealthUri(listenUrl, out var healthUri).Should().BeTrue();
            await WebProbeUriResolver.IsProbeableLoopbackUri(healthUri.ToString()).Should().BeTrue();
        }
    }

    [Test]
    public async Task HealthDiagnosticsAndProbesShareTheSameAddressRule()
    {
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Loopback, "127.0.0.1").Should().BeTrue();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Loopback, "localhost").Should().BeTrue();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.IPv6Loopback, "::1").Should().BeTrue();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.IPv6Loopback, "[::1]").Should().BeTrue();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Parse("192.168.1.20"), "127.0.0.1").Should().BeFalse();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Loopback, "192.168.1.20").Should().BeFalse();
        await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Any, "localhost").Should().BeFalse();

        // Every probe host the resolver can produce is accepted by the diagnostics rule.
        foreach (var probeHost in new[] { "localhost", "127.0.0.1", "::1" })
        {
            await WebSetupAccessPolicy.IsLocalDiagnosticSource(IPAddress.Loopback, probeHost).Should().BeTrue();
        }
    }

    [Test]
    public async Task UnprobeableListenerConfigurationRefusesInstallWithAClearReason()
    {
        var unprobeable = new V2rayRuntime(null!, null!, Config(("urls", "https://127.0.0.1:5443")), null!, null!);
        await unprobeable.GetWebUpdateRuntimeInstallReason().Should().BeEqualTo("maintenance.webUpdateProbeUnavailable");

        var nonLoopback = new V2rayRuntime(null!, null!, Config(("urls", "http://192.168.1.20:5080")), null!, null!);
        await nonLoopback.GetWebUpdateRuntimeInstallReason().Should().BeEqualTo("maintenance.webUpdateProbeUnavailable");

        var probeable = new V2rayRuntime(null!, null!, Config(("urls", "http://localhost:5080")), null!, null!);
        await (probeable.GetWebUpdateRuntimeInstallReason() is null).Should().BeTrue();
    }

    private static IConfigurationRoot Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();
}
