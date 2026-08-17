using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Fmt;

public class InnerFmtTests
{
    [Test]
    public async Task ToUriAndResolve_ShouldRoundTripPolicyGroupReferences()
    {
        var childA = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "child-a", "child-a");
        var childB = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "child-b", "child-b");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "group-1", "group-1",
            [childA.IndexId, childB.IndexId]);
        group.SetProtocolExtra(group.GetProtocolExtra() with { SubChildItems = "original-sub" });

        var uri = InnerFmt.ToUri([group, childA, childB]);

        await uri.Should().NotBeNull();
        await uri.Should().NotBeEmpty();

        var resolved = InnerFmt.Resolve(uri!, "sub-123");

        await resolved.Should().NotBeNull();
        await resolved.Should().HaveCount(3);

        var resolvedGroup = resolved!.Single(x => x.Remarks == group.Remarks);
        var resolvedChildA = resolved.Single(x => x.Remarks == childA.Remarks);
        var resolvedChildB = resolved.Single(x => x.Remarks == childB.Remarks);

        await resolvedGroup.ConfigType.Should().BeEqualTo(EConfigType.PolicyGroup);
        await resolvedGroup.GetProtocolExtra().SubChildItems.Should().BeEqualTo("sub-123");
        await resolvedGroup.GetProtocolExtra().ChildItems.Should().BeEqualTo($"{resolvedChildA.IndexId},{resolvedChildB.IndexId}");
    }
}
