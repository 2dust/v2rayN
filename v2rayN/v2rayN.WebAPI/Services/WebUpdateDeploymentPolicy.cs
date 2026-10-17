namespace v2rayN.WebAPI.Services;

internal enum WebDeploymentKind
{
    NativeWritable,
    SystemdManaged,
    Container,
    ReadOnly,
    Unsupported,
}

internal sealed record WebUpdateDeployment(WebDeploymentKind Kind, bool CanCheck, bool CanInstall, string? InstallReasonKey);

internal static class WebUpdateDeploymentPolicy
{
    public static WebUpdateDeployment Evaluate(
        bool isSupportedNativePlatform,
        bool isContainer,
        bool isSystemdManaged,
        bool isNativeSingleFile,
        bool installDirectoryWritable)
    {
        if (isContainer)
            return new(WebDeploymentKind.Container, true, false, "maintenance.webUpdateContainerImage");
        if (isSystemdManaged)
            return new(WebDeploymentKind.SystemdManaged, true, false, "maintenance.webUpdateSystemdAdmin");
        if (!isSupportedNativePlatform || !isNativeSingleFile)
            return new(WebDeploymentKind.Unsupported, true, false, "maintenance.webUpdateUnsupported");
        if (!installDirectoryWritable)
            return new(WebDeploymentKind.ReadOnly, true, false, "maintenance.webUpdateReadOnly");
        return new(WebDeploymentKind.NativeWritable, true, true, null);
    }
}
