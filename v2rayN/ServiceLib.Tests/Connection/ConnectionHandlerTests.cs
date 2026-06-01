using AwesomeAssertions;
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
}
