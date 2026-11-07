using System.Net.NetworkInformation;

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

    [Test]
    public async Task IsAuto_ValidVariations_ShouldReturnTrue()
    {
        await NetworkUtils.IsAuto(Global.Auto).Should().BeTrue();
        await NetworkUtils.IsAuto("auto").Should().BeTrue();
        await NetworkUtils.IsAuto(" AUTO ").Should().BeTrue();
        await NetworkUtils.IsAuto("Auto").Should().BeTrue();

        await NetworkUtils.IsAuto("").Should().BeFalse();
        await NetworkUtils.IsAuto("   ").Should().BeFalse();
        await NetworkUtils.IsAuto(null).Should().BeFalse();
        await NetworkUtils.IsAuto("WLAN").Should().BeFalse();
    }

    [Test]
    public async Task SelectDefaultInterface_BothEthernetAndWifiWithGateway_ShouldPreferEthernet()
    {
        var candidates = new List<NetworkInterfaceCandidate>
        {
            new("Wi-Fi", "Intel(R) Wi-Fi 6 AX200", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, true),
            new("Ethernet", "Realtek Gaming 2.5GbE Family Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true)
        };

        var selected = NetworkUtils.SelectDefaultInterface(candidates);

        await selected.Should().BeEqualTo("Ethernet");
    }

    [Test]
    public async Task SelectDefaultInterface_OnlyWifiWithGateway_ShouldSelectWifi()
    {
        var candidates = new List<NetworkInterfaceCandidate>
        {
            new("Ethernet", "Realtek Gaming 2.5GbE", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false),
            new("Wi-Fi", "Intel(R) Wi-Fi 6", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, true)
        };

        var selected = NetworkUtils.SelectDefaultInterface(candidates);

        await selected.Should().BeEqualTo("Wi-Fi");
    }

    [Test]
    public async Task SelectDefaultInterface_NoGateways_ShouldFallbackToFirstPhysicalNic()
    {
        var candidates = new List<NetworkInterfaceCandidate>
        {
            new("以太网", "Intel Ethernet Controller", NetworkInterfaceType.Ethernet, OperationalStatus.Up, false),
            new("WLAN", "Intel Wi-Fi", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, false)
        };

        var selected = NetworkUtils.SelectDefaultInterface(candidates);

        await selected.Should().BeEqualTo("以太网");
    }

    [Test]
    public async Task SelectDefaultInterface_VirtualAndLoopbackNics_ShouldBeFilteredOut()
    {
        var candidates = new List<NetworkInterfaceCandidate>
        {
            new("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true),
            new("Loopback", "Software Loopback Interface", NetworkInterfaceType.Loopback, OperationalStatus.Up, true),
            new("wintun", "Wintun Userspace Tunnel", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true),
            new("tailscale0", "Tailscale Tunnel", NetworkInterfaceType.Ethernet, OperationalStatus.Up, true),
            new("WLAN", "Realtek Wireless LAN", NetworkInterfaceType.Wireless80211, OperationalStatus.Up, true)
        };

        var selected = NetworkUtils.SelectDefaultInterface(candidates);

        await selected.Should().BeEqualTo("WLAN");
    }

    [Test]
    public async Task SelectDefaultInterface_EmptyOrDownNics_ShouldReturnNull()
    {
        var candidates = new List<NetworkInterfaceCandidate>
        {
            new("Ethernet", "Disconnected cable", NetworkInterfaceType.Ethernet, OperationalStatus.Down, false)
        };

        var selected = NetworkUtils.SelectDefaultInterface(candidates);

        await selected.Should().BeNull();
        await NetworkUtils.SelectDefaultInterface([]).Should().BeNull();
    }
}
