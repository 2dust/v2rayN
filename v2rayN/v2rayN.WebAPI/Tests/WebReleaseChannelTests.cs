using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class WebReleaseChannelTests
{
    [Test]
    public async Task OfficialReleaseTagsAreAcceptedAndLegacyWebTagsAreIgnored()
    {
        var accepted = WebReleaseChannel.TryParseReleaseTag("7.25.3", out var version);
        await accepted.Should().BeTrue();
        await version.Should().BeEqualTo("7.25.3");

        foreach (var legacy in new[] { "web-v7.25.3-web.1", "7.25.3-web.1", "v7.25.3", "7.25", "7.25.3.1", "", null })
        {
            var parsed = WebReleaseChannel.TryParseReleaseTag(legacy, out var parsedVersion);
            await parsed.Should().BeFalse();
            await parsedVersion.Should().BeEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task ReleaseIndexUrlIsBuiltFromTheBuildRepository()
    {
        await WebReleaseChannel.BuildReleaseIndexUrl("2dust/v2rayN")
            .Should().BeEqualTo("https://api.github.com/repos/2dust/v2rayN/releases?per_page=100");
        await WebReleaseChannel.DefaultRepository.Should().BeEqualTo("2dust/v2rayN");

        var rejected = false;
        try { _ = WebReleaseChannel.BuildReleaseIndexUrl("2dust/v2rayN/../../evil"); }
        catch (ArgumentException) { rejected = true; }
        await rejected.Should().BeTrue();
        await WebReleaseChannel.IsValidRepository("community-fork/v2rayN").Should().BeTrue();
        await WebReleaseChannel.IsValidRepository("not-a-repository").Should().BeFalse();
    }

    [Test]
    public async Task TrustedAssetUrlsMustUseTheBuildRepositoryTagAndAsset()
    {
        var repository = "2dust/v2rayN";
        var tag = "7.25.3";
        var asset = "v2rayN-linux-64-WebAPI-update.zip";
        var trusted = $"https://github.com/{repository}/releases/download/{tag}/{asset}";

        await WebReleaseChannel.IsTrustedAssetUrl(trusted, repository, tag, asset).Should().BeTrue();
        // A build must not accept the same asset from another repository.
        await WebReleaseChannel.IsTrustedAssetUrl(trusted.Replace(repository, "another-owner/v2rayN", StringComparison.Ordinal),
            repository, tag, asset).Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl("http://github.com/2dust/v2rayN/releases/download/7.25.3/" + asset,
            repository, tag, asset).Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl("https://example.com/2dust/v2rayN/releases/download/7.25.3/" + asset,
            repository, tag, asset).Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl(trusted, repository, "7.25.4", asset).Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl(trusted, repository, tag, "WebAPI-update.json").Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl("https://github.com/2dust/v2rayN/releases/download/7.25.3/sub/" + asset,
            repository, tag, asset).Should().BeFalse();
        await WebReleaseChannel.IsTrustedAssetUrl(null, repository, tag, asset).Should().BeFalse();
    }

    [Test]
    public async Task RuntimeIdentifiersMapToSupportedArchiveNames()
    {
        await WebUpdatePackageStager.ArtifactArch("linux-x64").Should().BeEqualTo("64");
        await WebUpdatePackageStager.ArtifactArch("linux-arm64").Should().BeEqualTo("arm64");
        await WebUpdatePackageStager.FullInstallAssetName("linux-x64").Should().BeEqualTo("v2rayN-linux-64-WebAPI.zip");
        await WebUpdatePackageStager.FullInstallAssetName("linux-arm64").Should().BeEqualTo("v2rayN-linux-arm64-WebAPI.zip");
        await WebUpdatePackageStager.AppOnlyAssetName("linux-x64").Should().BeEqualTo("v2rayN-linux-64-WebAPI-update.zip");
        await WebUpdatePackageStager.AppOnlyAssetName("linux-arm64").Should().BeEqualTo("v2rayN-linux-arm64-WebAPI-update.zip");
        await WebUpdatePackageStager.ArtifactArch("win-x64").Should().BeEqualTo("64");
        await WebUpdatePackageStager.FullInstallAssetName("win-x64").Should().BeEqualTo("v2rayN-windows-64-WebAPI.zip");
        await WebUpdatePackageStager.AppOnlyAssetName("win-x64").Should().BeEqualTo("v2rayN-windows-64-WebAPI-update.zip");
        await WebUpdatePackageStager.FullInstallAssetName("linux-riscv64").Should().BeNull();
        await WebUpdatePackageStager.AppOnlyAssetName(null).Should().BeNull();
    }

    [Test]
    public async Task OfficialVersionsCompareGreaterThanDevAndLegacyIdentities()
    {
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.2", "7.25.3", allowPrerelease: false).Should().BeTrue();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.3", allowPrerelease: false).Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.2", allowPrerelease: false).Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("0.0.0-dev", "7.25.3", allowPrerelease: false).Should().BeTrue();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.2-web.9", "7.25.3", allowPrerelease: false).Should().BeTrue();
    }
}
