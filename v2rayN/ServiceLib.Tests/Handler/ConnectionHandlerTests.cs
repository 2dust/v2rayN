namespace ServiceLib.Tests.Handler;

public class ConnectionHandlerTests
{
    [Test]
    public async Task ParseGoogleRegion_ShouldReadCountryCode()
    {
        await ParseRegion("""{"GAPI_HOST":"https://apis.google.com","GAPI_LOCALE":"fi_FI","GL":"FI","GOOGLE_FEEDBACK_PRODUCT_ID":"59"}""")
            .Should().BeEqualTo("FI");
    }

    [Test]
    [Arguments("")]
    [Arguments("no region here")]
    [Arguments("{\"GL\": \"\"}")]
    [Arguments("{\"GL\":\"FIN\"}")]
    public async Task ParseGoogleRegion_ShouldReturnNullWithoutCountryCode(string html)
    {
        await ParseRegion(html).Should().BeNull();
    }

    private static string? ParseRegion(string html)
    {
        var method = typeof(ConnectionHandler).GetMethod("ParseGoogleRegion", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string?)method.Invoke(null, [html]);
    }
}