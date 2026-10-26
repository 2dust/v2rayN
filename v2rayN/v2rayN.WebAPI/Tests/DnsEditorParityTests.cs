using System.Reflection;
using System.Text.Json.Nodes;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models.Configs;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Models.Entities;
using ServiceLib.Services.CoreConfig;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

[NotInParallel]
public class DnsEditorParityTests
{
    [Test]
    public async Task DnsEditorOptionsAreTheCurrentServiceLibOptions()
    {
        var options = V2rayRuntime.GetEditorOptions();
        var settings = V2rayRuntime.GetWebSettingsOptions();

        await options.Subscriptions.ConvertTargets.SequenceEqual(Global.SubConvertTargets).Should().BeTrue();
        await options.Subscriptions.CustomCoreTypes.SequenceEqual(
            Enum.GetValues<ECoreType>().Where(value => value != ECoreType.v2rayN).Select(value => value.ToString())).Should().BeTrue();
        await options.Profiles.CoreTypes.SequenceEqual(Global.CoreTypes).Should().BeTrue();
        await options.Profiles.ConfigTypes.SequenceEqual(Enum.GetValues<EConfigType>().Select(value => value.ToString())).Should().BeTrue();
        await options.Profiles.SingboxOnlyConfigTypes.ToHashSet().SetEquals(Global.SingboxOnlyConfigType.Select(value => value.ToString())).Should().BeTrue();
        await options.Profiles.Networks.SequenceEqual(Global.Networks).Should().BeTrue();
        await options.Profiles.DefaultNetwork.Should().BeEqualTo(Global.DefaultNetwork);
        await options.Profiles.DefaultStreamSecurity.Should().BeEqualTo(new ProfileItem().StreamSecurity);
        await options.Profiles.DefaultSecurity.Should().BeEqualTo(Global.DefaultSecurity);
        await options.Profiles.DefaultVlessEncryption.Should().BeEqualTo(Global.None);
        await options.Profiles.DefaultXhttpMode.Should().BeEqualTo(Global.DefaultXhttpMode);
        await options.Profiles.DefaultGrpcMode.Should().BeEqualTo(Global.GrpcGunMode);
        await options.Profiles.DefaultWireGuardMtu.Should().BeEqualTo(1280);
        await options.Profiles.VmessSecurities.SequenceEqual(Global.VmessSecurities).Should().BeTrue();
        await options.Profiles.Flows.SequenceEqual(Global.Flows).Should().BeTrue();
        await options.Profiles.XhttpModes.SequenceEqual(Global.XhttpMode).Should().BeTrue();
        await options.Profiles.KcpHeaderTypes.SequenceEqual(new[] { Global.None }.Concat(Global.KcpHeaderTypes)).Should().BeTrue();
        await options.Profiles.TuicCongestionControls.SequenceEqual(Global.TuicCongestionControls).Should().BeTrue();
        await options.Profiles.NaiveCongestionControls.SequenceEqual(Global.NaiveCongestionControls).Should().BeTrue();
        await options.Profiles.Fingerprints.SequenceEqual(Global.Fingerprints).Should().BeTrue();
        await options.Profiles.Alpns.SequenceEqual(Global.Alpns).Should().BeTrue();
        foreach (var coreType in Enum.GetValues<ECoreType>().Where(value => value != ECoreType.v2rayN))
        {
            var expected = coreType switch
            {
                ECoreType.v2fly => Global.SsSecurities,
                ECoreType.Xray => Global.SsSecuritiesInXray,
                _ => Global.SsSecuritiesInSingbox,
            };
            await options.Profiles.ShadowsocksSecurities[coreType.ToString()].SequenceEqual(expected).Should().BeTrue();
        }
        await options.Profiles.MultipleLoadStrategies.ToHashSet().SetEquals(Enum.GetNames<EMultipleLoad>()).Should().BeTrue();
        await settings.LogLevels.SequenceEqual(Global.LogLevels).Should().BeTrue();
        await settings.RootCertProviders.SequenceEqual(Global.RootCertProviders).Should().BeTrue();
        await settings.Fingerprints.SequenceEqual(Global.Fingerprints).Should().BeTrue();
        await settings.UserAgents.SequenceEqual(Global.UserAgent).Should().BeTrue();
        await settings.Mux4SboxProtocols.SequenceEqual(Global.SingboxMuxs).Should().BeTrue();
        await settings.FragmentPacketsOptions.SequenceEqual(Global.FragmentPacketsOptions).Should().BeTrue();
        await settings.DestOverrideProtocols.SequenceEqual(Global.destOverrideProtocols).Should().BeTrue();
        await settings.MixedConcurrencyCounts.SequenceEqual(Enumerable.Range(Global.SpeedTestConcurrencyCountMin, 20)).Should().BeTrue();
        await settings.SpeedTestTimeouts.SequenceEqual(Enumerable.Range(2, 5).Select(value => value * 5)).Should().BeTrue();
        await settings.SpeedTestUrls.SequenceEqual(Global.SpeedTestUrls).Should().BeTrue();
        await settings.SpeedPingTestUrls.SequenceEqual(Global.SpeedPingTestUrls).Should().BeTrue();
        await settings.UdpTestTargets.SequenceEqual(Global.UdpTestTargets).Should().BeTrue();
        await settings.IPAPIUrls.SequenceEqual(Global.IPAPIUrls).Should().BeTrue();
        await settings.SubConvertUrls.SequenceEqual(Global.SubConvertUrls).Should().BeTrue();
        await settings.GeoFilesSources.SequenceEqual(Global.GeoFilesSources).Should().BeTrue();
        await settings.SingboxRulesetSources.SequenceEqual(Global.SingboxRulesetSources).Should().BeTrue();
        await settings.RoutingRulesSources.SequenceEqual(Global.RoutingRulesSources).Should().BeTrue();
        await options.Dns.CoreTypes.SequenceEqual(Global.CoreTypes).Should().BeTrue();
        await options.Dns.DomainStrategies4Freedom.SequenceEqual(Global.DomainStrategy).Should().BeTrue();
        await options.Dns.DomainStrategies4Singbox.SequenceEqual(Global.DomainStrategies4Sbox).Should().BeTrue();
        await options.Dns.DomainDnsAddresses.SequenceEqual(Global.DomainPureIPDNSAddress).Should().BeTrue();
    }

    [Test]
    public async Task DnsDtoAndWebMappingRoundTripAllFieldsIncludingTunDns()
    {
        await (typeof(DnsProfileView).GetProperty(nameof(DnsProfileView.TunDNS)) is not null).Should().BeTrue();
        await (typeof(DnsProfileInput).GetProperty(nameof(DnsProfileInput.TunDNS)) is not null).Should().BeTrue();

        var source = new DNSItem
        {
            Id = "xray-dns",
            Remarks = "original",
            CoreType = ECoreType.Xray,
            Enabled = true,
            UseSystemHosts = true,
            NormalDNS = "original-normal",
            TunDNS = "original-tun",
            DomainStrategy4Freedom = Global.DomainStrategy.First(),
            DomainDNSAddress = Global.DomainPureIPDNSAddress.First(),
        };
        var loaded = V2rayRuntime.ToDnsProfileView(source);
        var saved = V2rayRuntime.ApplyDnsProfileInput(source, ECoreType.Xray, new DnsProfileInput(
            loaded.Remarks,
            loaded.Enabled,
            loaded.UseSystemHosts,
            "edited-normal",
            "edited-tun",
            Global.DomainStrategy[1],
            Global.DomainPureIPDNSAddress[1]));
        var reloaded = V2rayRuntime.ToDnsProfileView(saved);

        await reloaded.Id.Should().BeEqualTo("xray-dns");
        await reloaded.CoreType.Should().BeEqualTo(ECoreType.Xray);
        await reloaded.Remarks.Should().BeEqualTo("original");
        await reloaded.Enabled.Should().BeTrue();
        await reloaded.UseSystemHosts.Should().BeTrue();
        await reloaded.NormalDNS.Should().BeEqualTo("edited-normal");
        await reloaded.TunDNS.Should().BeEqualTo("edited-tun");
        await reloaded.DomainStrategy4Freedom.Should().BeEqualTo(Global.DomainStrategy[1]);
        await reloaded.DomainDNSAddress.Should().BeEqualTo(Global.DomainPureIPDNSAddress[1]);
    }

    [Test]
    public async Task DesktopDefaultDnsImportReadsEmbeddedXrayAndSingboxProfiles()
    {
        var defaults = new V2rayRuntime(null!, null!, null!, null!, null!).GetDefaultDnsProfiles();
        var xray = defaults.Single(item => item.CoreType == ECoreType.Xray);
        var singbox = defaults.Single(item => item.CoreType == ECoreType.sing_box);

        await xray.NormalDNS.Should().BeEqualTo(EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName));
        await xray.TunDNS.Should().BeEqualTo(EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName));
        await singbox.NormalDNS.Should().BeEqualTo(EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName));
        await singbox.TunDNS.Should().BeEqualTo(EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName));
        await string.IsNullOrWhiteSpace(xray.NormalDNS).Should().BeFalse();
        await string.IsNullOrWhiteSpace(singbox.TunDNS).Should().BeFalse();
    }

    [Test]
    public async Task XrayNormalAndTunDnsAreUsedByGeneratedCoreConfigAfterWebMappingRoundTrip()
    {
        var config = SniffingSettingsIntegrationTests.CreateConfig();
        SniffingSettingsIntegrationTests.BindAppManagerConfig(config);
        var initial = Existing(ECoreType.Xray);
        var mapped = V2rayRuntime.ToDnsProfileView(initial);
        var saved = V2rayRuntime.ApplyDnsProfileInput(initial, ECoreType.Xray, new DnsProfileInput(
            mapped.Remarks, true, false,
            "{\"servers\":[\"https://9.9.9.9/dns-query\"]}",
            "{\"servers\":[\"tls://1.0.0.1\"]}",
            Global.AsIs,
            Global.DomainPureIPDNSAddress.First()));
        var reloaded = V2rayRuntime.ToDnsProfileView(saved);

        var normal = Generate(ECoreType.Xray, config, saved, tun: false);
        var tun = Generate(ECoreType.Xray, config, saved, tun: true);
        await reloaded.NormalDNS.Should().BeEqualTo("{\"servers\":[\"https://9.9.9.9/dns-query\"]}");
        await reloaded.TunDNS.Should().BeEqualTo("{\"servers\":[\"tls://1.0.0.1\"]}");
        await normal["dns"]!["servers"]!.ToJsonString().Contains("9.9.9.9").Should().BeTrue();
        await tun["dns"]!["servers"]!.ToJsonString().Contains("1.0.0.1").Should().BeTrue();
    }

    [Test]
    public async Task SingboxNormalAndTunDnsAreUsedByGeneratedCoreConfigAfterWebMappingRoundTrip()
    {
        var config = SniffingSettingsIntegrationTests.CreateConfig();
        SniffingSettingsIntegrationTests.BindAppManagerConfig(config);
        var initial = Existing(ECoreType.sing_box);
        var mapped = V2rayRuntime.ToDnsProfileView(initial);
        var saved = V2rayRuntime.ApplyDnsProfileInput(initial, ECoreType.sing_box, new DnsProfileInput(
            mapped.Remarks, true, false,
            "{\"servers\":[{\"tag\":\"web-normal\",\"type\":\"udp\",\"server\":\"9.9.9.9\"}],\"final\":\"web-normal\"}",
            "{\"servers\":[{\"tag\":\"web-tun\",\"type\":\"udp\",\"server\":\"1.0.0.1\"}],\"final\":\"web-tun\"}",
            Global.DomainStrategies4Sbox.First(),
            Global.DomainPureIPDNSAddress.First()));
        var reloaded = V2rayRuntime.ToDnsProfileView(saved);

        var normal = Generate(ECoreType.sing_box, config, saved, tun: false);
        var tun = Generate(ECoreType.sing_box, config, saved, tun: true);
        await reloaded.NormalDNS.Should().BeEqualTo(saved.NormalDNS);
        await reloaded.TunDNS.Should().BeEqualTo(saved.TunDNS);
        await normal["dns"]!["servers"]!.ToJsonString().Contains("9.9.9.9").Should().BeTrue();
        await tun["dns"]!["servers"]!.ToJsonString().Contains("1.0.0.1").Should().BeTrue();
    }

    [Test]
    public async Task DnsValidationMatchesTheDesktopCoreEditorRules()
    {
        await V2rayRuntime.IsValidCustomDns(ECoreType.Xray, "8.8.8.8,1.1.1.1").Should().BeTrue();
        await V2rayRuntime.IsValidCustomDns(ECoreType.Xray, "{\"servers\":[\"1.1.1.1\"]}").Should().BeTrue();
        await V2rayRuntime.IsValidCustomDns(ECoreType.Xray, "{broken").Should().BeFalse();
        await V2rayRuntime.IsValidCustomDns(ECoreType.sing_box,
            "{\"servers\":[{\"type\":\"udp\",\"server\":\"1.1.1.1\"}]}").Should().BeTrue();
        await V2rayRuntime.IsValidCustomDns(ECoreType.sing_box,
            "{\"servers\":[{\"server\":\"1.1.1.1\"}]}").Should().BeFalse();
    }

    private static DNSItem Existing(ECoreType coreType) => new()
    {
        Id = $"{coreType}-dns",
        Remarks = "custom DNS",
        CoreType = coreType,
        Enabled = true,
        UseSystemHosts = false,
        NormalDNS = "old-normal",
        TunDNS = "old-tun",
        DomainStrategy4Freedom = string.Empty,
        DomainDNSAddress = Global.DomainPureIPDNSAddress.First(),
    };

    private static JsonNode Generate(ECoreType coreType, Config config, DNSItem dns, bool tun)
    {
        var node = new ProfileItem
        {
            IndexId = "dns-parity-node",
            ConfigType = EConfigType.VMess,
            CoreType = coreType,
            Remarks = "DNS parity node",
            Address = "example.test",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = Global.DefaultNetwork,
            StreamSecurity = string.Empty,
            Subid = string.Empty,
        };
        node.SetProtocolExtra(new ProtocolExtraItem { AlterId = "0", VmessSecurity = Global.DefaultSecurity });
        var context = new CoreConfigContext
        {
            Node = node,
            RunCoreType = coreType,
            RoutingItem = new RoutingItem
            {
                Id = "dns-route",
                Remarks = "DNS parity route",
                RuleSet = "[]",
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            },
            RawDnsItem = dns,
            SimpleDnsItem = config.SimpleDNSItem,
            AllProxiesMap = new Dictionary<string, ProfileItem> { [node.IndexId] = node },
            AppConfig = config,
            IsTunEnabled = tun,
            ProtectDomainList = [],
            HasGlobalIPv6Address = true,
        };
        var result = coreType == ECoreType.sing_box
            ? new CoreConfigSingboxService(context).GenerateClientConfigContent()
            : new CoreConfigV2rayService(context).GenerateClientConfigContent();
        if (!result.Success || result.Data is null)
        {
            throw new InvalidOperationException($"Core configuration generation failed: {result.Msg}");
        }
        return JsonNode.Parse(result.Data.ToString()!)!;
    }
}
