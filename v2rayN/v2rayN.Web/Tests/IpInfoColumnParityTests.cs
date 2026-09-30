using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

[NotInParallel]
public class IpInfoColumnParityTests
{
    [Test]
    public async Task IpInfoColumnRequiresConfiguredApiAndDesktopColumnVisibility()
    {
        await V2rayRuntime.ShouldShowIpInfoColumn("https://ip.example.test", hideColumnIpInfo: false).Should().BeTrue();
        await V2rayRuntime.ShouldShowIpInfoColumn(" ", hideColumnIpInfo: false).Should().BeTrue();
        await V2rayRuntime.ShouldShowIpInfoColumn(string.Empty, hideColumnIpInfo: false).Should().BeFalse();
        await V2rayRuntime.ShouldShowIpInfoColumn("https://ip.example.test", hideColumnIpInfo: true).Should().BeFalse();
        await V2rayRuntime.ShouldShowIpInfoColumn(null, hideColumnIpInfo: true).Should().BeFalse();
    }

    [Test]
    public async Task CanonicalSettingsExposeIpColumnVisibilityFromApiUrlAndDesktopHideFlag()
    {
        var config = SniffingSettingsIntegrationTests.CreateConfig();
        SniffingSettingsIntegrationTests.BindAppManagerConfig(config);
        var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);

        var initiallyEmpty = await runtime.GetSettingsAsync();
        await initiallyEmpty.ShowIpInfoColumn.Should().BeFalse();

        config.SpeedTestItem.IPAPIUrl = "https://ip.example.test";
        var configured = await runtime.GetSettingsAsync();
        await configured.ShowIpInfoColumn.Should().BeTrue();

        config.UiItem.HideColumnIpInfo = true;
        var hiddenByDesktopPreference = await runtime.GetSettingsAsync();
        await hiddenByDesktopPreference.ShowIpInfoColumn.Should().BeFalse();
    }
}
