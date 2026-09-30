using System.Text.Json;
using ServiceLib.Enums;
using ServiceLib.Models.Entities;

namespace v2rayN.Web.Tests;

public class RoutingParityTests
{
    [Test]
    public async Task RoutingProfileApiEntityCarriesAllDesktopFieldsAcrossJsonRoundTrip()
    {
        var original = new RoutingItem
        {
            Id = "route-profile",
            Remarks = "locked rules",
            Url = "https://rules.example/list.json",
            RuleSet = "[]",
            RuleNum = 0,
            Enabled = false,
            Locked = true,
            CustomIcon = "custom-icon",
            CustomRulesetPath4Singbox = "/srv/rules/custom.srs",
            DomainStrategy = "UseIP",
            DomainStrategy4Singbox = "prefer_ipv4",
            Sort = 19,
            IsActive = true,
        };
        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var reloaded = JsonSerializer.Deserialize<RoutingItem>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await reloaded.Id.Should().BeEqualTo(original.Id);
        await reloaded.Remarks.Should().BeEqualTo(original.Remarks);
        await reloaded.Url.Should().BeEqualTo(original.Url);
        await reloaded.RuleSet.Should().BeEqualTo(original.RuleSet);
        await reloaded.RuleNum.Should().BeEqualTo(original.RuleNum);
        await reloaded.Enabled.Should().BeEqualTo(original.Enabled);
        await reloaded.Locked.Should().BeEqualTo(original.Locked);
        await reloaded.CustomIcon.Should().BeEqualTo(original.CustomIcon);
        await reloaded.CustomRulesetPath4Singbox.Should().BeEqualTo(original.CustomRulesetPath4Singbox);
        await reloaded.DomainStrategy.Should().BeEqualTo(original.DomainStrategy);
        await reloaded.DomainStrategy4Singbox.Should().BeEqualTo(original.DomainStrategy4Singbox);
        await reloaded.Sort.Should().BeEqualTo(original.Sort);
        await reloaded.IsActive.Should().BeEqualTo(original.IsActive);
    }

    [Test]
    public async Task RoutingRuleEntityCarriesEveryDesktopMatcherAndControlField()
    {
        var original = new RulesItem
        {
            Id = "rule-id",
            Type = "field",
            Port = "80,443",
            Network = "tcp,udp",
            InboundTag = ["socks", "tun"],
            OutboundTag = "proxy",
            Ip = ["geoip:cn"],
            Domain = ["domain:example.com"],
            Protocol = ["http", "tls"],
            Process = ["browser"],
            Enabled = false,
            Remarks = "web rule",
            RuleType = ERuleType.Routing,
        };
        var json = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var reloaded = JsonSerializer.Deserialize<RulesItem>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await reloaded.Id.Should().BeEqualTo(original.Id);
        await reloaded.Type.Should().BeEqualTo(original.Type);
        await reloaded.Port.Should().BeEqualTo(original.Port);
        await reloaded.Network.Should().BeEqualTo(original.Network);
        await reloaded.InboundTag!.SequenceEqual(original.InboundTag!).Should().BeTrue();
        await reloaded.OutboundTag.Should().BeEqualTo(original.OutboundTag);
        await reloaded.Ip!.SequenceEqual(original.Ip!).Should().BeTrue();
        await reloaded.Domain!.SequenceEqual(original.Domain!).Should().BeTrue();
        await reloaded.Protocol!.SequenceEqual(original.Protocol!).Should().BeTrue();
        await reloaded.Process!.SequenceEqual(original.Process!).Should().BeTrue();
        await reloaded.Enabled.Should().BeEqualTo(original.Enabled);
        await reloaded.Remarks.Should().BeEqualTo(original.Remarks);
        await reloaded.RuleType.Should().BeEqualTo(original.RuleType);
    }
}
