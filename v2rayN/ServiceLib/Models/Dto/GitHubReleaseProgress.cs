namespace ServiceLib.Models.Dto;

public class GitHubReleaseProgress
{
    public ECoreType CoreType { get; set; }
    public EGitHubReleaseStage Stage { get; set; }
}
