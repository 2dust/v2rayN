using ServiceLib.Services.CoreConfig;
using System.Text.Json.Nodes;

namespace ServiceLib.Tests.CoreConfig;

public class PsiphonConfigTests
{
    [Test]
    [Arguments("only")]
    [Arguments("after")]
    public async Task FailedPsiphonFallsBackWithoutDisablingTun(string mode)
    {
        var config = new ServiceLib.Models.Configs.Config
        {
            IndexId = "active-profile",
            PsiphonMode = mode,
            TunModeItem = new() { EnableTun = true },
        };

        await PsiphonConfigService.DisableFailedMode(config).Should().BeTrue();
        await config.PsiphonMode.Should().BeEqualTo("off");
        await config.TunModeItem.EnableTun.Should().BeTrue();
        await config.IndexId.Should().BeEqualTo("active-profile");
    }

    [Test]
    public async Task NormalTunProtection_PreservesRoutingAndExistingExclusions()
    {
        const string source = """
            {"inbounds":[{"type":"tun","route_exclude_address":["192.0.2.1/32"]}],
            "route":{"rules":[{"rule_set":["geoip-cn"],"outbound":"direct"}],
            "rule_set":[{"tag":"geoip-cn","type":"local","path":"geoip-cn.srs"}]},
            "http_clients":[{"tag":"download"}]}
            """;
        var result = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source,
            [System.Net.IPAddress.Parse("192.0.2.10")], preserveRuleSets: true))!;
        await result["route"]!["rules"]!.AsArray().Count.Should().BeEqualTo(1);
        await result["route"]!["rule_set"]!.AsArray().Count.Should().BeEqualTo(1);
        await result["inbounds"]![0]!["route_exclude_address"]!.AsArray().Count.Should().BeEqualTo(2);
        await (result["http_clients"] != null).Should().BeTrue();
    }

    [Test]
    public async Task OverlayMode_PersistsWithoutReplacingActiveProfile()
    {
        var config = new ServiceLib.Models.Configs.Config
        {
            IndexId = "active-profile",
            PsiphonMode = "after",
            PsiphonProfileId = "internal-psiphon",
        };
        var restored = ServiceLib.Common.JsonUtils.Deserialize<ServiceLib.Models.Configs.Config>(
            ServiceLib.Common.JsonUtils.Serialize(config))!;
        await restored.IndexId.Should().BeEqualTo("active-profile");
        await restored.PsiphonMode.Should().BeEqualTo("after");
        await restored.PsiphonProfileId.Should().BeEqualTo("internal-psiphon");
    }

    [Test]
    public async Task TunFrontend_DoesNotDownloadRulesThroughUnreadyTunnel()
    {
        const string source = """
            {"dns":{"rules":[{"rule_set":["geosite-google"],"server":"remote"},{"server":"local"}]},
             "route":{"rules":[{"rule_set":["geoip-cn"],"outbound":"direct"},{"outbound":"proxy"}],
             "rule_set":[{"tag":"geoip-cn","type":"remote"}]},"http_clients":[{"tag":"download"}]}
            """;
        var result = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source))!;
        await result["route"]!["rule_set"]!.AsArray().Count.Should().BeEqualTo(0);
        await result["route"]!["rules"]!.AsArray().Count.Should().BeEqualTo(1);
        await result["dns"]!["rules"]!.AsArray().Count.Should().BeEqualTo(1);
        await (result["http_clients"] == null).Should().BeTrue();
    }

    [Test]
    public async Task TunFrontend_BypassesCoreDnsBeforeDnsHijacking()
    {
        const string source = """
            {"route":{"rules":[
              {"port":[53],"process_path":["xray.exe"],"action":"hijack-dns"},
              {"outbound":"direct","process_path":["xray.exe","psiphon.exe"]},
              {"outbound":"proxy"}]}}
            """;
        var rules = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source))!
            ["route"]!["rules"]!.AsArray();
        await rules[0]!["outbound"]!.GetValue<string>().Should().BeEqualTo("direct");
        await rules[1]!["outbound"]!.GetValue<string>().Should().BeEqualTo("direct");
        await rules[1]!["process_path"]!.AsArray().Count.Should().BeEqualTo(2);
        await (rules[0]!["process_path"] == null).Should().BeTrue();
        await (rules[1]!["process_name"] == null).Should().BeTrue();
        await rules[0]!["process_name"]![0]!.GetValue<string>().Should().BeEqualTo("xray.exe");
    }

    [Test]
    public async Task TunFrontend_ExcludesResolvedUpstreamFromWindowsTunRoute()
    {
        const string source = """{"inbounds":[{"type":"tun","auto_route":true}]}""";
        var result = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source,
            [System.Net.IPAddress.Parse("192.0.2.10"), System.Net.IPAddress.Parse("2001:db8::10")]))!;
        var excluded = result["inbounds"]![0]!["route_exclude_address"]!.AsArray();
        await excluded[0]!.GetValue<string>().Should().BeEqualTo("192.0.2.10/32");
        await excluded[1]!.GetValue<string>().Should().BeEqualTo("2001:db8::10/128");
    }

    [Test]
    public async Task RegionList_ContainsOnlyNetworkReportedFallbacks()
    {
        await PsiphonConfigService.Regions.Any(item => item.Code == "DE").Should().BeTrue();
        await PsiphonConfigService.Regions.Any(item => item.Code == "UZ").Should().BeFalse();
        await PsiphonConfigService.Regions.Any(item => item.Code == "AF").Should().BeFalse();
    }

    [Test]
    public async Task PortSelection_RejectsOccupiedPortAndReturnsBindableAlternative()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var occupied = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            await ServiceLib.Common.Utils.CanBindTcpPort(occupied).Should().BeFalse();
            var alternative = ServiceLib.Common.Utils.GetFreePort(occupied);
            await ServiceLib.Common.Utils.CanBindTcpPort(alternative).Should().BeTrue();
            await ServiceLib.Common.Utils.CanBindTcpPort(65536).Should().BeFalse();
        }
        finally { listener.Stop(); }
    }

    private const string Source = """
        {"SponsorId":"FFFFFFFFFFFFFFFF","PropagationChannelId":"FFFFFFFFFFFFFFFF",
        "EgressRegion":"DE","UpstreamProxyURL":"socks5://old.example:9000",
        "RemoteServerListUrl":"https://example.com/list","ListenInterface":"any",
        "EnableLightProxyFallback":true,"SplitTunnelOwnRegion":true}
        """;

    [Test]
    public async Task DirectMode_RemovesImportedUpstreamAndBindsOnlyLoopback()
    {
        var config = PsiphonConfigService.CreateConfig(Source, "US", 21088, "test-data");
        await config.ContainsKey("UpstreamProxyURL").Should().BeFalse();
        await config.ContainsKey("UpstreamProxyUrl").Should().BeFalse();
        await config["ListenInterface"]!.GetValue<string>().Should().BeEqualTo("");
        await config["EgressRegion"]!.GetValue<string>().Should().BeEqualTo("US");
        await config["EnableLightProxyFallback"]!.GetValue<bool>().Should().BeFalse();
        await config["SplitTunnelOwnRegion"]!.GetValue<bool>().Should().BeFalse();
    }

    [Test]
    public async Task ChainedMode_UsesOnlyManagedUpstreamAndKeepsBootstrap()
    {
        var config = PsiphonConfigService.CreateConfig(Source, "DE", 21088, "test-data", 21089);
        await config["UpstreamProxyUrl"]!.GetValue<string>().Should().BeEqualTo("socks5://127.0.0.1:21089");
        await config["RemoteServerListUrl"]!.GetValue<string>().Should().BeEqualTo("https://example.com/list");
        await config["LocalSocksProxyPort"]!.GetValue<int>().Should().BeEqualTo(21088);
    }

    [Test]
    public async Task Automatic_ClearsImportedCountry_WhileNullPreservesIt()
    {
        await PsiphonConfigService.CreateConfig(Source, "", 21088, "data")["EgressRegion"]!.GetValue<string>().Should().BeEqualTo("");
        await PsiphonConfigService.CreateConfig(Source, null, 21088, "data")["EgressRegion"]!.GetValue<string>().Should().BeEqualTo("DE");
    }

    [Test]
    [Arguments(0)]
    [Arguments(65536)]
    [Arguments(-1)]
    public async Task InvalidPort_IsRejected(int port)
    {
        var rejected = false;
        try { PsiphonConfigService.CreateConfig(Source, "", port, "data"); }
        catch (ArgumentException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task LoopingUpstream_IsRejected()
    {
        var rejected = false;
        try { PsiphonConfigService.CreateConfig(Source, "", 21088, "data", 21088); }
        catch (ArgumentException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task EmbeddedBootstrap_IsUsableAndHasServerListVerificationKey()
    {
        var source = ServiceLib.Common.EmbedUtils.GetEmbedText("ServiceLib.Sample.psiphon_default");
        var config = PsiphonConfigService.CreateConfig(source, "", 21088, "data");
        await string.IsNullOrWhiteSpace(config["RemoteServerListSignaturePublicKey"]?.GetValue<string>()).Should().BeFalse();
        await new Uri(config["RemoteServerListUrl"]!.GetValue<string>()).Scheme.Should().BeEqualTo("https");
    }

    [Test]
    public async Task TunFrontend_PinsUpstreamHostsSoReconnectDoesNotNeedTheTunnel()
    {
        const string source = """
            {"inbounds":[{"type":"tun"}],
            "dns":{"servers":[{"type":"https","tag":"remote","server":"cloudflare-dns.com","detour":"proxy"}],
            "rules":[{"server":"remote"}],"final":"remote"},"route":{"rules":[]}}
            """;
        var address = System.Net.IPAddress.Parse("192.0.2.20");
        var result = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source, [address],
            pinnedHosts: new Dictionary<string, System.Net.IPAddress[]> { ["edge.example.workers.dev"] = [address], ["192.0.2.30"] = [address] }))!;
        var rule = result["dns"]!["rules"]![0]!;
        await rule["server"]!.GetValue<string>().Should().BeEqualTo(PsiphonConfigService.PinnedHostsTag);
        await rule["domain"]!.AsArray().Count.Should().BeEqualTo(1);
        var hosts = result["dns"]!["servers"]!.AsArray().Last()!;
        await hosts["type"]!.GetValue<string>().Should().BeEqualTo("hosts");
        await hosts["predefined"]!["edge.example.workers.dev"]![0]!.GetValue<string>().Should().BeEqualTo("192.0.2.20");
    }

    [Test]
    public async Task TunFrontend_WithoutPinnedHostsLeavesDnsUnchanged()
    {
        const string source = """{"inbounds":[{"type":"tun"}],"dns":{"servers":[],"rules":[]},"route":{"rules":[]}}""";
        var result = JsonNode.Parse(PsiphonConfigService.SimplifyTunFrontend(source))!;
        await result["dns"]!["rules"]!.AsArray().Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Config_AddsMobileFallbackResolvers()
    {
        var config = PsiphonConfigService.CreateConfig(Source, "", 21088, "data", 21089);
        await config["DNSResolverAlternateServers"]!.AsArray().Count.Should().BeEqualTo(4);
    }

    [Test]
    public async Task Watchdog_LetsPsiphonReconnectByItself()
    {
        var now = DateTime.UtcNow;
        // Tunnel just dropped but the engine is alive and its proxy answers: wait.
        await ServiceLib.Manager.CoreManager.PsiphonNeedsRestart(true, true, 0, now.AddSeconds(-30), now).Should().BeFalse();
        // Stalled beyond the grace period: restart.
        await ServiceLib.Manager.CoreManager.PsiphonNeedsRestart(true, true, 0, now.AddSeconds(-120), now).Should().BeTrue();
        // A crashed engine, upstream or TUN frontend: restart at once.
        await ServiceLib.Manager.CoreManager.PsiphonNeedsRestart(false, false, 1, null, now).Should().BeTrue();
        // Local proxy stopped answering twice: restart.
        await ServiceLib.Manager.CoreManager.PsiphonNeedsRestart(true, false, 2, null, now).Should().BeTrue();
        await ServiceLib.Manager.CoreManager.PsiphonNeedsRestart(true, false, 1, null, now).Should().BeFalse();
    }
}
