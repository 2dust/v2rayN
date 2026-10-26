namespace v2rayN.WebAPI.Security;

internal static class WebAuthStorage
{
    public static string MigrateAndGetPath(string startupPath, string legacyConfigPath)
    {
        var directory = Path.Combine(startupPath, "WebAPIData");
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var authPath = Path.Combine(directory, "WebAPI-auth.json");
        // Pre-rename locations that can still hold the verifier on existing installs:
        // the previous private location and the original guiConfigs location.
        var previousPrivatePath = Path.Combine(startupPath, "webData", "web-auth.json");
        if (!File.Exists(authPath))
        {
            if (File.Exists(previousPrivatePath))
            {
                File.Move(previousPrivatePath, authPath);
            }
            else if (File.Exists(legacyConfigPath))
            {
                File.Move(legacyConfigPath, authPath);
            }
        }

        // Stale pre-rename copies must not survive next to the current verifier.
        if (File.Exists(previousPrivatePath))
        {
            File.Delete(previousPrivatePath);
        }
        if (File.Exists(legacyConfigPath))
        {
            File.Delete(legacyConfigPath);
        }

        if (OperatingSystem.IsLinux() && File.Exists(authPath))
        {
            File.SetUnixFileMode(authPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return authPath;
    }
}
