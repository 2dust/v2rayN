namespace ServiceLib.Services.AppRouting;

internal static class AppRoutingLifecycle
{
    public static async Task RestoreAsync(Config config, IAppRoutingRuntime runtime)
    {
        // Runtime shutdown/failure must not erase the user's startup preference.
        if (config.AppRouting.Enabled && !runtime.IsEnabled)
        {
            await runtime.StartAsync(config);
        }
    }

    public static async Task SetEnabledAsync(Config config, IAppRoutingRuntime runtime,
        Func<Config, Task<int>> save, bool enabled)
    {
        var previous = config.AppRouting.Enabled;
        config.AppRouting.Enabled = enabled;
        try
        {
            if (await save(config) != 0)
            {
                throw new IOException(ResUI.OperationFailed);
            }
        }
        catch
        {
            config.AppRouting.Enabled = previous;
            throw;
        }
        try
        {
            if (enabled)
            {
                await runtime.StartAsync(config);
            }
            else
            {
                await runtime.StopAsync();
            }
        }
        catch
        {
            config.AppRouting.Enabled = previous;
            if (await save(config) != 0)
            {
                throw new IOException(ResUI.OperationFailed);
            }

            throw;
        }
    }
}
