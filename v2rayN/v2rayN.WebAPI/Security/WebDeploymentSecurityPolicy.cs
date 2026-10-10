namespace v2rayN.WebAPI.Security;

public static class WebDeploymentSecurityPolicy
{
    public const string MissingManagementKeyMessage =
        "Management Key is required in supervised/container deployments. Set V2RAYN_WEB_API_KEY before starting v2rayN.WebAPI.";

    public static string? GetStartupError(bool supervisedEnvironment, bool containerEnvironment, string? managementKey) =>
        (supervisedEnvironment || containerEnvironment) && string.IsNullOrWhiteSpace(managementKey)
            ? MissingManagementKeyMessage
            : null;
}
