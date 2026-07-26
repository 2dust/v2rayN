using AwesomeAssertions;
using ServiceLib.Enums;
using ServiceLib.Manager;
using Xunit;

namespace ServiceLib.Tests.Manager;

public class CoreManagerTests
{
    [Theory]
    [InlineData(ECoreType.sing_box)]
    [InlineData(ECoreType.mihomo)]
    [InlineData(ECoreType.Xray)]
    public void ShouldRunAsSudo_TunLaunchOnNonWindows_RequiresElevation(ECoreType coreType)
    {
        CoreManager.ShouldRunAsSudo(isTunLaunch: true, coreType, isNonWindows: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldRunAsSudo_NonTunLaunch_ShouldNotElevate()
    {
        // Regression guard for the macOS TUN failure: the elevation decision must follow
        // the context snapshot that generated the config. A launch whose snapshot has TUN
        // disabled must never elevate, and a launch whose snapshot has TUN enabled must
        // elevate regardless of later changes to the live config.
        CoreManager.ShouldRunAsSudo(isTunLaunch: false, ECoreType.sing_box, isNonWindows: true).Should().BeFalse();
        CoreManager.ShouldRunAsSudo(isTunLaunch: false, ECoreType.Xray, isNonWindows: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldRunAsSudo_OnWindows_ShouldNotElevate()
    {
        CoreManager.ShouldRunAsSudo(isTunLaunch: true, ECoreType.sing_box, isNonWindows: false).Should().BeFalse();
    }

    [Theory]
    [InlineData(ECoreType.v2fly)]
    [InlineData(ECoreType.hysteria)]
    [InlineData(null)]
    public void ShouldRunAsSudo_UnsupportedCoreType_ShouldNotElevate(ECoreType? coreType)
    {
        CoreManager.ShouldRunAsSudo(isTunLaunch: true, coreType, isNonWindows: true).Should().BeFalse();
    }
}
