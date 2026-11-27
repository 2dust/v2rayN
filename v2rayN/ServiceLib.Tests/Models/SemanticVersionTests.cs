namespace ServiceLib.Tests.Models;

public class SemanticVersionTests
{
    [Test]
    public async Task TryParse_RejectsNonNumericTag()
    {
        var parsed = SemanticVersion.TryParse("nightly", out var result);

        await parsed.Should().BeFalse();
        await result.Should().BeNull();
    }

    [Test]
    public async Task TryParse_AcceptsVPrefixedVersion()
    {
        var parsed = SemanticVersion.TryParse("v1.0.0", out var result);

        await parsed.Should().BeTrue();
        await result.Should().BeEqualTo(new SemanticVersion(1, 0, 0));
        await result!.ToString().Should().BeEqualTo("v1.0.0");
    }

    [Test]
    public async Task TryParse_AcceptsZeroVersion()
    {
        var parsed = SemanticVersion.TryParse("0.0.0", out var result);

        await parsed.Should().BeTrue();
        await result.Should().BeEqualTo(new SemanticVersion(0, 0, 0));
    }

    [Test]
    public async Task TryParse_KeepsEmptyStringAsZeroVersionForCompatibility()
    {
        var parsed = SemanticVersion.TryParse("", out var result);

        await parsed.Should().BeTrue();
        await result.Should().BeEqualTo(new SemanticVersion(0, 0, 0));
    }

    [Test]
    public async Task TryParse_KeepsPrereleaseAndBuildMetadata()
    {
        var parsed = SemanticVersion.TryParse("1.2.3-beta.1+sha", out var result);

        await parsed.Should().BeTrue();
        await result!.ToStandardVersionString().Should().BeEqualTo("1.2.3-beta.1+sha");
        await (result < new SemanticVersion(1, 2, 3)).Should().BeTrue();
    }

    [Test]
    public async Task Constructor_StillFallsBackToZeroForInvalidTag()
    {
        var version = new SemanticVersion("nightly");

        await version.ToStandardVersionString().Should().BeEqualTo("0.0.0");
    }
}
