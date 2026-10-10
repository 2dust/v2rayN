using ServiceLib;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class ParityValidationTests
{
    [Test]
    public async Task RestoredSubscriptionSelectionIsPreservedOnlyWhenItsSubItemExists()
    {
        var available = new[] { "subscription-a", "subscription-b" };
        await V2rayRuntime.NormalizeSelectedSubscriptionId("subscription-a", available).Should().BeEqualTo("subscription-a");
        await V2rayRuntime.NormalizeSelectedSubscriptionId("deleted-subscription", available).Should().BeEqualTo(string.Empty);
        await V2rayRuntime.NormalizeSelectedSubscriptionId(string.Empty, available).Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task XrayShadowsocksPlainIsAcceptedAndSavedByServiceLib()
    {
        var profile = CreateShadowsocks(ECoreType.Xray, "plain");

        var valid = V2rayRuntime.TryValidateProfile(profile, out var code, out _);
        await valid.Should().BeTrue();
        await code.Should().BeEqualTo("ok");
        await profile.IsValid().Should().BeFalse();

        var result = await ConfigHandler.AddShadowsocksServer(new Config(), profile, toFile: false);

        await result.Should().BeEqualTo(0);
        await profile.ConfigVersion.Should().BeEqualTo(4);
    }

    [Test]
    public async Task SingboxShadowsocksMethodIsAcceptedAndSavedByServiceLib()
    {
        var method = "aes-128-gcm";
        var profile = CreateShadowsocks(ECoreType.sing_box, method);

        var valid = V2rayRuntime.TryValidateProfile(profile, out var code, out _);
        await valid.Should().BeTrue();
        await code.Should().BeEqualTo("ok");
        await profile.IsValid().Should().BeTrue();

        var result = await ConfigHandler.AddShadowsocksServer(new Config(), profile, toFile: false);

        await result.Should().BeEqualTo(0);
    }

    [Test]
    public async Task InvalidShadowsocksMethodIsLeftForServiceLibToReject()
    {
        var profile = CreateShadowsocks(ECoreType.Xray, "not-a-method");

        var valid = V2rayRuntime.TryValidateProfile(profile, out _, out _);
        await valid.Should().BeTrue();

        var result = await ConfigHandler.AddShadowsocksServer(new Config(), profile, toFile: false);

        await result.Should().BeEqualTo(-1);
    }

    [Test]
    public async Task ExistingProfileConfigTypeCannotChange()
    {
        var existing = CreateShadowsocks(ECoreType.Xray, "plain");
        var unchanged = CreateShadowsocks(ECoreType.Xray, "plain");
        var converted = new ProfileItem { ConfigType = EConfigType.VLESS };

        await V2rayRuntime.IsConfigTypeUnchanged(unchanged, existing).Should().BeTrue();
        await V2rayRuntime.IsConfigTypeUnchanged(converted, existing).Should().BeFalse();
    }

    [Test]
    public async Task HttpHeadersJsonUsesServiceLibParserOnlyForHttpProfiles()
    {
        var validHttp = CreateProfile(EConfigType.HTTP);
        validHttp.SetProtocolExtra(new ProtocolExtraItem { HttpHeaders = "{\"X-Test\":\"value\"}" });
        var emptyHttp = CreateProfile(EConfigType.HTTP);
        emptyHttp.SetProtocolExtra(new ProtocolExtraItem { HttpHeaders = string.Empty });
        var invalidHttp = CreateProfile(EConfigType.HTTP);
        invalidHttp.SetProtocolExtra(new ProtocolExtraItem { HttpHeaders = "{invalid" });
        var invalidNonHttp = CreateProfile(EConfigType.SOCKS);
        invalidNonHttp.SetProtocolExtra(new ProtocolExtraItem { HttpHeaders = "{invalid" });
        var originalInvalidHeaders = invalidHttp.ProtoExtra;

        await V2rayRuntime.TryValidateProfile(validHttp, out _, out _).Should().BeTrue();
        await V2rayRuntime.TryValidateProfile(emptyHttp, out _, out _).Should().BeTrue();
        await V2rayRuntime.TryValidateProfile(invalidHttp, out var code, out _).Should().BeFalse();
        await code.Should().BeEqualTo("profile_http_headers_invalid");
        await (invalidHttp.ProtoExtra == originalInvalidHeaders).Should().BeTrue();
        await V2rayRuntime.TryValidateProfile(invalidNonHttp, out _, out _).Should().BeTrue();
    }

    [Test]
    public async Task WebCoreMappingsAreLimitedToUpstreamEditableTypes()
    {
        var editableTypes = Enum.GetValues<EConfigType>()
            .Where(V2rayRuntime.IsWebEditableCoreType)
            .ToArray();
        var expected = new[]
        {
            EConfigType.VMess,
            EConfigType.Custom,
            EConfigType.Shadowsocks,
            EConfigType.SOCKS,
            EConfigType.VLESS,
            EConfigType.Trojan,
            EConfigType.Hysteria2,
            EConfigType.WireGuard,
        };
        await (editableTypes.SequenceEqual(expected)).Should().BeTrue();

        var validMappings = expected.Select(configType => new CoreTypeMapping(configType, ECoreType.Xray)).ToArray();
        await V2rayRuntime.AreWebCoreTypeMappingsValid(validMappings).Should().BeTrue();
        foreach (var configType in new[]
                 {
                     EConfigType.TUIC, EConfigType.HTTP, EConfigType.Anytls, EConfigType.Naive,
                     EConfigType.Outbound, EConfigType.PolicyGroup, EConfigType.ProxyChain,
                 })
        {
            await V2rayRuntime.AreWebCoreTypeMappingsValid([new(configType, ECoreType.Xray)]).Should().BeFalse();
        }
    }

    [Test]
    public async Task FixedCoreSettingsOptionsRejectUnknownValuesAndAllowEmptyValues()
    {
        await V2rayRuntime.AreCoreSettingsOptionsValid(CreateCoreSettings(
            fingerprint: "chrome", userAgent: "chrome", mux4SboxProtocol: "h2mux",
            xudpProxyUDP443: "reject", fragmentPackets: "tlshello")).Should().BeTrue();
        await V2rayRuntime.AreCoreSettingsOptionsValid(CreateCoreSettings(
            fingerprint: string.Empty, userAgent: string.Empty, mux4SboxProtocol: string.Empty,
            xudpProxyUDP443: null, fragmentPackets: null)).Should().BeTrue();
        await V2rayRuntime.AreCoreSettingsOptionsValid(CreateCoreSettings(
            fingerprint: "random-user-value", userAgent: "random-user-value", mux4SboxProtocol: "random-user-value",
            xudpProxyUDP443: "random-user-value", fragmentPackets: "random-user-value")).Should().BeFalse();
    }

    [Test]
    public async Task DestOverrideOnlyAcceptsUpstreamProtocols()
    {
        await V2rayRuntime.AreDestOverrideProtocolsValid(["http", "tls"]).Should().BeTrue();
        await V2rayRuntime.AreDestOverrideProtocolsValid([]).Should().BeTrue();
        await V2rayRuntime.AreDestOverrideProtocolsValid(null).Should().BeTrue();
        await V2rayRuntime.AreDestOverrideProtocolsValid(["http", "arbitrary"]).Should().BeFalse();
    }

    [Test]
    public async Task RootCertificateProviderAcceptsOnlyDesktopOptionsOrAnEmptyValue()
    {
        await V2rayRuntime.IsRootCertProviderValid(null).Should().BeTrue();
        await V2rayRuntime.IsRootCertProviderValid(string.Empty).Should().BeTrue();
        foreach (var provider in Global.RootCertProviders)
        {
            await V2rayRuntime.IsRootCertProviderValid(provider).Should().BeTrue();
        }
        await V2rayRuntime.IsRootCertProviderValid("not-a-desktop-provider").Should().BeFalse();
    }

    [Test]
    public async Task SettingsApiDoesNotInventDesktopEditorFieldsForHiddenServiceLibValues()
    {
        await (typeof(CoreSettingsView).GetProperty("Mux4SboxMaxConnections") is null).Should().BeTrue();
        await (typeof(CoreSettingsInput).GetProperty("Mux4SboxMaxConnections") is null).Should().BeTrue();
        await (typeof(SpeedTestSettingsView).GetProperty("SpeedTestPageSize") is null).Should().BeTrue();
        await (typeof(SpeedTestSettingsInput).GetProperty("SpeedTestDelayInterval") is null).Should().BeTrue();
    }

    [Test]
    public async Task RoutingProfileStrategiesUseTheTwoUpstreamOptionSets()
    {
        foreach (var strategy in new[] { string.Empty, "AsIs", "IPIfNonMatch", "IPOnDemand" })
        {
            await V2rayRuntime.AreRoutingProfileStrategiesValid(strategy, string.Empty).Should().BeTrue();
        }
        foreach (var strategy in new[] { "", "prefer_ipv4", "prefer_ipv6", "ipv4_only", "ipv6_only" })
        {
            await V2rayRuntime.AreRoutingProfileStrategiesValid("AsIs", strategy).Should().BeTrue();
        }
        await V2rayRuntime.AreRoutingProfileStrategiesValid("UseIP", "random-value").Should().BeFalse();
    }

    [Test]
    public async Task FullConfigTemplateTunConfigCanBeEditedSavedAndReloadedWithoutLoss()
    {
        var current = new FullConfigTemplateItem
        {
            Id = "template-id",
            Remarks = "custom template",
            Enabled = true,
            CoreType = ECoreType.Xray,
            Config = "main-template",
            TunConfig = "old-tun-template",
            AddProxyOnly = true,
            ProxyDetour = "upstream",
        };
        var saved = V2rayRuntime.ApplyCoreConfigTemplateInput(current, ECoreType.Xray,
            new CoreConfigTemplateInput(current.Remarks, current.Enabled, current.Config, "new-tun-template", current.AddProxyOnly, current.ProxyDetour));
        var reloaded = V2rayRuntime.ToCoreConfigTemplateView(saved);

        await reloaded.Config.Should().BeEqualTo("main-template");
        await reloaded.TunConfig.Should().BeEqualTo("new-tun-template");
        await reloaded.AddProxyOnly.Should().BeEqualTo(true);
        await reloaded.ProxyDetour.Should().BeEqualTo("upstream");
    }

    private static ProfileItem CreateShadowsocks(ECoreType coreType, string method)
    {
        var profile = CreateProfile(EConfigType.Shadowsocks);
        profile.CoreType = coreType;
        profile.Password = "password";
        profile.ConfigVersion = 3;
        profile.SetProtocolExtra(new ProtocolExtraItem { SsMethod = method });
        return profile;
    }

    private static ProfileItem CreateProfile(EConfigType configType) => new()
    {
        ConfigType = configType,
        Remarks = "test profile",
        Address = "example.test",
        Port = 443,
    };

    private static CoreSettingsInput CreateCoreSettings(
        string? fingerprint,
        string? userAgent,
        string? mux4SboxProtocol,
        string? xudpProxyUDP443,
        string? fragmentPackets) => new(
            false, "warning", fingerprint, userAgent, null, null,
            null, null, xudpProxyUDP443, mux4SboxProtocol, null, false,
            0, 0, false, false, fragmentPackets, [], [], null);
}
