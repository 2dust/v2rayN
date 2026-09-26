namespace ServiceLib.Tests.Manager;

public class CoreInfoManagerTests
{
    [Test]
    public async Task ShouldCheckPreRelease_XrayTunOnNonWindows_UsesPreRelease()
    {
        await CoreInfoManager.ShouldCheckPreRelease(ECoreType.Xray, preRelease: false, enableTun: true, isNonWindows: true)
            .Should().BeTrue();
    }

    [Test]
    public async Task ShouldCheckPreRelease_XrayTunOnWindows_UsesStableRelease()
    {
        await CoreInfoManager.ShouldCheckPreRelease(ECoreType.Xray, preRelease: false, enableTun: true, isNonWindows: false)
            .Should().BeFalse();
    }

    [Test]
    public async Task ShouldCheckPreRelease_XrayWithoutTun_UsesStableRelease()
    {
        await CoreInfoManager.ShouldCheckPreRelease(ECoreType.Xray, preRelease: false, enableTun: false, isNonWindows: true)
            .Should().BeFalse();
    }

    [Test]
    public async Task ShouldCheckPreRelease_ExplicitSelectionIsPreserved()
    {
        await CoreInfoManager.ShouldCheckPreRelease(ECoreType.Xray, preRelease: true, enableTun: false, isNonWindows: false)
            .Should().BeTrue();
        await CoreInfoManager.ShouldCheckPreRelease(ECoreType.mihomo, preRelease: true, enableTun: true, isNonWindows: true)
            .Should().BeFalse();
    }
}
