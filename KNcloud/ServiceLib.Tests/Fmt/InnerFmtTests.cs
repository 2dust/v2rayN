using AwesomeAssertions;
using ServiceLib.Enums;
using ServiceLib.Handler.Fmt;
using ServiceLib.Tests.CoreConfig;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class InnerFmtTests
{
    [Fact]
    public void ToUriAndResolve_ShouldRoundTripPolicyGroupReferences()
    {
        var childA = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "child-a", "child-a");
        var childB = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "child-b", "child-b");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "group-1", "group-1",
            [childA.IndexId, childB.IndexId]);
        group.SetProtocolExtra(group.GetProtocolExtra() with { SubChildItems = "original-sub" });

        var uri = InnerFmt.ToUri([group, childA, childB]);

        uri.Should().NotBeNullOrWhiteSpace();

        var resolved = InnerFmt.Resolve(uri!, "sub-123");

        resolved.Should().NotBeNull();
        resolved.Should().HaveCount(3);

        var resolvedGroup = resolved!.Single(x => x.Remarks == group.Remarks);
        var resolvedChildA = resolved.Single(x => x.Remarks == childA.Remarks);
        var resolvedChildB = resolved.Single(x => x.Remarks == childB.Remarks);

        resolvedGroup.ConfigType.Should().Be(EConfigType.PolicyGroup);
        resolvedGroup.GetProtocolExtra().SubChildItems.Should().Be("sub-123");
        resolvedGroup.GetProtocolExtra().ChildItems.Should().Be($"{resolvedChildA.IndexId},{resolvedChildB.IndexId}");
    }
}
