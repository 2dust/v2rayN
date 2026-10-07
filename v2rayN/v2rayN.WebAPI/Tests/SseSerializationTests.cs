using v2rayN.WebAPI.Api;

namespace v2rayN.WebAPI.Tests;

public class SseSerializationTests
{
    [Test]
    public async Task NullEventDataSerializesAsJsonNull()
    {
        await WebApiEndpoints.SerializeEventData(null).Should().BeEqualTo("null");
    }
}
