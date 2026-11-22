using ServiceLib.Services.GitHub;

namespace ServiceLib.Tests.Services.GitHub;

public class GitHubRefsParserTests
{
    [Test]
    public async Task ParseTags_ReturnsTagRefsOnlyAndStripsCapabilities()
    {
        const string content = "001e# service=git-upload-pack\n"
                               + "0000"
                               + "0128abcd refs/tags/v1.0.0\0multi_ack side-band-64k ofs-delta\n"
                               + "003fabcd refs/tags/v1.0.0^{}\n"
                               + "003eabcd refs/heads/main\n"
                               + "0041abcd refs/tags/v1.2.0-rc.1\n"
                               + "0000";

        var tags = GitHubRefsParser.ParseTags(content);

        await tags.Count.Should().BeEqualTo(2);
        await string.Join(",", tags).Should().BeEqualTo("v1.0.0,v1.2.0-rc.1");
    }

    [Test]
    public async Task ParseTags_SkipsPeeledTagsAndKeepsCrLfPayload()
    {
        const string content = "001e# service=git-upload-pack\r\n"
                               + "0000"
                               + "003f0123456789abcdef0123456789abcdef01234567 refs/tags/v2.0.0\r\n"
                               + "003f0123456789abcdef0123456789abcdef01234567 refs/tags/v2.0.0^{}\r\n";

        var tags = GitHubRefsParser.ParseTags(content);

        await string.Join(",", tags).Should().BeEqualTo("v2.0.0");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ParseTags_ReturnsEmptyForMissingContent(string? content)
    {
        var tags = GitHubRefsParser.ParseTags(content);

        await tags.Should().BeEmpty();
    }
}
