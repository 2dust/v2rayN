namespace v2rayN.Web.Security;

internal static class WebAuthStorage
{
    public static string MigrateAndGetPath(string startupPath, string legacyConfigPath)
    {
        var directory = Path.Combine(startupPath, "webData");
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var authPath = Path.Combine(directory, "web-auth.json");
        if (File.Exists(legacyConfigPath))
        {
            if (File.Exists(authPath))
            {
                File.Delete(legacyConfigPath);
            }
            else
            {
                File.Move(legacyConfigPath, authPath);
            }
        }

        if (OperatingSystem.IsLinux() && File.Exists(authPath))
        {
            File.SetUnixFileMode(authPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return authPath;
    }
}
