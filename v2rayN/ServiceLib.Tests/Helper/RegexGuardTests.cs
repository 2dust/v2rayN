namespace ServiceLib.Tests.Helper;

public class RegexGuardTests
{
    [Test]
    public async Task IsRegexMatch_NormalPattern_ShouldMatch()
    {
        await Utils.IsRegexMatch("HK-node-01", "HK|香港").Should().BeTrue();
        await Utils.IsRegexMatch("JP-node-01", "HK|香港").Should().BeFalse();
    }

    [Test]
    public async Task IsRegexMatch_EmptyPattern_ShouldPassThrough()
    {
        await Utils.IsRegexMatch("anything", "").Should().BeTrue();
        await Utils.IsRegexMatch("anything", null).Should().BeTrue();
    }

    [Test]
    public async Task IsRegexMatch_InvalidPattern_ShouldFailOpen()
    {
        await Utils.IsRegexMatch("node-01", "([unclosed").Should().BeTrue();
    }

    [Test]
    public async Task IsRegexMatch_EvilPattern_ShouldTimeoutAndFailOpen()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = Utils.IsRegexMatch(new string('a', 30) + "!", "(a+)+$");
        sw.Stop();

        await result.Should().BeTrue();
        await (sw.Elapsed < TimeSpan.FromSeconds(30)).Should().BeTrue().Because(
            $"evil pattern must be cut off by timeout, took {sw.Elapsed}");
    }
}
