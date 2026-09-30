using v2rayN.Web.Contracts;

namespace v2rayN.Web.Tests;

public class WebApiCapabilitiesTests
{
    [Test]
    public async Task StatusCapabilitiesAreStableUniqueApiIdentifiers()
    {
        var capabilities = WebApiCapabilities.Current;

        await capabilities.Distinct(StringComparer.Ordinal).Count().Should().BeEqualTo(capabilities.Length);
        await capabilities.Should().Contain("events.sse");
        await capabilities.Should().Contain("editor.options");
        await capabilities.Should().Contain("static-webui");
    }
}
