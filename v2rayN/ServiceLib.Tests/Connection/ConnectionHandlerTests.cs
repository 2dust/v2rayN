using AwesomeAssertions;
using System.Globalization;
using Xunit;

namespace ServiceLib.Tests.Connection;

public class ConnectionHandlerTests
{
    [Fact]
    public void ParseIPInfo_IpWhoIsResponse_ShouldIncludeRegionAndCity()
    {
        const string json = """
                            {
                              "ip": "192.0.2.10",
                              "country": "Example Country",
                              "country_code": "ZZ",
                              "region": "Example Region",
                              "city": "Example City"
                            }
                            """;

        var result = ConnectionHandler.ParseIPInfo(json);

        result.Should().NotBeNull();
        result!.Value.Country.Should().Be("ZZ");
        result.Value.Region.Should().Be("Example Region");
        result.Value.City.Should().Be("Example City");
        result.Value.Ip.Should().Be("192.0.2.10");
        result.Value.ToString().Should().Be("ZZ Example Region Example City 192.0.2.10");
    }

    [Fact]
    public void ParseIPInfo_ApiIpapiIsLocationResponse_ShouldUseNestedLocation()
    {
        const string json = """
                            {
                              "ip": "198.51.100.20",
                              "location": {
                                "country": "Example Country",
                                "country_code": "ZZ",
                                "state": "Example State",
                                "city": "Example City"
                              }
                            }
                            """;

        var result = ConnectionHandler.ParseIPInfo(json);

        result.Should().NotBeNull();
        result!.Value.Country.Should().Be("ZZ");
        result.Value.Region.Should().Be("Example State");
        result.Value.City.Should().Be("Example City");
        result.Value.Ip.Should().Be("198.51.100.20");
        result.Value.ToString().Should().Contain("Example State Example City 198.51.100.20");
    }

    [Fact]
    public void IpInfoResult_ToString_ShouldSkipDuplicateLocationParts()
    {
        var result = new IpInfoResult("ZZ", "203.0.113.30", "Example City", "Example City");

        result.ToString().Should().Be("ZZ Example City 203.0.113.30");
    }

    [Fact]
    public void ParseIPInfo_InvalidJson_ShouldReturnNull()
    {
        var result = ConnectionHandler.ParseIPInfo("not-json");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("https://ipwho.is/", "192.0.2.10", "https://ipwho.is/192.0.2.10")]
    [InlineData("https://api.ip.sb/geoip", "192.0.2.10", "https://api.ip.sb/geoip/192.0.2.10")]
    [InlineData("https://example.test/geo/{ip}", "192.0.2.10", "https://example.test/geo/192.0.2.10")]
    [InlineData("https://example.test/geo/{0}", "192.0.2.10", "https://example.test/geo/192.0.2.10")]
    [InlineData("https://api.ipapi.is", "192.0.2.10", "https://api.ipapi.is/?q=192.0.2.10")]
    public void BuildIPInfoUrlForAddress_ShouldTargetSpecificIp(string apiUrl, string ip, string expected)
    {
        var result = ConnectionHandler.BuildIPInfoUrlForAddress(apiUrl, ip);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task ResolveAddressForIPInfo_PublicIp_ShouldReturnIp()
    {
        var result = await ConnectionHandler.ResolveAddressForIPInfo("192.0.2.10");

        result.Should().Be("192.0.2.10");
    }

    [Fact]
    public async Task ResolveAddressForIPInfo_PrivateIp_ShouldReturnNull()
    {
        var result = await ConnectionHandler.ResolveAddressForIPInfo("127.0.0.1");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(42, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void AvailabilityCheckResult_IsAvailable_ShouldReflectPositiveResponseTime(int responseTime, bool expected)
    {
        var result = new AvailabilityCheckResult(responseTime, Global.None);

        result.IsAvailable.Should().Be(expected);
    }

    [Fact]
    public void SubItem_FormatUpdateTimeAgo_ShouldShowChineseRelativeTime()
    {
        var oldCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var updateTime = now.AddMinutes(-5).ToUnixTimeSeconds();

            SubItem.FormatUpdateTimeAgo(updateTime, now).Should().Be("5 分钟前");
            SubItem.FormatUpdateTimeAgo(0, now).Should().Be("未更新");
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }
}
