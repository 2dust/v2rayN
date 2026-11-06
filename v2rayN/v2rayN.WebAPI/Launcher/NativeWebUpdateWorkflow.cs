namespace v2rayN.WebAPI.Launcher;

internal sealed record NativeWebUpdateTransactionResult(bool Success, bool RollbackSucceeded, string? Detail);

internal static class NativeWebUpdateWorkflow
{
    public static bool RuntimeIntentIsSatisfied(v2rayN.WebAPI.Services.RuntimeRestartIntent intent, WebHealthProbeResult health)
    {
        var hasCore = (health.CoreProcessIds?.Count ?? 0) > 0;
        if (!intent.WasRunning)
            return string.Equals(health.CoreState, "stopped", StringComparison.OrdinalIgnoreCase) && !hasCore;
        return string.Equals(health.CoreState, "running", StringComparison.OrdinalIgnoreCase)
            && hasCore
            && !string.IsNullOrWhiteSpace(health.CoreProfileId);
    }

    public static async Task<NativeWebUpdateTransactionResult> ApplyAsync(
        Func<Task> installCandidateAsync,
        Func<Task<bool>> startCandidateAndVerifyHealthAsync,
        Func<Task> restorePreviousFilesAsync,
        Func<Task<bool>> startPreviousAndVerifyHealthAsync,
        Action<string> setPhase,
        Func<Task> cleanBackupsAsync)
    {
        try
        {
            setPhase("installing");
            await installCandidateAsync();
            setPhase("restarting");
            setPhase("verifying-health");
            if (await startCandidateAndVerifyHealthAsync())
            {
                await cleanBackupsAsync();
                return new(true, false, null);
            }
        }
        catch (Exception installException)
        {
            setPhase("rolling-back");
            return await TryRestoreAndVerifyAsync(
                restorePreviousFilesAsync,
                startPreviousAndVerifyHealthAsync,
                cleanBackupsAsync,
                setPhase,
                installException.Message);
        }

        setPhase("rolling-back");
        return await TryRestoreAndVerifyAsync(
            restorePreviousFilesAsync,
            startPreviousAndVerifyHealthAsync,
            cleanBackupsAsync,
            setPhase,
            "The new Web application did not become healthy.");
    }

    private static async Task<NativeWebUpdateTransactionResult> TryRestoreAndVerifyAsync(
        Func<Task> restorePreviousFilesAsync,
        Func<Task<bool>> startPreviousAndVerifyHealthAsync,
        Func<Task> cleanBackupsAsync,
        Action<string> setPhase,
        string failure)
    {
        string? restoreFailure = null;
        try
        {
            await restorePreviousFilesAsync();
        }
        catch (Exception rollbackException)
        {
            restoreFailure = rollbackException.Message;
        }

        try
        {
            setPhase("restarting");
            setPhase("verifying-health");
            if (!await startPreviousAndVerifyHealthAsync())
                return new(false, false, $"{failure} The previous Web build did not become healthy. {restoreFailure}");
            if (restoreFailure is not null)
                return new(false, false, $"{failure} The previous Web process is responding, but restoring all previous app files failed: {restoreFailure}");
            await cleanBackupsAsync();
            return new(false, true, $"{failure} The previous Web build was restored and is healthy.");
        }
        catch (Exception restartException)
        {
            return new(false, false, $"{failure} Rollback/restart failed: {restoreFailure} {restartException.Message}");
        }
    }
}
