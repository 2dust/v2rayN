using v2rayN.Web.Security;

namespace v2rayN.Web.Tests;

public class WebDeploymentSecurityPolicyTests
{
    [Test]
    public async Task NativeInteractiveInstallCanStartWithoutEnvironmentKey()
    {
        await WebDeploymentSecurityPolicy.GetStartupError(false, false, null).Should().BeNull();
        await WebDeploymentSecurityPolicy.GetStartupError(false, false, "  ").Should().BeNull();
    }

    [Test]
    public async Task SupervisedDeploymentRequiresAnEnvironmentKey()
    {
        await WebDeploymentSecurityPolicy.GetStartupError(true, false, null)
            .Should().BeEqualTo(WebDeploymentSecurityPolicy.MissingManagementKeyMessage);
        await WebDeploymentSecurityPolicy.GetStartupError(true, false, "\t")
            .Should().BeEqualTo(WebDeploymentSecurityPolicy.MissingManagementKeyMessage);
        await WebDeploymentSecurityPolicy.GetStartupError(true, false, "a-configured-key")
            .Should().BeNull();
    }

    [Test]
    public async Task ContainerDeploymentRequiresAnEnvironmentKey()
    {
        await WebDeploymentSecurityPolicy.GetStartupError(false, true, null)
            .Should().BeEqualTo(WebDeploymentSecurityPolicy.MissingManagementKeyMessage);
        await WebDeploymentSecurityPolicy.GetStartupError(false, true, "a-configured-key")
            .Should().BeNull();
    }
}
