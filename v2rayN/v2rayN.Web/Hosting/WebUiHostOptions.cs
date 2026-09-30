namespace v2rayN.Web.Hosting;

public sealed record WebUiHostOptions(string? RootPath, string? ConfigurationError = null)
{
    public bool IsAvailable
    {
        get
        {
            if (RootPath is null) return false;
            try
            {
                return Directory.Exists(RootPath) && File.Exists(Path.Combine(RootPath, "index.html"));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }
    }

    public string? IndexFilePath => RootPath is null ? null : Path.Combine(RootPath, "index.html");

    public static WebUiHostOptions Resolve(string? applicationBaseDirectory, string? configuredPath)
    {
        // An explicitly empty value is the supported API-only switch. An unset value uses
        // the executable directory, never the process's current working directory.
        if (configuredPath is not null && string.IsNullOrWhiteSpace(configuredPath))
        {
            return new WebUiHostOptions(RootPath: null);
        }

        try
        {
            var applicationBase = Path.GetFullPath(applicationBaseDirectory ?? AppContext.BaseDirectory);
            var candidate = configuredPath is null
                ? Path.Combine(applicationBase, "webui")
                : Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(applicationBase, configuredPath);
            return new(Path.GetFullPath(candidate));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new WebUiHostOptions(null, "The configured WebUI path is invalid; static WebUI hosting is disabled.");
        }
    }
}
