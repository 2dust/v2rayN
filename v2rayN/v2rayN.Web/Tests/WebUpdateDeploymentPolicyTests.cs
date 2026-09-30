using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class WebUpdateDeploymentPolicyTests
{
    [Test]
    public async Task OnlyWritableNativeSingleFileInstallCanApplyUpdates()
    {
        var native = WebUpdateDeploymentPolicy.Evaluate(true, false, false, true, true);
        await native.Kind.Should().BeEqualTo(WebDeploymentKind.NativeWritable);
        await native.CanCheck.Should().BeTrue();
        await native.CanInstall.Should().BeTrue();

        var readOnly = WebUpdateDeploymentPolicy.Evaluate(true, false, false, true, false);
        await readOnly.CanCheck.Should().BeTrue();
        await readOnly.CanInstall.Should().BeFalse();
        await readOnly.InstallReasonKey.Should().BeEqualTo("maintenance.webUpdateReadOnly");

        var systemd = WebUpdateDeploymentPolicy.Evaluate(true, false, true, true, false);
        await systemd.Kind.Should().BeEqualTo(WebDeploymentKind.SystemdManaged);
        await systemd.CanInstall.Should().BeFalse();
        await systemd.InstallReasonKey.Should().BeEqualTo("maintenance.webUpdateSystemdAdmin");

        var container = WebUpdateDeploymentPolicy.Evaluate(true, true, false, true, true);
        await container.Kind.Should().BeEqualTo(WebDeploymentKind.Container);
        await container.CanCheck.Should().BeTrue();
        await container.CanInstall.Should().BeFalse();

        var unsupported = WebUpdateDeploymentPolicy.Evaluate(false, false, false, false, true);
        await unsupported.CanCheck.Should().BeTrue();
        await unsupported.CanInstall.Should().BeFalse();
    }
}
