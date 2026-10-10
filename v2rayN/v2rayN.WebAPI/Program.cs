using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceLib;
using ServiceLib.Common;
using v2rayN.WebAPI.Api;
using v2rayN.WebAPI.Configuration;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Launcher;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

internal static class Program
{
    private const string ManagementKeyEnvironmentVariable = "V2RAYN_WEB_API_KEY";

    public static async Task<int> Main(string[] args)
    {
        if (args.Contains(WebLaunchOptions.HelpFlag, StringComparer.Ordinal)
            || args.Contains(WebLaunchOptions.HelpShortFlag, StringComparer.Ordinal))
        {
            Console.Out.WriteLine(LauncherMessages.Help(GetLauncherLocale()));
            return 0;
        }

        try
        {
            NativeEnvironmentFile.LoadFromExecutableDirectory(AppContext.BaseDirectory);
        }
        catch (NativeEnvironmentConfigurationException exception)
        {
            Console.Error.WriteLine($"Invalid v2rayN.WebAPI environment configuration: {exception.Message}");
            return 1;
        }

        if (args.Length == 2 && args[0] == "--apply-WebAPI-update")
        {
            return await NativeWebUpdateHelper.RunAsync(args[1]);
        }

        var environment = ReadEnvironment();
        var interactiveTerminal = !Console.IsInputRedirected;
        if (interactiveTerminal)
        {
            // Record the launch context for detached children and later helper
            // processes, which can no longer probe the terminal themselves.
            Environment.SetEnvironmentVariable(LauncherEnvironment.InteractiveLaunchEnvironmentVariable, "1");
        }

        var daemonEnvironment = LauncherEnvironment.IsDaemonEnvironment(environment);
        var interactiveLaunch = interactiveTerminal || LauncherEnvironment.IsInteractiveLaunchMarker(environment);
        var supervisedEnvironment = daemonEnvironment && !interactiveLaunch;
        var containerEnvironment = IsContainerEnvironment();
        var managementKey = environment.GetValueOrDefault(ManagementKeyEnvironmentVariable);
        if (!string.IsNullOrEmpty(managementKey)
            && (managementKey.Length < WebAuthService.MinimumKeyLength
                || managementKey.Length > WebAuthService.MaximumKeyLength))
        {
            Console.Error.WriteLine(
                $"{ManagementKeyEnvironmentVariable} length must be between {WebAuthService.MinimumKeyLength} and {WebAuthService.MaximumKeyLength} characters.");
            return 1;
        }

        if (WebDeploymentSecurityPolicy.GetStartupError(supervisedEnvironment, containerEnvironment, managementKey) is { } startupError)
        {
            Console.Error.WriteLine(startupError);
            return 1;
        }

        PrepareDataScope();
        var launchOptions = WebLaunchOptions.Parse(
            args,
            OperatingSystem.IsLinux(),
            daemonEnvironment,
            containerEnvironment,
            interactiveLaunch);

        if (launchOptions.Mode == WebLaunchMode.Stop)
        {
            if (!OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine(LauncherMessages.StopUnsupported(GetLauncherLocale()));
                return 1;
            }

            // The stop probe must follow the same configuration sources as the running
            // instance (process/.env environment and command line), so build the same
            // configuration the host would have used.
            var stopConfiguration = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = launchOptions.HostArguments,
                ContentRootPath = AppContext.BaseDirectory,
            }).Configuration;
            var (stopHealthUri, _) = GetLauncherUris(stopConfiguration);
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

        WebCorsPolicy cors;
        try
        {
            cors = new WebCorsPolicy(builder.Configuration[WebCorsPolicy.EnvironmentVariable]);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        builder.Services.AddSingleton(cors);
        builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy.PolicyName, cors.Configure));

        if (launchOptions.Mode == WebLaunchMode.BackgroundLauncher)
        {
            if (!OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine(LauncherMessages.StartFailed(GetLauncherLocale()));
                return 1;
            }

            var (healthUri, apiUri) = GetLauncherUris(builder.Configuration);
            if (healthUri is null || apiUri is null)
            {
                Console.Error.WriteLine(LauncherMessages.StartUnprobeable(GetLauncherLocale()));
                return 1;
            }

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
                apiUri,
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
                && !supervisedEnvironment
                && !containerEnvironment;
            hostResult = await RunWebHostAsync(builder, showForegroundPrompt);
        }

        // The host cleanup is complete and the old owner has disposed this lock. The
        // handoff still verifies the lock state before allowing a replacement to start.
        var exitAction = hostResult.RestoreAndRestartRequested
            ? WebLifecycleAction.RestoreAndRestart
            : WebLifecycleAction.Shutdown;
        var lifecyclePlan = WebLifecyclePlanner.Create(
            exitAction,
            OperatingSystem.IsLinux(),
            LauncherEnvironment.IsSystemdManagedDeployment(),
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

            var (restartHealthUri, _) = GetLauncherUris(builder.Configuration);
            if (restartHealthUri is null)
            {
                WriteNativeRestartDiagnostic(
                    "Restore requested a native Web restart, but the configured listeners have no loopback HTTP health endpoint to verify the replacement.");
                return 1;
            }

            var coordinator = new NativeWebRestartCoordinator(
                new HttpWebHealthProbe(),
                new LinuxReplacementProcessStarter(),
                new FileWebInstanceOwnershipProbe());
            var handoff = await coordinator.StartReplacementAfterOwnerReleaseAsync(
                GetInstanceLockPath(),
                restartHealthUri,
                command);
            WriteNativeRestartDiagnostic(handoff.Message, handoff.Success, handoff.ReplacementProcessId);
            return handoff.Success ? hostResult.ExitCode : 1;
        }
        return hostResult.ExitCode;
    }

    private static async Task<WebHostRunResult> RunWebHostAsync(WebApplicationBuilder builder, bool showForegroundPrompt)
    {
        // "guiConfigs/web-auth.json" is the pre-rename legacy location; WebAuthStorage
        // migrates it (and the previous private location) to WebAPIData/WebAPI-auth.json.
        var configPath = WebAuthStorage.MigrateAndGetPath(
            Utils.StartupPath(),
            Utils.GetConfigPath("web-auth.json"));
        var webAuth = new WebAuthService(configPath, Environment.GetEnvironmentVariable(ManagementKeyEnvironmentVariable));
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
        app.UseRouting();
        app.UseMiddleware<WebOriginGuardMiddleware>();
        app.UseCors(WebCorsPolicy.PolicyName); // OPTIONS must complete before session auth.
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

        if (showForegroundPrompt)
        {
            var (_, apiUri) = GetLauncherUris(builder.Configuration);
            if (apiUri is not null)
            {
                var displayUri = apiUri.ToString();
                app.Lifetime.ApplicationStarted.Register(() =>
                    Console.Out.WriteLine(LauncherMessages.ForegroundStarted(displayUri, GetLauncherLocale())));
            }
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
        Path.Combine(Utils.StartupPath(), "v2rayN.WebAPI.instance.lock");

    internal static (Uri? HealthUri, Uri? ApiUri) GetLauncherUris(IConfiguration configuration)
    {
        if (WebProbeUriResolver.TryResolve(configuration, out var endpoints))
        {
            return (endpoints.HealthUri, endpoints.ApiUri);
        }

        // Never guess a loopback address or port: a probe that does not match the
        // configured listeners would verify (or stop) the wrong process.
        return (null, null);
    }

    private static bool IsContainerEnvironment()
    {
        var environment = ReadEnvironment();
        return LauncherEnvironment.IsContainerEnvironment(environment)
            || File.Exists("/.dockerenv")
            || File.Exists("/run/.containerenv");
    }

    private static Dictionary<string, string?> ReadEnvironment() =>
        LauncherEnvironment.ReadCurrentEnvironment();

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
