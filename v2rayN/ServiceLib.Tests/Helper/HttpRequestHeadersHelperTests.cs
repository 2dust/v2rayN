namespace ServiceLib.Tests.Helper;

public class HttpRequestHeadersHelperTests
{
    [Test]
    public async Task TryParse_ShouldAcceptEmptySettingsForExistingSubscriptions()
    {
        foreach (var json in new string?[] { null, "", " \r\n ", "{}" })
        {
            await HttpRequestHeadersHelper.TryParse(json, out var headers).Should().BeTrue();
            await headers.Count.Should().BeEqualTo(0);
        }
    }

    [Test]
    public async Task TryParse_ShouldPreserveValuesAndUseCaseInsensitiveNames()
    {
        const string json = """
            {
              "X-hwid": "my_test_device",
              "Authorization": "Bearer test:token",
              "accept": "application/json",
              "Content-Type": "application/json",
              "X-Empty": ""
            }
            """;

        await HttpRequestHeadersHelper.TryParse(json, out var headers).Should().BeTrue();
        await headers["x-HWID"].Should().BeEqualTo("my_test_device");
        await headers["AUTHORIZATION"].Should().BeEqualTo("Bearer test:token");
        await headers["Accept"].Should().BeEqualTo("application/json");
        await headers["Content-Type"].Should().BeEqualTo("application/json");
        await headers["X-Empty"].Should().BeEqualTo("");
    }

    [Test]
    [Arguments("not-json")]
    [Arguments("null")]
    [Arguments("[]")]
    [Arguments("{\"X-Test\": 1}")]
    [Arguments("{\"X-Test\": null}")]
    [Arguments("{\"X-Test\": [\"one\", \"two\"]}")]
    [Arguments("{\"X-Test\": \"one\", \"X-Test\": \"two\"}")]
    [Arguments("{\"Accept\": \"one\", \"accept\": \"two\"}")]
    [Arguments("{\"Bad Header\": \"value\"}")]
    [Arguments("{\"Bad:Header\": \"value\"}")]
    [Arguments("{\"\": \"value\"}")]
    [Arguments("{\"X-Test\": \"one\\r\\nInjected: two\"}")]
    [Arguments("{\"X-Test\": \"one\\nInjected: two\"}")]
    [Arguments("{\"X-Test\": \"one\\u0000two\"}")]
    public async Task TryParse_ShouldRejectInvalidHeadersWithoutReturningPartialSettings(string json)
    {
        await HttpRequestHeadersHelper.TryParse(json, out var headers).Should().BeFalse();
        await headers.Count.Should().BeEqualTo(0);
    }
}
