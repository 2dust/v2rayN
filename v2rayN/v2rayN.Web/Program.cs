using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceLib;
using ServiceLib.Common;
using v2rayN.Web.Api;
using v2rayN.Web.Configuration;
using v2rayN.Web.Contracts;
using v2rayN.Web.Hosting;
using v2rayN.Web.Launcher;
using v2rayN.Web.Security;
using v2rayN.Web.Services;

internal static class Program
{
    private const string ManagementKeyEnvironmentVariable = "V2RAYN_WEB_API_KEY";
    private const string WebUiPathEnvironmentVariable = "V2RAYN_WEB_UI_PATH";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            NativeEnvironmentFile.LoadFromExecutableDirectory(AppContext.BaseDirectory);
        }
        catch (NativeEnvironmentConfigurationException exception)
        {
            Console.Error.WriteLine($"Invalid v2rayN.Web environment configuration: {exception.Message}");
            return 1;
        }

        if (args.Length == 2 && args[0] == "--apply-web-update")
        {
            return await NativeWebUpdateHelper.RunAsync(args[1]);
        }

        var environment = ReadEnvironment();
        var daemonEnvironment = LauncherEnvironment.IsDaemonEnvironment(environment);
        var containerEnvironment = IsContainerEnvironment();
        var managementKey = environment.GetValueOrDefault(ManagementKeyEnvironmentVariable);
        if (WebDeploymentSecurityPolicy.GetStartupError(daemonEnvironment, containerEnvironment, managementKey) is { } startupError)
        {
            Console.Error.WriteLine(startupError);
            return 1;
        }

        PrepareDataScope();
        var launchOptions = WebLaunchOptions.Parse(
            args,
            OperatingSystem.IsLinux(),
            daemonEnvironment,
            containerEnvironment);

        if (launchOptions.Mode == WebLaunchMode.Stop)
        {
            if (!OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine(LauncherMessages.StopUnsupported(GetLauncherLocale()));
                return 1;
            }

            var (stopHealthUri, _) = GetLauncherUris(launchOptions.HostArguments);
            var stopper = new WebStopper(new HttpWebHealthProbe(), new LinuxProcessSignalSender());
            var result = await stopper.StopAsync(GetInstanceLockPath(), stopHealthUri);
            Console.Out.WriteLine(LauncherMessages.StopMessage(result, GetLauncherLocale(), stopper.LastObservedShutdownStage));
            return result is WebStopResult.NotRunning or WebStopResult.Stopped ? 0 : 1;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = launchOptions.HostArguments,
            ContentRootPath = AppContext.BaseDirectory,
        });
        WebListenerSecurityPolicy.ApplyDefault(builder);
        if (WebListenerSecurityPolicy.GetStartupError(builder.Configuration, managementKey) is { } listenerError)
        {
            Console.Error.WriteLine(listenerError);
            return 1;
        }

        if (launchOptions.Mode == WebLaunchMode.BackgroundLauncher)
        {
            if (!OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine(LauncherMessages.StartFailed(GetLauncherLocale()));
                return 1;
            }

            var (healthUri, webUiUri) = GetLauncherUris(launchOptions.HostArguments);
            var launcher = new WebLauncher(new HttpWebHealthProbe(), new LinuxXdgBrowserOpener(), GetLauncherLocale());
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                Console.Error.WriteLine(LauncherMessages.StartFailed(GetLauncherLocale()));
                return 1;
            }

            var lockPath = GetInstanceLockPath();
            return await launcher.RunAsync(
                processPath,
                launchOptions.HostArguments,
                lockPath,
                healthUri,
                webUiUri,
                launchOptions.NoOpen);
        }

        if (launchOptions.Mode == WebLaunchMode.BackgroundChild)
        {
            BackgroundChildProcess.DetachStandardHandles();
        }

        var instanceLockPath = GetInstanceLockPath();
        if (!WebInstanceLock.TryAcquire(instanceLockPath, writeOwner: true, out var instanceLock))
        {
            if (launchOptions.Mode == WebLaunchMode.BackgroundChild)
            {
                return 73;
            }

            Console.Error.WriteLine(LauncherMessages.InstanceInUse(GetLauncherLocale()));
            return 73;
        }

        WebHostRunResult hostResult;
        using (instanceLock)
        {
            var showForegroundPrompt = launchOptions.Mode == WebLaunchMode.Foreground
                && args.Contains(WebLaunchOptions.ForegroundFlag, StringComparer.Ordinal)
                && !daemonEnvironment
                && !containerEnvironment;
            hostResult = await RunWebHostAsync(builder, launchOptions.HostArguments, showForegroundPrompt);
        }

        // The host cleanup is complete and the old owner has disposed this lock. The
        // handoff still verifies the lock state before allowing a replacement to start.
        var exitAction = hostResult.RestoreAndRestartRequested
            ? WebLifecycleAction.RestoreAndRestart
            : WebLifecycleAction.Shutdown;
        var lifecyclePlan = WebLifecyclePlanner.Create(
            exitAction,
            OperatingSystem.IsLinux(),
            daemonEnvironment || WebStopper.IsManagedBySystemd(Environment.ProcessId),
            containerEnvironment);
        if (lifecyclePlan.ShouldStartReplacement)
        {
            var processPath = Environment.ProcessPath;
            var commandLine = Environment.GetCommandLineArgs();
            var command = WebReplacementCommand.Create(
                LinuxXdgBrowserOpener.FindExecutable("setsid"),
                processPath,
                WebReplacementCommand.GetManagedEntryPoint(processPath, commandLine),
                launchOptions.HostArguments);
            if (command is null)
            {
                WriteNativeRestartDiagnostic("Restore requested a native Web restart, but the executable entry point or setsid could not be resolved.");
                return 1;
            }

            var coordinator = new NativeWebRestartCoordinator(
                new HttpWebHealthProbe(),
                new LinuxReplacementProcessStarter(),
                new FileWebInstanceOwnershipProbe());
            var handoff = await coordinator.StartReplacementAfterOwnerReleaseAsync(
                GetInstanceLockPath(),
                GetLauncherUris(launchOptions.HostArguments).HealthUri,
                command);
            WriteNativeRestartDiagnostic(handoff.Message, handoff.Success, handoff.ReplacementProcessId);
            return handoff.Success ? hostResult.ExitCode : 1;
        }
        return hostResult.ExitCode;
    }

    private static async Task<WebHostRunResult> RunWebHostAsync(WebApplicationBuilder builder, string[] args, bool showForegroundPrompt)
    {
        var configPath = WebAuthStorage.MigrateAndGetPath(
            Utils.StartupPath(),
            Utils.GetConfigPath("web-auth.json"));
        var webAuth = new WebAuthService(configPath, Environment.GetEnvironmentVariable(ManagementKeyEnvironmentVariable));
        var applicationBase = AppContext.BaseDirectory;
        var webUiOptions = WebUiHostOptions.Resolve(
            applicationBase,
            Environment.GetEnvironmentVariable(WebUiPathEnvironmentVariable));
        // EventSource carries only a one-time, short-lived SSE ticket in its URL; keep
        // request lifecycle logs from recording that ticket as well.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Services.AddSingleton<EventHub>();
        builder.Services.AddSingleton<LogBuffer>();
        builder.Services.AddSingleton<RuntimeOperationCoordinator>();
        builder.Services.AddSingleton<V2rayRuntime>();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = RuntimeShutdownBudgets.HostShutdown);
        builder.Services.AddHostedService<V2rayHostedService>(services =>
            new V2rayHostedService(
                services.GetRequiredService<V2rayRuntime>(),
                services.GetRequiredService<WebAuthService>()));
        builder.Services.AddSingleton(webAuth);
        builder.Services.AddSingleton<WebSessionService>();
        builder.Services.AddRateLimiter(WebAuthRateLimiting.Configure);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        var app = builder.Build();
        var runtime = app.Services.GetRequiredService<V2rayRuntime>();
        app.UseMiddleware<WebSecurityHeadersMiddleware>();
        app.UseMiddleware<WebAuthRequestBodyLimitMiddleware>();
        app.UseReplaceableWebUi(webUiOptions);
        app.UseRouting();
        app.UseRateLimiter();

        app.UseMiddleware<WebSessionAuthenticationMiddleware>();

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (BadHttpRequestException exception) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
            {
                app.Logger.LogInformation(exception, "Invalid API request.");
                var isPayloadTooLarge = exception.StatusCode == StatusCodes.Status413PayloadTooLarge;
                context.Response.StatusCode = isPayloadTooLarge ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(
                    isPayloadTooLarge ? "payload_too_large" : "request_invalid",
                    ApiMessageKeys.CommonInvalidInput));
            }
            catch (JsonException exception) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
            {
                app.Logger.LogInformation(exception, "Invalid API JSON payload.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail("json_invalid", ApiMessageKeys.CommonInvalidInput));
            }
            catch (OperationCanceledException) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            catch (Exception exception) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
            {
                app.Logger.LogError(exception, "Unhandled API request failure.");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail("internal_error", ApiMessageKeys.CommonInternal));
            }
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path == "/api/health"
                || context.Request.Path == "/api/events"
                || context.Request.Path.StartsWithSegments("/api/setup")
                || context.Request.Path.StartsWithSegments("/api/auth"))
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
            var leaseKind = RuntimeRequestOperationPolicy.Classify(context.Request.Method, path);
            if (leaseKind == RuntimeRequestOperationKind.Background)
            {
                await next();
                return;
            }

            var operations = context.RequestServices.GetRequiredService<RuntimeOperationCoordinator>();
            await using var operation = leaseKind switch
            {
                RuntimeRequestOperationKind.Exclusive => await operations.EnterExclusiveAsync(context.RequestAborted),
                RuntimeRequestOperationKind.ExclusiveReadOnly => await operations.EnterExclusiveAsync(
                    context.RequestAborted,
                    allowReadOnlyObservations: true),
                RuntimeRequestOperationKind.Observation => await operations.EnterObservationAsync(context.RequestAborted),
                _ => await operations.EnterOperationAsync(context.RequestAborted),
            };
            context.RequestAborted = operation.Token;
            await next();
        });

        app.MapWebApi();
        app.MapWebAuthEndpoints();
        app.MapWebSetupEndpoints();
        app.MapFallback("/api/{**path}", () =>
            Results.NotFound(ApiEnvelope<object>.Fail("route_not_found", ApiMessageKeys.CommonRouteNotFound)));

        if (webUiOptions.ConfigurationError is { } webUiError)
        {
            app.Logger.LogWarning("{WebUiError}", webUiError);
        }

        if (showForegroundPrompt)
        {
            var (_, webUiUri) = GetLauncherUris(args);
            app.Lifetime.ApplicationStarted.Register(() =>
                Console.Out.WriteLine(LauncherMessages.ForegroundStarted(webUiUri.ToString(), GetLauncherLocale())));
        }

        await app.RunAsync();
        return new WebHostRunResult(0, runtime.RestoreAndRestartRequested);
    }

    private static void WriteNativeRestartDiagnostic(string message, bool success = false, int? processId = null)
    {
        var line = $"{DateTimeOffset.UtcNow:O} {(success ? "INFO" : "ERROR")} {message}"
            + (processId.HasValue ? $" ProcessId={processId.Value}." : string.Empty)
            + Environment.NewLine;
        if (!success) Console.Error.WriteLine(message);
        try
        {
            var path = Utils.GetLogPath("native-restore-restart.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!success) Console.Error.WriteLine($"Could not persist the native restart diagnostic: {exception.Message}");
        }
    }

    private sealed record WebHostRunResult(int ExitCode, bool RestoreAndRestartRequested);

    private static void PrepareDataScope()
    {
        var dataHome = Environment.GetEnvironmentVariable("V2RAYN_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(dataHome))
        {
            var fullDataHome = Path.GetFullPath(dataHome);
            Directory.CreateDirectory(fullDataHome);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", fullDataHome);
            Environment.SetEnvironmentVariable(Global.LocalAppData, "1");
            return;
        }

        if (!Utils.HasWritePermission())
        {
            Environment.SetEnvironmentVariable(Global.LocalAppData, "1");
        }
    }

    private static string GetInstanceLockPath() =>
        Path.Combine(Utils.StartupPath(), "v2rayN.Web.instance.lock");

    private static (Uri HealthUri, Uri WebUiUri) GetLauncherUris(string[] hostArguments)
    {
        var configuredUrl = hostArguments
            .Select((argument, index) => (argument, index))
            .Where(item => item.argument == "--urls" && item.index + 1 < hostArguments.Length)
            .Select(item => hostArguments[item.index + 1])
            .FirstOrDefault()
            ?? hostArguments.FirstOrDefault(argument => argument.StartsWith("--urls=", StringComparison.Ordinal))?[7..]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(port => $"http://127.0.0.1:{port}").FirstOrDefault();

        var baseUri = new Uri("http://127.0.0.1:5080");
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            foreach (var item in configuredUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = item.Replace("*", "127.0.0.1", StringComparison.Ordinal)
                    .Replace("+", "127.0.0.1", StringComparison.Ordinal);
                if (Uri.TryCreate(normalized, UriKind.Absolute, out var candidate)
                    && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
                {
                    baseUri = new UriBuilder(candidate) { Host = "127.0.0.1", Path = "/" }.Uri;
                    break;
                }
            }
        }

        var healthUri = new Uri(baseUri, "api/health");
        return (healthUri, baseUri);
    }

    private static bool IsContainerEnvironment()
    {
        var environment = ReadEnvironment();
        return LauncherEnvironment.IsContainerEnvironment(environment)
            || File.Exists("/.dockerenv")
            || File.Exists("/run/.containerenv");
    }

    private static Dictionary<string, string?> ReadEnvironment() =>
        Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

    private static LauncherLocale GetLauncherLocale()
    {
        var environment = ReadEnvironment();
        return LauncherMessages.ResolveLocale(
            environment.GetValueOrDefault("LC_ALL"),
            environment.GetValueOrDefault("LC_MESSAGES"),
            environment.GetValueOrDefault("LANG"),
            CultureInfo.CurrentUICulture.Name);
    }
}
