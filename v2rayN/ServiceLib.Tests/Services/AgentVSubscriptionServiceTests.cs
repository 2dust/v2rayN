using Xunit;

namespace ServiceLib.Tests.Services;

public class AgentVSubscriptionServiceTests
{
    [Fact]
    public void Load_ReadsAndNormalizesHeaders()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("v2rayn-agent-v-");
        var filePath = Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.DefaultFileName);

        try
        {
            File.WriteAllText(filePath,
                "user_agent=Throne/1.1.6\n" +
                "x_hwid=test-hwid\n" +
                "x_device_os=Windows\n" +
                "# comment\n");

            var result = AgentVSubscriptionService.Load(filePath);

            Assert.Equal("Throne/1.1.6", result.UserAgent);
            Assert.Equal("test-hwid", result.Headers["x-hwid"]);
            Assert.Equal("Windows", result.Headers["x-device-os"]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_ReturnsEmptyWhenFileDoesNotExist()
    {
        var result = AgentVSubscriptionService.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void BuildRequestHeadersLog_ContainsUserAgentAndCustomHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-hwid"] = "test-hwid",
            ["x-device-os"] = "Windows"
        };

        var message = AgentVSubscriptionService.BuildRequestHeadersLog(
            "Throne/1.1.6",
            headers,
            false);

        Assert.Contains("SUBSCRIPTION REQUEST HEADERS", message);
        Assert.Contains("User-Agent=Throne/1.1.6", message);
        Assert.Contains("x-hwid=test-hwid", message);
        Assert.Contains("x-device-os=Windows", message);
    }

    [Fact]
    public void BuildRequestHeadersLog_LogsUserAgentWithoutAgentVHeaders()
    {
        var message = AgentVSubscriptionService.BuildRequestHeadersLog(
            "v2rayN/7.24.1",
            null,
            true);

        Assert.Contains("User-Agent=v2rayN/7.24.1", message);
        Assert.Contains("Authorization=Basic ***", message);
    }
}
