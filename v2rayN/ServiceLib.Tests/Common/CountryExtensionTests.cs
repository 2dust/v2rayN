using AwesomeAssertions;
using ServiceLib.Common;
using ServiceLib.Models.Dto;
using Xunit;

namespace ServiceLib.Tests.Common;

public class CountryExtensionTests
{
    [Theory]
    [InlineData("DE", "DE")]
    [InlineData("de", "DE")]
    [InlineData("Us", "US")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("unknown", null)]
    [InlineData("D1", null)]
    [InlineData("ДЕ", null)]
    public void NormalizeCountryCode_ShouldReturnUppercaseAsciiAlpha2OrNull(string? value, string? expected)
    {
        value.NormalizeCountryCode().Should().Be(expected);
    }

    [Fact]
    public void IpInfoResult_ToString_ShouldNormalizeCodeWithoutEmoji()
    {
        var result = new IpInfoResult("de", "203.0.113.1");

        result.CountryCode.Should().Be("DE");
        result.ToString().Should().Be("(DE) 203.0.113.1");
    }

    [Fact]
    public void NormalizeStoredIpInfo_ShouldRemoveLegacyEmojiAndExtractCode()
    {
        var normalized = "🇩🇪(DE) 203.0.113.1".NormalizeStoredIpInfo(out var countryCode);

        normalized.Should().Be("(DE) 203.0.113.1");
        countryCode.Should().Be("DE");
    }

    [Theory]
    [InlineData("(de) 203.0.113.1", "(de) 203.0.113.1", "DE")]
    [InlineData("none", "none", null)]
    [InlineData("prefix(DE) 203.0.113.1", "prefix(DE) 203.0.113.1", null)]
    [InlineData("ABCD(DE) 203.0.113.1", "ABCD(DE) 203.0.113.1", null)]
    public void NormalizeStoredIpInfo_ShouldPreserveNonLegacyText(string value, string expected, string? expectedCode)
    {
        var normalized = value.NormalizeStoredIpInfo(out var countryCode);

        normalized.Should().Be(expected);
        countryCode.Should().Be(expectedCode);
    }
}
