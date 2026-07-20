using Xunit;

namespace ServiceLib.Tests.Common;

public class StartupArgumentHelperTests
{
    [Fact]
    public void ParseTunDelay_ReturnsNotSpecifiedWhenOptionIsAbsent()
    {
        var result = StartupArgumentHelper.ParseTunDelay(["rebootas"]);

        Assert.False(result.IsSpecified);
        Assert.Equal(0, result.DelaySeconds);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void ParseTunDelay_ReadsDelayInSeconds()
    {
        var result = StartupArgumentHelper.ParseTunDelay(["-tundelay", "30"]);

        Assert.True(result.IsSpecified);
        Assert.Equal(30, result.DelaySeconds);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void ParseTunDelay_ZeroMeansManualEnableOnly()
    {
        var result = StartupArgumentHelper.ParseTunDelay(["-tundelay", "0"]);

        Assert.True(result.IsSpecified);
        Assert.Equal(0, result.DelaySeconds);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void ParseTunDelay_SupportsEqualsSyntax()
    {
        var result = StartupArgumentHelper.ParseTunDelay(["-tundelay=15"]);

        Assert.True(result.IsSpecified);
        Assert.Equal(15, result.DelaySeconds);
        Assert.Null(result.ParseError);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void ParseTunDelay_InvalidValueDisablesAutomaticEnable(string value)
    {
        var result = StartupArgumentHelper.ParseTunDelay(["-tundelay", value]);

        Assert.True(result.IsSpecified);
        Assert.Equal(0, result.DelaySeconds);
        Assert.NotNull(result.ParseError);
    }

    [Fact]
    public void ParseTunDelay_MissingValueDisablesAutomaticEnable()
    {
        var result = StartupArgumentHelper.ParseTunDelay(["-tundelay"]);

        Assert.True(result.IsSpecified);
        Assert.Equal(0, result.DelaySeconds);
        Assert.NotNull(result.ParseError);
    }
}
