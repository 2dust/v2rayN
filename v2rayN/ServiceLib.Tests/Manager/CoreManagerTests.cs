namespace ServiceLib.Tests.Manager;

public class CoreManagerTests
{
    [Test]
    [Arguments(ECoreType.sing_box)]
    [Arguments(ECoreType.mihomo)]
    [Arguments(ECoreType.Xray)]
    public async Task ShouldRunAsSudo_TunLaunchOnNonWindows_RequiresElevation(ECoreType coreType)
    {
        await CoreManager.ShouldRunAsSudo(isTunLaunch: true, coreType, isNonWindows: true).Should().BeTrue();
    }

    [Test]
    public async Task ShouldRunAsSudo_NonTunLaunch_ShouldNotElevate()
    {
        // Regression guard for the macOS TUN failure: the elevation decision must follow
        // the context snapshot that generated the config. A launch whose snapshot has TUN
        // disabled must never elevate, and a launch whose snapshot has TUN enabled must
        // elevate regardless of later changes to the live config.
        await CoreManager.ShouldRunAsSudo(isTunLaunch: false, ECoreType.sing_box, isNonWindows: true).Should().BeFalse();
        await CoreManager.ShouldRunAsSudo(isTunLaunch: false, ECoreType.Xray, isNonWindows: true).Should().BeFalse();
    }

    [Test]
    public async Task ShouldRunAsSudo_OnWindows_ShouldNotElevate()
    {
        await CoreManager.ShouldRunAsSudo(isTunLaunch: true, ECoreType.sing_box, isNonWindows: false).Should().BeFalse();
    }

    [Test]
    [Arguments(ECoreType.v2fly)]
    [Arguments(ECoreType.hysteria)]
    [Arguments(null)]
    public async Task ShouldRunAsSudo_UnsupportedCoreType_ShouldNotElevate(ECoreType? coreType)
    {
        await CoreManager.ShouldRunAsSudo(isTunLaunch: true, coreType, isNonWindows: true).Should().BeFalse();
    }
}
