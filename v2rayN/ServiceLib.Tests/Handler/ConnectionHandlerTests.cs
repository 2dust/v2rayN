namespace ServiceLib.Tests.Handler;

public class ConnectionHandlerTests
{
    private const string _traceContent = "fl=270f180\r\nh=1.1.1.1\r\nip=104.28.222.16\r\nts=1792135653.000\r\ncolo=MXP\r\nloc=FI\r\ntls=TLSv1.3\r\n";

    [Test]
    public async Task ParseCloudflareTrace_ShouldReadIpAndCountry()
    {
        var result = Parse(_traceContent);

        await result.HasValue.Should().BeTrue();
        await result!.Value.Ip.Should().BeEqualTo("104.28.222.16");
        await result.Value.Country.Should().BeEqualTo("FI");
    }

    [Test]
    [Arguments("")]
    [Arguments("no trace here")]
    [Arguments("ip=\nloc=FI")]
    public async Task ParseCloudflareTrace_ShouldReturnNullWithoutIp(string content)
    {
        await Parse(content).HasValue.Should().BeFalse();
    }

    [Test]
    public async Task ParseCloudflareTrace_ShouldFallbackCountryToNone()
    {
        var result = Parse("ip=104.28.222.16\ncolo=MXP");

        await result.HasValue.Should().BeTrue();
        await result!.Value.Country.Should().BeEqualTo(Global.None);
    }

    [Test]
    [Arguments(Global.CloudflareTraceUrl, true)]
    [Arguments("https://1.0.0.1/cdn-cgi/trace", true)]
    [Arguments("https://1.1.1.1/cdn-cgi/trace/", true)]
    [Arguments("https://www.google.com/generate_204", false)]
    [Arguments("https://1.1.1.1/other", false)]
    [Arguments("not an url", false)]
    [Arguments("", false)]
    public async Task IsCloudflareTraceUrl_ShouldMatchOnlyTraceEndpoint(string url, bool expected)
    {
        await (IsTrace(url) == expected).Should().BeTrue();
    }

    private static IpInfoResult? Parse(string content)
    {
        var method = typeof(ConnectionHandler).GetMethod("ParseCloudflareTrace", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IpInfoResult?)method.Invoke(null, [content]);
    }

    private static bool IsTrace(string url)
    {
        var method = typeof(ConnectionHandler).GetMethod("IsCloudflareTraceUrl", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (bool)method.Invoke(null, [url])!;
    }
}