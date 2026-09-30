using ServiceLib.Enums;
using ServiceLib.Handler.Builder;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;
using v2rayN.Web.Contracts;

namespace v2rayN.Web.Services;

internal sealed record CoreLaunchPreflightResult(
    ProfileItem Profile,
    CoreConfigContextBuilderAllResult BuiltContext,
    OperationView? Failure)
{
    public bool Success => Failure is null;
}

internal static class CoreLaunchPreflight
{
    public static async Task<CoreLaunchPreflightResult> BuildAndValidateAsync(
        Config config,
        ProfileItem profile,
        Func<ECoreType, OperationView?> validateCoreBinary)
    {
        var built = await CoreConfigContextBuilder.BuildAll(config, profile);
        return Validate(profile, built, validateCoreBinary);
    }

    internal static CoreLaunchPreflightResult Validate(
        ProfileItem profile,
        CoreConfigContextBuilderAllResult built,
        Func<ECoreType, OperationView?> validateCoreBinary)
    {
        var tunRejection = GetTunLaunchRejection(built);
        if (tunRejection is not null)
        {
            return new CoreLaunchPreflightResult(profile, built, tunRejection);
        }

        if (!built.Success)
        {
            return new CoreLaunchPreflightResult(profile, built,
                OperationView.Fail("profile_validation_failed", ApiMessageKeys.ProfileInvalid));
        }

        var requiredCoreTypes = new[]
            {
                (ECoreType?)built.MainResult.Context.RunCoreType,
                built.PreSocksResult?.Context.RunCoreType,
            }
            .Where(coreType => coreType.HasValue)
            .Select(coreType => coreType!.Value)
            .Distinct();

        foreach (var coreType in requiredCoreTypes)
        {
            var failure = validateCoreBinary(coreType);
            if (failure is not null)
            {
                return new CoreLaunchPreflightResult(profile, built, failure);
            }
        }

        return new CoreLaunchPreflightResult(profile, built, null);
    }

    internal static OperationView? GetTunLaunchRejection(CoreConfigContextBuilderAllResult built) =>
        built.MainResult.Context.IsTunEnabled || built.PreSocksResult?.Context.IsTunEnabled == true
            ? OperationView.Fail("tun_not_supported", ApiMessageKeys.CoreTunNotSupported)
            : null;
}

internal static class CoreRestartFlow
{
    public static async Task<OperationView> ExecuteAsync(
        Func<Task<CoreLaunchPreflightResult>> buildAndValidate,
        Func<Task> stopCore,
        Action markCoreStopped,
        Func<CoreLaunchPreflightResult, Task<OperationView>> launch)
    {
        var preflight = await buildAndValidate();
        if (preflight.Failure is { } failure)
        {
            return failure;
        }

        await stopCore();
        markCoreStopped();
        var startResult = await launch(preflight);
        return startResult.Success
            ? OperationView.Ok(ApiMessageKeys.CoreRestarted, startResult.Data)
            : startResult;
    }
}
