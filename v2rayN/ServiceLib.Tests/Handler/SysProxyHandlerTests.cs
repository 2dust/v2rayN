using Xunit;

namespace ServiceLib.Tests.Handler;

public class SysProxyHandlerTests
{
    [Theory]
    [InlineData(ESysProxyType.ForcedClear, ESysProxyType.ForcedClear)]
    [InlineData(ESysProxyType.ForcedChange, ESysProxyType.ForcedChange)]
    [InlineData(ESysProxyType.Unchanged, ESysProxyType.Unchanged)]
    [InlineData(ESysProxyType.Pac, ESysProxyType.Pac)]
    public void GetEffectiveProxyType_PreservesConfiguredTypeWhenTunIsInactive(
        ESysProxyType configuredType,
        ESysProxyType expectedType)
    {
        var result = SysProxyHandler.GetEffectiveProxyType(configuredType, false, false, false);

        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData(ESysProxyType.ForcedClear)]
    [InlineData(ESysProxyType.ForcedChange)]
    [InlineData(ESysProxyType.Pac)]
    public void GetEffectiveProxyType_ClearsManagedProxyWhenTunIsActive(ESysProxyType configuredType)
    {
        var result = SysProxyHandler.GetEffectiveProxyType(configuredType, false, true, true);

        Assert.Equal(ESysProxyType.ForcedClear, result);
    }

    [Fact]
    public void GetEffectiveProxyType_PreservesUnchangedModeWhenTunIsActive()
    {
        var result = SysProxyHandler.GetEffectiveProxyType(ESysProxyType.Unchanged, false, true, true);

        Assert.Equal(ESysProxyType.Unchanged, result);
    }

    [Theory]
    [InlineData(ESysProxyType.ForcedClear)]
    [InlineData(ESysProxyType.ForcedChange)]
    [InlineData(ESysProxyType.Unchanged)]
    [InlineData(ESysProxyType.Pac)]
    public void GetEffectiveProxyType_AllowsManualProxyChoiceWhileTunIsActive(
        ESysProxyType configuredType)
    {
        var result = SysProxyHandler.GetEffectiveProxyType(configuredType, false, true, false);

        Assert.Equal(configuredType, result);
    }

    [Theory]
    [InlineData(ESysProxyType.ForcedClear)]
    [InlineData(ESysProxyType.ForcedChange)]
    [InlineData(ESysProxyType.Pac)]
    public void GetEffectiveProxyType_ForceDisableOverridesManualTunChoice(
        ESysProxyType configuredType)
    {
        var result = SysProxyHandler.GetEffectiveProxyType(configuredType, true, true, false);

        Assert.Equal(ESysProxyType.ForcedClear, result);
    }
}
