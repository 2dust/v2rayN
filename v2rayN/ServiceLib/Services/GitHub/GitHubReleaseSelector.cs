namespace ServiceLib.Services.GitHub;

/// <summary>
///     Picks the release tag from the GitHub releases list, honouring the pre-release flag and the locked max version.
/// </summary>
public static class GitHubReleaseSelector
{
    public static string? SelectTag(List<GitHubRelease>? releases, bool preRelease, SemanticVersion? lockedMaxVersion)
    {
        if (releases is null || releases.Count == 0)
        {
            return null;
        }

        var release = preRelease
            ? releases.FirstOrDefault()
            : releases.FirstOrDefault(r => r.Prerelease == false);
        var tagName = release?.TagName;
        if (lockedMaxVersion == null)
        {
            return tagName;
        }

        var remoteVersion = new SemanticVersion(tagName);
        if (remoteVersion <= lockedMaxVersion)
        {
            return tagName;
        }

        var fallbackRelease = releases
            .Where(r => preRelease || !r.Prerelease)
            .Select(r => new { Release = r, IsValid = SemanticVersion.TryParse(r.TagName, out var v), Version = v })
            .Where(x => x.IsValid && x.Version <= lockedMaxVersion)
            .MaxBy(x => x.Version)?
            .Release;

        return fallbackRelease?.TagName;
    }
}
