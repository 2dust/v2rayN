namespace ServiceLib.Tests.Common;

public class NetworkUtilsTests
{
    [Test]
    public async Task GetRealBindInterfaceName_EmptyOrNull_ShouldReturnNull()
    {
        await NetworkUtils.GetRealBindInterfaceName(null).Should().BeNull();
        await NetworkUtils.GetRealBindInterfaceName("").Should().BeNull();
        await NetworkUtils.GetRealBindInterfaceName("   ").Should().BeNull();
    }

    [Test]
    public async Task GetRealBindInterfaceName_SpecificInterface_ShouldReturnTrimmedName()
    {
        await NetworkUtils.GetRealBindInterfaceName("eth0").Should().BeEqualTo("eth0");
        await NetworkUtils.GetRealBindInterfaceName(" WLAN ").Should().BeEqualTo("WLAN");
    }

    [Test]
    public async Task GetRealBindInterfaceName_Auto_ShouldResolveDefaultOrReturnNull()
    {
        var expected = NetworkUtils.GetDefaultPhysicalInterfaceName();

        await NetworkUtils.GetRealBindInterfaceName(Global.Auto).Should().BeEqualTo(expected);
        await NetworkUtils.GetRealBindInterfaceName("auto").Should().BeEqualTo(expected);
        await NetworkUtils.GetRealBindInterfaceName("Auto").Should().BeEqualTo(expected);
    }

    [Test]
    public async Task GetBindInterfaceNames_ShouldContainAutoAndEmpty()
    {
        var names = NetworkUtils.GetBindInterfaceNames();

        await names.Should().Contain(Global.Auto);
        await names.Should().Contain(string.Empty);
    }
}
