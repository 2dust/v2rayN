namespace v2rayN.WebAPI.Launcher;

public enum WebLaunchMode
{
    Stop,
    Foreground,
    BackgroundLauncher,
    BackgroundChild,
}

public sealed record WebLaunchOptions(WebLaunchMode Mode, bool NoOpen, string[] HostArguments)
{
    public const string StopFlag = "--stop";
    public const string ForegroundFlag = "--foreground";
    public const string BackgroundFlag = "--background";
    public const string NoOpenFlag = "--no-open";
    public const string BackgroundChildFlag = "--background-child";
    public const string HelpFlag = "--help";
    public const string HelpShortFlag = "-h";

    public static WebLaunchOptions Parse(
        string[] arguments,
        bool isLinux,
        bool daemonEnvironment,
        bool containerEnvironment,
        bool interactiveLaunch)
    {
        var mode = arguments.Contains(StopFlag, StringComparer.Ordinal)
            ? WebLaunchMode.Stop
            : arguments.Contains(BackgroundChildFlag, StringComparer.Ordinal)
                ? WebLaunchMode.BackgroundChild
                : arguments.Contains(ForegroundFlag, StringComparer.Ordinal)
                    ? WebLaunchMode.Foreground
                    : arguments.Contains(BackgroundFlag, StringComparer.Ordinal)
                        ? WebLaunchMode.BackgroundLauncher
                        : isLinux && !containerEnvironment && (!daemonEnvironment || interactiveLaunch)
                            ? WebLaunchMode.BackgroundLauncher
                            : WebLaunchMode.Foreground;

        var hostArguments = arguments
            .Where(argument => argument is not StopFlag
                and not ForegroundFlag
                and not BackgroundFlag
                and not NoOpenFlag
                and not BackgroundChildFlag
                and not HelpFlag
                and not HelpShortFlag)
            .ToArray();
        return new WebLaunchOptions(
            mode,
            arguments.Contains(NoOpenFlag, StringComparer.Ordinal),
            hostArguments);
    }
}

public static class LauncherEnvironment
{
    // Set when the launch chain started in an interactive terminal and inherited
    // by every later child, helper, and replacement process. Terminals that were
    // started by systemd export INVOCATION_ID/JOURNAL_STREAM to everything they
    // run, so the launch context is recorded once instead of being probed again.
    public const string InteractiveLaunchEnvironmentVariable = "V2RAYN_WEB_INTERACTIVE_LAUNCH";

    public static bool IsDaemonEnvironment(IReadOnlyDictionary<string, string?> environment)
    {
        return HasValue(environment, "INVOCATION_ID")
            || HasValue(environment, "JOURNAL_STREAM")
            || HasValue(environment, "NOTIFY_SOCKET");
    }

    public static bool IsContainerEnvironment(IReadOnlyDictionary<string, string?> environment) =>
        HasValue(environment, "DOTNET_RUNNING_IN_CONTAINER")
        || HasValue(environment, "container");

    public static bool IsInteractiveLaunchMarker(IReadOnlyDictionary<string, string?> environment) =>
        HasValue(environment, InteractiveLaunchEnvironmentVariable);

    /// <summary>
    /// True when this process runs with an interactive terminal attached or the
    /// launch chain was marked as interactive by the process that had one.
    /// </summary>
    public static bool IsInteractiveLaunch(IReadOnlyDictionary<string, string?> environment) =>
        !Console.IsInputRedirected || IsInteractiveLaunchMarker(environment);

    /// <summary>
    /// True when systemd markers or the service cgroup claim this process, unless
    /// the markers merely leaked into a terminal (or its detached children),
    /// where no external supervisor exists.
    /// </summary>
    public static bool IsSystemdManagedDeployment()
    {
        if (WebStopper.IsManagedBySystemd(Environment.ProcessId))
        {
            return true;
        }

        var environment = ReadCurrentEnvironment();
        return IsDaemonEnvironment(environment) && !IsInteractiveLaunch(environment);
    }

    public static Dictionary<string, string?> ReadCurrentEnvironment() =>
        Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

    private static bool HasValue(IReadOnlyDictionary<string, string?> environment, string key) =>
        environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
}

public enum LauncherLocale
{
    SimplifiedChinese,
    TraditionalChinese,
    English,
}

public static class LauncherMessages
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> MessageCache = new(StringComparer.Ordinal);

    public static LauncherLocale ResolveLocale(params string?[] localeValues)
    {
        var locale = localeValues.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var normalized = locale.Replace('_', '-');
        if (normalized.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return LauncherLocale.TraditionalChinese;
        }

        return normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? LauncherLocale.SimplifiedChinese
            : LauncherLocale.English;
    }

    public static string Started(string url, string command, LauncherLocale locale) =>
        Format("started", locale, url, command);

    public static string AlreadyRunning(string url, string command, bool browserOpened, LauncherLocale locale) =>
        Format(browserOpened ? "alreadyRunningOpening" : "alreadyRunning", locale, url, command);

    public static string ForegroundStarted(string url, LauncherLocale locale) => Format("foregroundStarted", locale, url);

    public static string StopMessage(WebStopResult result, LauncherLocale locale, string? lastStage = null)
    {
        var message = Get(result switch
        {
            WebStopResult.NotRunning => "stopNotRunning",
            WebStopResult.Stopped => "stopSucceeded",
            WebStopResult.ProbeUnavailable => "stopUnprobeable",
            WebStopResult.IdentityUnverified => "stopIdentityUnverified",
            WebStopResult.SupervisorManaged => "stopSupervisorManaged",
            WebStopResult.CoreProcessStillRunning => "stopCoreStillRunning",
            WebStopResult.SignalFailed => "stopSignalFailed",
            WebStopResult.TimedOut => "stopTimedOut",
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        }, locale);

        return result == WebStopResult.TimedOut && !string.IsNullOrWhiteSpace(lastStage)
            ? $"{message} {Get("stopTimedOutStage", locale).Replace("{stage}", lastStage, StringComparison.Ordinal)}"
            : message;
    }

    public static string StopUnsupported(LauncherLocale locale) => Get("stopUnsupported", locale);

    public static string Help(LauncherLocale locale) => Get("help", locale);

    public static string StartFailed(LauncherLocale locale) => Get("startFailed", locale);

    public static string StartUnprobeable(LauncherLocale locale) => Get("startUnprobeable", locale);

    public static string ExistingUnhealthy(LauncherLocale locale) => Get("existingUnhealthy", locale);

    public static string InstanceInUse(LauncherLocale locale) => Get("instanceInUse", locale);

    public static string ExecutableCommand(string executablePath, string? managedEntryPoint = null)
    {
        if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(managedEntryPoint)
            && managedEntryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{executablePath}\" \"{Path.GetFullPath(managedEntryPoint)}\"";
        }

        return $"./{Path.GetFileName(executablePath)}";
    }

    private static string Format(string key, LauncherLocale locale, string url, string? command = null) =>
        Get(key, locale)
            .Replace("{url}", url, StringComparison.Ordinal)
            .Replace("{command}", command ?? string.Empty, StringComparison.Ordinal);

    private static string Get(string key, LauncherLocale locale)
    {
        var localeName = locale switch
        {
            LauncherLocale.SimplifiedChinese => "zh-CN",
            LauncherLocale.TraditionalChinese => "zh-TW",
            _ => "en-US",
        };
        return MessageCache.GetOrAdd($"{localeName}:{key}", _ => ReadMessage(localeName, key));
    }

    private static string ReadMessage(string localeName, string key)
    {
        var resourceName = $"v2rayN.WebAPI.Launcher.Locales.{localeName}.json";
        using var stream = typeof(LauncherMessages).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded locale resource: {resourceName}");
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("launcher").GetProperty(key).GetString()
            ?? throw new InvalidOperationException($"Missing launcher locale key: {localeName}.{key}");
    }
}
