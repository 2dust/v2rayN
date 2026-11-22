using ServiceLib.Services.GitHub;

namespace ServiceLib.Tests.Services.GitHub;

public class GitHubReleaseSelectorTests
{
    [Test]
    public async Task SelectTag_SkipsPrereleaseWhenNotRequested()
    {
        var releases = new List<GitHubRelease>
        {
            new() { TagName = "v2.0.0-beta", Prerelease = true },
            new() { TagName = "v1.9.0", Prerelease = false },
        };

        var tag = GitHubReleaseSelector.SelectTag(releases, false, null);

        await tag.Should().BeEqualTo("v1.9.0");
    }

    [Test]
    public async Task SelectTag_ReturnsFirstReleaseWhenPrereleaseRequested()
    {
        var releases = new List<GitHubRelease>
        {
            new() { TagName = "v2.0.0-beta", Prerelease = true },
            new() { TagName = "v1.9.0", Prerelease = false },
        };

        var tag = GitHubReleaseSelector.SelectTag(releases, true, null);

        await tag.Should().BeEqualTo("v2.0.0-beta");
    }

    [Test]
    public async Task SelectTag_FallsBackToHighestVersionBelowLockedMax()
    {
        var releases = new List<GitHubRelease>
        {
            new() { TagName = "v2.0.0", Prerelease = false },
            new() { TagName = "v1.5.0", Prerelease = false },
            new() { TagName = "v1.4.0", Prerelease = false },
        };

        var tag = GitHubReleaseSelector.SelectTag(releases, false, new SemanticVersion(1, 5, 0));

        await tag.Should().BeEqualTo("v1.5.0");
    }

    [Test]
    public async Task SelectTag_ReturnsLatestTagWhenWithinLockedMax()
    {
        var releases = new List<GitHubRelease>
        {
            new() { TagName = "v1.2.0", Prerelease = false },
            new() { TagName = "v1.1.0", Prerelease = false },
        };

        var tag = GitHubReleaseSelector.SelectTag(releases, false, new SemanticVersion(1, 14, int.MaxValue));

        await tag.Should().BeEqualTo("v1.2.0");
    }

    [Test]
    public async Task SelectTag_ReturnsNullWhenAllReleasesExceedLockedMax()
    {
        var releases = new List<GitHubRelease>
        {
            new() { TagName = "v2.0.0", Prerelease = false },
        };

        var tag = GitHubReleaseSelector.SelectTag(releases, false, new SemanticVersion(1, 0, 0));

        await tag.Should().BeNull();
    }

    [Test]
    public async Task SelectTag_ReturnsNullForEmptyInput()
    {
        var emptyTag = GitHubReleaseSelector.SelectTag([], false, null);
        var nullTag = GitHubReleaseSelector.SelectTag(null, true, new SemanticVersion(1, 0, 0));

        await emptyTag.Should().BeNull();
        await nullTag.Should().BeNull();
    }
}
