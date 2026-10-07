using System.Text.Json;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class WebUpdateReleaseSelectionTests
{
    private const string ReleaseIndex = """
        [
          {
            "tag_name": "web-v7.25.9-web.3",
            "draft": false,
            "prerelease": false,
            "assets": [
              { "name": "web-update.json", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/web-v7.25.9-web.3/web-update.json", "size": 512 }
            ]
          },
          {
            "tag_name": "7.25.6",
            "draft": true,
            "prerelease": false,
            "assets": [
              { "name": "web-update.json", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/7.25.6/web-update.json", "size": 512 }
            ]
          },
          {
            "tag_name": "7.25.5",
            "draft": false,
            "prerelease": true,
            "assets": [
              { "name": "web-update.json", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/7.25.5/web-update.json", "size": 512 }
            ]
          },
          {
            "tag_name": "7.25.4",
            "draft": false,
            "prerelease": false,
            "assets": [
              { "name": "v2rayN-linux-64.zip", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/7.25.4/v2rayN-linux-64.zip", "size": 1024 }
            ]
          },
          {
            "tag_name": "7.25.3",
            "draft": false,
            "prerelease": false,
            "assets": [
              { "name": "web-update.json", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/7.25.3/web-update.json", "size": 512 },
              { "name": "v2rayN-linux-64-web-update.zip", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/7.25.3/v2rayN-linux-64-web-update.zip", "size": 4096 }
            ]
          },
          {
            "tag_name": "v7.25.7",
            "draft": false,
            "prerelease": false,
            "assets": [
              { "name": "web-update.json", "browser_download_url": "https://github.com/2dust/v2rayN/releases/download/v7.25.7/web-update.json", "size": 512 }
            ]
          }
        ]
        """;

    [Test]
    public async Task OfficialReleaseIndexSelectsOnlyTaggedReleasesWithAWebManifest()
    {
        var stable = V2rayRuntime.ParseWebReleaseCandidates(ReleaseIndex, allowPrerelease: false);
        await stable.Count.Should().BeEqualTo(1);
        var selected = stable[0];
        await selected.Tag.Should().BeEqualTo("7.25.3");
        await selected.Version.Should().BeEqualTo("7.25.3");
        await selected.IsPrerelease.Should().BeFalse();
        await selected.ManifestUrl.Should().BeEqualTo("https://github.com/2dust/v2rayN/releases/download/7.25.3/web-update.json");
        await selected.Assets.ContainsKey("v2rayN-linux-64-web-update.zip").Should().BeTrue();

        var withPrerelease = V2rayRuntime.ParseWebReleaseCandidates(ReleaseIndex, allowPrerelease: true);
        await withPrerelease.Count.Should().BeEqualTo(2);
        await withPrerelease[0].Version.Should().BeEqualTo("7.25.5");
        await withPrerelease[1].Version.Should().BeEqualTo("7.25.3");
    }

    [Test]
    public async Task NewestOfficialCandidateWinsAfterVersionOrdering()
    {
        var candidates = V2rayRuntime.ParseWebReleaseCandidates(ReleaseIndex, allowPrerelease: false);
        candidates.Sort((left, right) => new ServiceLib.Models.Dto.SemanticVersion(right.Version)
            .CompareTo(new ServiceLib.Models.Dto.SemanticVersion(left.Version)));
        await candidates[0].Version.Should().BeEqualTo("7.25.3");
    }

    [Test]
    public async Task InvalidReleaseIndexesAreRejected()
    {
        var malformedRejected = false;
        try { _ = V2rayRuntime.ParseWebReleaseCandidates("{ not json", allowPrerelease: false); }
        catch (JsonException) { malformedRejected = true; }
        await malformedRejected.Should().BeTrue();

        var nonArrayRejected = false;
        try { _ = V2rayRuntime.ParseWebReleaseCandidates("{}", allowPrerelease: false); }
        catch (InvalidDataException) { nonArrayRejected = true; }
        await nonArrayRejected.Should().BeTrue();
    }
}
