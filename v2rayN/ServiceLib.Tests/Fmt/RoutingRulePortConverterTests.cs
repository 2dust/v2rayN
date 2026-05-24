using AwesomeAssertions;
using ServiceLib.Models.Entities;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class RoutingRulePortConverterTests
{
    [Fact]
    public void DeserializeRoutingRules_PortNumber_ShouldBeAccepted()
    {
        var json = """
        [
          {
            "type": "field",
            "outboundTag": "direct",
            "port": 53
          }
        ]
        """;

        var rules = JsonUtils.Deserialize<List<RulesItem>>(json);

        rules.Should().NotBeNull();
        rules!.Should().HaveCount(1);
        rules[0].Port.Should().Be("53");
    }

    [Fact]
    public void DeserializeRoutingRules_PortStringRange_ShouldStillWork()
    {
        var json = """
        [
          {
            "type": "field",
            "outboundTag": "proxy",
            "port": "0-65535"
          }
        ]
        """;

        var rules = JsonUtils.Deserialize<List<RulesItem>>(json);

        rules.Should().NotBeNull();
        rules!.Should().HaveCount(1);
        rules[0].Port.Should().Be("0-65535");
    }
}
