namespace ServiceLib.Services.GitHub;

/// <summary>
///     Discovers the latest release tag of a core on GitHub: releases/latest redirect, releases API or the git references.
/// </summary>
public class GitHubReleaseSource(DownloadService downloadService, Func<GitHubReleaseProgress, Task>? progressFunc = null)
{
    public async Task<string?> GetLatestTag(CoreInfo coreInfo, bool preRelease, CancellationToken cancellationToken = default)
    {
        if (preRelease || coreInfo.LockedMaxVersion != null)
        {
            await Report(coreInfo, EGitHubReleaseStage.ReleasesApi);

            var tagName = await GetTagViaReleasesApi(coreInfo, preRelease, cancellationToken);
            if (!string.IsNullOrEmpty(tagName))
            {
                return tagName;
            }

            await Report(coreInfo, EGitHubReleaseStage.ReleasesApiFailed);
            if (!preRelease)
            {
                return null;
            }

            await Report(coreInfo, EGitHubReleaseStage.GitRefs);

            tagName = await GetTagViaGitRefs(coreInfo, cancellationToken);
            if (string.IsNullOrEmpty(tagName))
            {
                await Report(coreInfo, EGitHubReleaseStage.GitRefsFailed);
                return null;
            }
            return tagName;
        }

        await Report(coreInfo, EGitHubReleaseStage.LatestRedirect);
        return await GetTagViaLatestRedirect(coreInfo, cancellationToken);
    }

    private async Task Report(CoreInfo coreInfo, EGitHubReleaseStage stage)
    {
        if (progressFunc == null)
        {
            return;
        }
        await progressFunc(new GitHubReleaseProgress { CoreType = coreInfo.CoreType, Stage = stage });
    }

    private async Task<string?> GetTagViaLatestRedirect(CoreInfo coreInfo, CancellationToken cancellationToken)
    {
        var url = $"{coreInfo.Url}/latest";
        var lastUrl = await downloadService.GetFinalUrlAsync(url, cancellationToken);
        return lastUrl?.Split("/tag/").LastOrDefault();
    }

    private async Task<string?> GetTagViaReleasesApi(CoreInfo coreInfo, bool preRelease, CancellationToken cancellationToken)
    {
        var result = await downloadService.TryDownloadString(coreInfo.ReleaseApiUrl, cancellationToken);
        if (result.IsNullOrEmpty())
        {
            return null;
        }

        var releases = JsonUtils.Deserialize<List<GitHubRelease>>(result);
        return GitHubReleaseSelector.SelectTag(releases, preRelease, coreInfo.LockedMaxVersion);
    }

    private async Task<string?> GetTagViaGitRefs(CoreInfo coreInfo, CancellationToken cancellationToken)
    {
        var result = await downloadService.TryDownloadString(coreInfo.GitInfoRefsUrl, cancellationToken);
        if (result.IsNullOrEmpty())
        {
            return null;
        }

        var tags = GitHubRefsParser.ParseTags(result);
        var lockedMaxVersion = coreInfo.LockedMaxVersion ?? new SemanticVersion(int.MaxValue, int.MaxValue, int.MaxValue);
        var sortedTags = tags
            .Select(tag => new { Tag = tag, IsValid = SemanticVersion.TryParse(tag, out var v), Version = v })
            .Where(x => x.IsValid && x.Version <= lockedMaxVersion)
            .OrderByDescending(x => x.Version)
            .Select(x => x.Tag)
            .ToList();

        // TODO: anit-pattern for now
        using var httpClient = new HttpClient(downloadService.Handler, false);
        foreach (var tag in sortedTags)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, $"{coreInfo.Url}/tag/{tag}");
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return tag;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Ignore
            }
        }
        return null;
    }
}
