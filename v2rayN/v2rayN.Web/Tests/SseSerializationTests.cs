using v2rayN.Web.Api;

namespace v2rayN.Web.Tests;

public class SseSerializationTests
{
    [Test]
    public async Task NullEventDataSerializesAsJsonNull()
    {
        await WebApiEndpoints.SerializeEventData(null).Should().BeEqualTo("null");
    }
}
