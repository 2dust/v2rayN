namespace ServiceLib.Tests.Services;

public class CustomSpeedtestConfigTests
{
    [Test]
    [Arguments(ECoreType.Xray, "protocol", "port")]
    [Arguments(ECoreType.v2fly, "protocol", "port")]
    [Arguments(ECoreType.sing_box, "type", "listen_port")]
    public async Task ChangesOnlyProxyInboundPort(ECoreType core, string typeKey, string portKey)
    {
        var source = $$"""
            {
              "inbounds": [{ "{{typeKey}}": "socks", "listen": "127.0.0.1", "{{portKey}}": 10808, "tag": "entry" }],
              "outbounds": [{ "tag": "selector", "type": "selector", "outbounds": ["proxy", "direct"] }],
              "routing": { "rules": [{ "inboundTag": ["entry"], "outboundTag": "selector" }] },
              "dns": { "hosts": { "example.org": "127.0.0.2" } },
              "customExtension": { "fragment": [1, 2, 3] }
            }
            """;
        var original = JsonUtils.ParseJson(source)!.AsObject();

        var success = CustomSpeedtestConfig.TryChangePort(source, core, 22345, false, out var result, out _);

        await success.Should().BeTrue();
        var modified = JsonUtils.ParseJson(result)!.AsObject();
        await modified["inbounds"]![0]![portKey]!.GetValue<int>().Should().BeEqualTo(22345);
        modified["inbounds"]![0]![portKey] = 10808;
        await modified.ToJsonString().Should().BeEqualTo(original.ToJsonString());
        await original["inbounds"]![0]![portKey]!.GetValue<int>().Should().BeEqualTo(10808);
    }

    [Test]
    [Arguments(ECoreType.mihomo)]
    [Arguments(ECoreType.hysteria2)]
    [Arguments(ECoreType.v2fly_v5)]
    public async Task RejectsOtherCoreFormats(ECoreType core)
    {
        var success = CustomSpeedtestConfig.TryChangePort(RayConfig(), core, 23456, false,
            out var result, out _);

        await success.Should().BeFalse();
        await result.Should().BeNull();
    }

    [Test]
    public async Task RejectsAmbiguousOrAuthenticatedInbound()
    {
        var multipleNode = JsonUtils.ParseJson(RayConfig())!;
        multipleNode["inbounds"]!.AsArray().Add(JsonUtils.ParseJson("""
            {"protocol":"socks","listen":"127.0.0.1","port":10809}
            """));
        var multiple = multipleNode.ToJsonString();
        var authenticated = RayConfig().Replace("\"noauth\"", "\"password\"", StringComparison.Ordinal);

        await CustomSpeedtestConfig.TryChangePort(multiple, ECoreType.Xray, 23456, false, out _, out _).Should().BeFalse();
        await CustomSpeedtestConfig.TryChangePort(authenticated, ECoreType.Xray, 23456, false, out _, out _).Should().BeFalse();
    }

    [Test]
    public async Task RemapsEveryLoopbackInboundAndUsesSocksPortForTesting()
    {
        var source = """
            {
              "inbounds": [
                {"protocol":"socks","listen":"127.0.0.1","port":10808,"tag":"socks","settings":{"auth":"noauth","udp":true}},
                {"protocol":"http","listen":"127.0.0.1","port":10809,"tag":"http","settings":{}}
              ],
              "outbounds": [{"protocol":"freedom","tag":"direct"}],
              "routing": {"rules":[{"inboundTag":["socks","http"],"outboundTag":"direct"}]}
            }
            """;

        var success = CustomSpeedtestConfig.TryChangePorts(source, ECoreType.Xray, [22345, 22346], true,
            out var result, out var testPort, out _);

        await success.Should().BeTrue();
        await testPort.Should().BeEqualTo(22345);
        var modified = JsonUtils.ParseJson(result)!;
        await modified["inbounds"]![0]!["port"]!.GetValue<int>().Should().BeEqualTo(22345);
        await modified["inbounds"]![1]!["port"]!.GetValue<int>().Should().BeEqualTo(22346);
        await modified["routing"]!["rules"]![0]!["inboundTag"]!.ToJsonString()
            .Should().BeEqualTo("[\"socks\",\"http\"]");
        await CustomSpeedtestConfig.GetInboundCount(source).Should().BeEqualTo(2);
    }

    [Test]
    public async Task UdpRequiresExplicitRayUdpSupport()
    {
        await CustomSpeedtestConfig.TryChangePort(RayConfig(), ECoreType.Xray, 23456, true, out _, out _).Should().BeFalse();
        var enabledNode = JsonUtils.ParseJson(RayConfig())!;
        enabledNode["inbounds"]![0]!["settings"]!["udp"] = true;
        var enabled = enabledNode.ToJsonString();
        await CustomSpeedtestConfig.TryChangePort(enabled, ECoreType.Xray, 23456, true, out _, out _).Should().BeTrue();
    }

    [Test]
    public async Task RejectsInvalidJsonAndNonLoopbackInbound()
    {
        await CustomSpeedtestConfig.TryChangePort("{", ECoreType.Xray, 23456, false, out _, out _).Should().BeFalse();
        await CustomSpeedtestConfig.TryChangePort(RayConfig().Replace("127.0.0.1", "0.0.0.0", StringComparison.Ordinal),
            ECoreType.Xray, 23456, false, out _, out _).Should().BeFalse();

        var secondaryNonLoopback = JsonUtils.ParseJson(RayConfig())!;
        secondaryNonLoopback["inbounds"]!.AsArray().Add(JsonUtils.ParseJson("""
            {"protocol":"http","listen":"0.0.0.0","port":10809}
            """));
        await CustomSpeedtestConfig.TryChangePorts(secondaryNonLoopback.ToJsonString(), ECoreType.Xray,
            [23456, 23457], false, out _, out _, out _).Should().BeFalse();
    }

    [Test]
    public async Task AppliesCoreSpecificInboundRulesAndRejectsAdditionalListeners()
    {
        var rayMixed = RayConfig().Replace("\"socks\"", "\"mixed\"", StringComparison.Ordinal);
        var rayMetrics = JsonUtils.ParseJson(RayConfig())!;
        rayMetrics["metrics"] = JsonUtils.ParseJson("""{"listen":"127.0.0.1:11111"}""");
        var singBoxMixed = """
            {"inbounds":[{"type":"mixed","listen":"127.0.0.1","listen_port":10808}],"outbounds":[{"type":"direct"}]}
            """;
        var singBoxApi = JsonUtils.ParseJson(singBoxMixed)!;
        singBoxApi["experimental"] = JsonUtils.ParseJson("""{"v2ray_api":{"listen":"127.0.0.1:8080"}}""");

        await CustomSpeedtestConfig.TryChangePort(rayMixed, ECoreType.Xray, 23456, false, out _, out _).Should().BeFalse();
        await CustomSpeedtestConfig.TryChangePort(rayMetrics.ToJsonString(), ECoreType.Xray, 23456, false,
            out _, out _).Should().BeFalse();
        await CustomSpeedtestConfig.TryChangePort(singBoxMixed, ECoreType.sing_box, 23456, false,
            out _, out _).Should().BeTrue();
        await CustomSpeedtestConfig.TryChangePort(singBoxApi.ToJsonString(), ECoreType.sing_box, 23456, false,
            out _, out _).Should().BeFalse();
    }

    private static string RayConfig()
    {
        return """
        {"inbounds":[{"protocol":"socks","listen":"127.0.0.1","port":10808,"settings":{"auth":"noauth"}}],"outbounds":[{"protocol":"freedom"}]}
        """;
    }
}
