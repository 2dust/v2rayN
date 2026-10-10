using ServiceLib.Enums;
using ServiceLib.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Security;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Api;

public static class WebApiEndpoints
{
    private static readonly System.Text.Json.JsonSerializerOptions SseJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    internal static string SerializeEventData(object? data) => data is null
        ? "null"
        : System.Text.Json.JsonSerializer.Serialize(data, data.GetType(), SseJsonOptions);

    public static void MapWebApi(this WebApplication app)
    {
        app.MapGet("/api/health", (HttpContext context, V2rayRuntime runtime) =>
            Results.Ok(WebHealthEndpoint.CreateResponse(context, runtime)));
        app.MapGet("/api/status", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetStatusAsync(), "status.loaded"));
        app.MapGet("/api/operations", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetRunningOperationsAsync(), "operations.loaded"));
        app.MapGet("/api/logs", (int? limit, string? filter, V2rayRuntime runtime) =>
            ApiReplies.Ok(runtime.GetRecentLogs(limit ?? 200, filter), "logs.loaded"));
        app.MapGet("/api/logs/page", (int? page, int? pageSize, string? filter, V2rayRuntime runtime) =>
            ApiReplies.Ok(runtime.GetRecentLogsPage(page ?? 1, pageSize ?? 100, filter), "logs.loaded"));
        app.MapDelete("/api/logs", (V2rayRuntime runtime) =>
        {
            var generation = runtime.ClearLogs();
            return ApiReplies.Ok(new { cleared = true, generation }, "logs.cleared");
        });

        MapSubscriptions(app);
        MapProfiles(app);
        MapSettings(app);
        app.MapGet("/api/editor-options", () => ApiReplies.Ok(V2rayRuntime.GetEditorOptions(), ApiMessageKeys.CommonLoaded));
        MapCore(app);
        MapEvents(app);
    }

    private static void MapSubscriptions(WebApplication app)
    {
        app.MapGet("/api/subscriptions", async (V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.GetSubscriptionsAsync(), "subscriptions.loaded"));
        app.MapPost("/api/subscriptions", async (SubscriptionInput input, V2rayRuntime runtime) =>
        {
            var result = await runtime.AddSubscriptionAsync(input);
            return result.Success
                ? Results.Created($"/api/subscriptions/{result.Data!.Id}", result)
                : Results.BadRequest(result);
        });
        app.MapPut("/api/subscriptions/{id}", async (string id, SubscriptionInput input, V2rayRuntime runtime) =>
        {
            var result = await runtime.UpdateSubscriptionAsync(id, input);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });
        app.MapDelete("/api/subscriptions/{id}", async (string id, V2rayRuntime runtime) =>
        {
            var result = await runtime.DeleteSubscriptionAsync(id);
            return ApiReplies.Operation(result, failureStatus: StatusCodes.Status404NotFound);
        });
        app.MapGet("/api/subscriptions/{id}/share", async (string id, V2rayRuntime runtime) =>
        {
            var url = await runtime.GetSubscriptionShareAsync(id);
            return url is null
                ? ApiReplies.NotFound("subscription_not_found", ApiMessageKeys.SubscriptionNotFound)
                : ApiReplies.Ok(new { id, url }, "subscriptions.shareLoaded");
        });
        app.MapPost("/api/subscriptions/update", (SubscriptionUpdateRequest request, V2rayRuntime runtime) =>
            runtime.StartSubscriptionUpdate(request.SubscriptionId ?? string.Empty, request.UseProxy)
                ? Results.Accepted("/api/operations", OperationView.Ok(ApiMessageKeys.SubscriptionUpdateStarted,
                    new { subscriptionId = request.SubscriptionId, useProxy = request.UseProxy }))
                : ApiReplies.Operation(OperationView.Fail("subscription_update_busy", ApiMessageKeys.SubscriptionUpdateBusy), failureStatus: StatusCodes.Status409Conflict));
        app.MapPost("/api/subscriptions/{id}/update", (string id, bool? useProxy, V2rayRuntime runtime) =>
            runtime.StartSubscriptionUpdate(id, useProxy ?? false)
                ? Results.Accepted($"/api/subscriptions/{id}", OperationView.Ok(ApiMessageKeys.SubscriptionUpdateStarted,
                    new { subscriptionId = id, useProxy = useProxy ?? false }))
                : ApiReplies.Operation(OperationView.Fail("subscription_update_busy", ApiMessageKeys.SubscriptionUpdateBusy), failureStatus: StatusCodes.Status409Conflict));
    }

    private static void MapProfiles(WebApplication app)
    {
        app.MapGet("/api/profile-groups", async (V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.GetProfileGroupsAsync(), "profiles.groupsLoaded"));
        app.MapPut("/api/profile-groups/current", async (ProfileGroupSelectionRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SelectProfileGroupAsync(request.SubscriptionId), failureStatus: StatusCodes.Status404NotFound));
        app.MapPost("/api/profile-groups/generate/all", async (string? subscriptionId, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.GenerateProfileGroupsAsync(subscriptionId, false)));
        app.MapPost("/api/profile-groups/generate/regions", async (string? subscriptionId, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.GenerateProfileGroupsAsync(subscriptionId, true)));

        app.MapGet("/api/profiles", async (string? subscriptionId, string? filter, V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.GetProfilesAsync(subscriptionId, filter), "profiles.loaded"));
        app.MapGet("/api/profiles/{id}", async (string id, V2rayRuntime runtime) =>
        {
            var profile = await runtime.GetProfileDetailsAsync(id);
            return profile is null
                ? ApiReplies.NotFound("profile_not_found", ApiMessageKeys.ProfileNotFound)
                : ApiReplies.Ok(profile, ApiMessageKeys.ProfileDetailLoaded);
        });
        app.MapPost("/api/profiles/import", async (ProfileImportRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.ImportProfilesAsync(request)));
        app.MapPost("/api/profiles", async (ProfileItem profile, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveProfileAsync(profile)));
        app.MapPut("/api/profiles/{id}", async (string id, ProfileItem profile, V2rayRuntime runtime) =>
        {
            var result = await runtime.SaveProfileAsync(profile, id);
            return ApiReplies.Operation(result,
                failureStatus: result.Code == "profile_not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        });
        app.MapDelete("/api/profiles", async ([FromBody] ProfileIdsRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.DeleteProfilesAsync(request.ProfileIds)));
        app.MapPost("/api/profiles/copy", async (ProfileIdsRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.CopyProfilesAsync(request.ProfileIds)));
        app.MapPost("/api/profiles/move-to-group", async (MoveProfilesRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.MoveProfilesToGroupAsync(request)));
        app.MapPost("/api/profiles/move", async (MoveProfileRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.MoveProfileAsync(request)));
        app.MapPost("/api/profiles/sort", async (SortProfilesRequest request, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SortProfilesAsync(request)));
        app.MapPost("/api/profiles/deduplicate", async (string? subscriptionId, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.RemoveDuplicateProfilesAsync(subscriptionId)));
        app.MapDelete("/api/profiles/invalid-test-results", async (string? subscriptionId, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.RemoveInvalidProfilesAsync(subscriptionId)));
        app.MapPost("/api/profiles/export", async (ProfileExportRequest request, V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.ExportProfileDataAsync(request), "profiles.exportReady"));
        app.MapPost("/api/profiles/{id}/select", async (string id, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.SelectProfileAsync(id, cancellationToken), failureStatus: StatusCodes.Status409Conflict));
        app.MapPost("/api/profiles/{id}/latency", async (string id, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.StartLatencyTestAsync(id), successStatus: StatusCodes.Status202Accepted));
        app.MapPost("/api/speedtests", async (SpeedTestRequest request, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.StartSpeedTestAsync(request, cancellationToken), successStatus: StatusCodes.Status202Accepted));
        app.MapDelete("/api/speedtests", (V2rayRuntime runtime) => ApiReplies.Operation(runtime.StopSpeedTests()));
        app.MapPost("/api/latency/stop", (V2rayRuntime runtime) => ApiReplies.Operation(runtime.StopLatencyTests()));
    }

    private static void MapSettings(WebApplication app)
    {
        app.MapGet("/api/settings", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetSettingsAsync(), "settings.loaded"));
        app.MapPut("/api/settings/apply", async (SettingsApplyInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.ApplySettingsAsync(input)));
        app.MapPut("/api/settings/inbound", async (InboundSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateInboundSettingsAsync(input)));
        app.MapPut("/api/settings/core", async (CoreSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateCoreSettingsAsync(input)));
        app.MapPut("/api/settings/application", async (AppSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateAppSettingsAsync(input)));
        app.MapPut("/api/settings/speedtest", async (SpeedTestSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateSpeedTestSettingsAsync(input)));
        app.MapPut("/api/settings/core-types", async (CoreTypeMappingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateCoreTypeMappingsAsync(input.Mappings)));
        app.MapPut("/api/settings/routing", async (RoutingSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateRoutingStrategiesAsync(input)));
        app.MapGet("/api/settings/webdav", (V2rayRuntime runtime) => ApiReplies.Ok(runtime.GetWebDavSettings(), "backup.webdavSettingsLoaded"));
        app.MapPut("/api/settings/webdav", async (WebDavSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateWebDavSettingsAsync(input)));

        app.MapGet("/api/settings/dns/simple", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetSimpleDNSAsync(), "dns.simpleLoaded"));
        app.MapPut("/api/settings/dns/simple", async (ServiceLib.Models.Configs.SimpleDNSItem input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateSimpleDNSAsync(input)));
        app.MapGet("/api/settings/dns/profiles", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetDnsProfilesAsync(), "dns.profilesLoaded"));
        app.MapPut("/api/settings/dns/profiles/{coreType}", async (ECoreType coreType, DnsProfileInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.UpdateDnsProfileAsync(coreType, input), failureStatus: StatusCodes.Status404NotFound));
        app.MapGet("/api/settings/dns/editor-options", () => ApiReplies.Ok(V2rayRuntime.GetDnsEditorOptions(), ApiMessageKeys.CommonLoaded));
        app.MapGet("/api/settings/dns/defaults", (V2rayRuntime runtime) => ApiReplies.Ok(runtime.GetDefaultDnsProfiles(), ApiMessageKeys.CommonLoaded));

        app.MapGet("/api/settings/routing-profiles", async (V2rayRuntime runtime) => ApiReplies.Ok(await runtime.GetRoutingProfilesAsync(), "routing.profilesLoaded"));
        app.MapPost("/api/settings/routing-profiles", async (RoutingItem input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveRoutingProfileAsync(input)));
        app.MapPut("/api/settings/routing-profiles/{id}", async (string id, RoutingItem input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveRoutingProfileAsync(input, id), failureStatus: StatusCodes.Status404NotFound));
        app.MapDelete("/api/settings/routing-profiles/{id}", async (string id, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.DeleteRoutingProfileAsync(id), failureStatus: StatusCodes.Status404NotFound));
        app.MapPost("/api/settings/routing-profiles/{id}/activate", async (string id, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.ActivateRoutingProfileAsync(id), failureStatus: StatusCodes.Status404NotFound));
        app.MapGet("/api/settings/routing-profiles/{id}/rules", async (string id, V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.GetRoutingRulesAsync(id), "routing.rulesLoaded"));
        app.MapPut("/api/settings/routing-profiles/{id}/rules", async (string id, List<RulesItem> rules, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveRoutingRulesAsync(id, rules), failureStatus: StatusCodes.Status404NotFound));
        app.MapPost("/api/settings/routing-profiles/{id}/rules/import", async (string id, RouteRulesImportInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.ImportRoutingRulesAsync(id, input)));
        app.MapPost("/api/settings/routing-profiles/{id}/rules/import-url", async (string id, RouteRulesUrlImportInput input, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.ImportRoutingRulesFromUrlAsync(id, input.Append, cancellationToken)));
        app.MapDelete("/api/settings/routing-profiles/{id}/rules/{ruleId}", async (string id, string ruleId, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.DeleteRoutingRuleAsync(id, ruleId), failureStatus: StatusCodes.Status404NotFound));
        app.MapPost("/api/settings/routing-profiles/{id}/rules/move", async (string id, RouteRulesMoveRequest input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.MoveRoutingRuleAsync(id, input.RuleId, input.Direction, input.Position)));
        app.MapPost("/api/settings/routing-profiles/import", async (V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.ImportRoutingProfilesAsync()));

        app.MapGet("/api/settings/core-templates", async (V2rayRuntime runtime) =>
            ApiReplies.Ok(await runtime.GetFullConfigTemplatesAsync(), "coreTemplates.loaded"));
        app.MapPut("/api/settings/core-templates/{coreType}", async (ECoreType coreType, CoreConfigTemplateInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveFullConfigTemplateAsync(coreType, input), failureStatus: StatusCodes.Status404NotFound));
        app.MapPost("/api/settings/regional-presets/{preset}", async (EPresetType preset, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.ApplyRegionalPresetAsync(preset, cancellationToken)));
        app.MapDelete("/api/statistics", async (V2rayRuntime runtime) => ApiReplies.Operation(await runtime.ClearStatisticsAsync()));

        app.MapPost("/api/backup/webdav/check", async (V2rayRuntime runtime) => ApiReplies.Operation(await runtime.CheckWebDavAsync()));
        app.MapPost("/api/backup/webdav", async (V2rayRuntime runtime) => ApiReplies.Operation(await runtime.BackupToWebDavAsync()));
        app.MapPost("/api/backup/webdav/restore", async (V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.RestoreFromWebDavAsync(cancellationToken), successStatus: StatusCodes.Status202Accepted));
        app.MapGet("/api/backup/download", async (V2rayRuntime runtime) =>
        {
            var (result, filePath) = await runtime.CreateBackupArchiveAsync();
            return result.Success && filePath is not null
                ? (IResult)new DeleteAfterFileResult(filePath)
                : ApiReplies.Operation(result);
        });
        app.MapPost("/api/backup/restore", async (IFormFile file, V2rayRuntime runtime, CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return ApiReplies.Operation(OperationView.Fail("backup_file_empty", ApiMessageKeys.BackupArchiveInvalid));
            }
            if (file.Length > 64L * 1024 * 1024)
            {
                return ApiReplies.Operation(OperationView.Fail("backup_archive_too_large", ApiMessageKeys.BackupArchiveInvalid));
            }
            await using var input = file.OpenReadStream();
            return ApiReplies.Operation(await runtime.RestoreFromUploadAsync(input, cancellationToken), successStatus: StatusCodes.Status202Accepted);
        }).DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(65L * 1024 * 1024))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 65L * 1024 * 1024 });
    }

    private static void MapCore(WebApplication app)
    {
        app.MapGet("/api/core-updates", (V2rayRuntime runtime) =>
            ApiReplies.Ok(runtime.GetCoreUpdateSettings(), "maintenance.updatesLoaded"));
        app.MapGet("/api/web-updates", (V2rayRuntime runtime) =>
            ApiReplies.Ok(runtime.GetWebUpdateTarget(), "maintenance.updatesLoaded"));
        app.MapGet("/api/web-updates/check", async (bool? preRelease, bool? useProxy, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            Results.Ok(await runtime.CheckWebUpdateAsync(preRelease, useProxy, cancellationToken)));
        app.MapPost("/api/web-updates/update", (bool? preRelease, bool? useProxy, V2rayRuntime runtime) =>
            ApiReplies.Operation(runtime.StartWebUpdate(preRelease, useProxy),
                successStatus: StatusCodes.Status202Accepted,
                failureStatus: StatusCodes.Status409Conflict));
        app.MapGet("/api/core-updates/progress", (V2rayRuntime runtime) =>
            ApiReplies.Ok(runtime.GetCoreUpdateProgress(), "maintenance.updatesLoaded"));
        app.MapPut("/api/core-updates/settings", async (CoreUpdateSettingsInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(await runtime.SaveCoreUpdateSettingsAsync(input)));
        app.MapPost("/api/core-updates/batch", (CoreUpdateBatchInput input, V2rayRuntime runtime) =>
            ApiReplies.Operation(runtime.StartSelectedCoreUpdateBatch(input.Apply),
                successStatus: StatusCodes.Status202Accepted,
                failureStatus: StatusCodes.Status409Conflict));
        app.MapGet("/api/core-updates/{coreType}/check", async (ECoreType coreType, bool? preRelease, bool? useProxy, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            Results.Ok(await runtime.CheckCoreUpdateAsync(coreType, preRelease, useProxy, cancellationToken)));
        app.MapPost("/api/core-updates/{coreType}/update", (ECoreType coreType, bool? preRelease, bool? useProxy, V2rayRuntime runtime) =>
            ApiReplies.Operation(
                runtime.StartCoreUpdate(coreType, preRelease, useProxy),
                successStatus: StatusCodes.Status202Accepted,
                failureStatus: StatusCodes.Status409Conflict));
        app.MapGet("/api/core/xray/check-update", async (bool? preRelease, bool? useProxy, V2rayRuntime runtime, CancellationToken cancellationToken) =>
            Results.Ok(await runtime.CheckXrayUpdateAsync(preRelease ?? false, useProxy ?? true, cancellationToken)));
        app.MapPost("/api/core/xray/update", (bool? preRelease, bool? useProxy, V2rayRuntime runtime) =>
            ApiReplies.Operation(
                runtime.StartCoreUpdate(ECoreType.Xray, preRelease ?? false, useProxy ?? true),
                successStatus: StatusCodes.Status202Accepted,
                failureStatus: StatusCodes.Status409Conflict));
        app.MapPost("/api/core/geo/update", (bool? useProxy, V2rayRuntime runtime) =>
            ApiReplies.Operation(
                runtime.StartGeoUpdate(useProxy),
                successStatus: StatusCodes.Status202Accepted,
                failureStatus: StatusCodes.Status409Conflict));

        app.MapPost("/api/core/start", async (V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.StartCoreAsync(null, cancellationToken), failureStatus: StatusCodes.Status409Conflict));
        app.MapPost("/api/core/stop", async (V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.StopCoreAsync(cancellationToken)));
        app.MapPost("/api/core/restart", async (V2rayRuntime runtime, CancellationToken cancellationToken) =>
            ApiReplies.Operation(await runtime.RestartCoreAsync(cancellationToken), failureStatus: StatusCodes.Status409Conflict));
    }

    private static void MapEvents(WebApplication app)
    {
        app.MapGet("/api/events", async (HttpContext context, EventHub events, WebSessionService sessions, IHostApplicationLifetime lifetime) =>
        {
            var session = context.Items.TryGetValue(WebSessionService.SessionSnapshotContextKey, out var sessionValue)
                ? sessionValue as WebSessionSnapshot
                : null;
            if (session is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var revoked = session.RevocationToken;
            var applicationStopping = lifetime.ApplicationStopping;
            // The stream must end as soon as the host starts stopping: Kestrel's
            // graceful stop waits for open requests, and an attached client would
            // otherwise pin the whole host shutdown budget.
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                revoked,
                applicationStopping);
            var cancellationToken = linkedCancellation.Token;

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            using var heartbeat = new PeriodicTimer(TimeSpan.FromMinutes(1));
            var includeLogs = string.Equals(context.Request.Query["include_logs"], "true", StringComparison.OrdinalIgnoreCase);
            await using var subscription = events.Subscribe(cancellationToken, includeLogs).GetAsyncEnumerator(cancellationToken);
            var nextEvent = subscription.MoveNextAsync().AsTask();
            var nextHeartbeat = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var completed = await Task.WhenAny(nextEvent, nextHeartbeat);
                    if (completed == nextHeartbeat)
                    {
                        if (!await nextHeartbeat)
                        {
                            break;
                        }

                        if (!await TryWriteSseHeartbeatAsync(context, sessions, session, cancellationToken))
                        {
                            break;
                        }
                        nextHeartbeat = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
                        continue;
                    }

                    if (!await nextEvent)
                    {
                        break;
                    }

                    var message = subscription.Current;
                    var payload = SerializeEventData(message.Data);
                    await context.Response.WriteAsync($"event: {message.Type}\ndata: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                    nextEvent = subscription.MoveNextAsync().AsTask();
                }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested || revoked.IsCancellationRequested)
            {
                await WriteUnauthorizedSseResponseAsync(context);
            }
            catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested)
            {
                // The host is shutting down; end the stream quietly so the web
                // server can finish its graceful stop instead of waiting for it.
            }
            finally
            {
                // Task.WhenAny can leave either asynchronous wait pending when the stream is revoked.
                // Cancel and drain both before disposing the async iterator and timer.
                linkedCancellation.Cancel();
                try
                {
                    await nextEvent;
                }
                catch (OperationCanceledException)
                {
                }

                try
                {
                    await nextHeartbeat;
                }
                catch (OperationCanceledException)
                {
                }
            }
        });
    }

    internal static async Task<bool> TryWriteSseHeartbeatAsync(
        HttpContext context,
        WebSessionService sessions,
        WebSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        if (!sessions.TryValidateSseSessionWithoutRenewal(session.SessionDigest, out _))
        {
            await WriteUnauthorizedSseResponseAsync(context);
            return false;
        }

        await context.Response.WriteAsync(": keep-alive\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
        return true;
    }

    private static async Task WriteUnauthorizedSseResponseAsync(HttpContext context)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            ApiEnvelope<object>.Fail("unauthorized", ApiMessageKeys.CommonUnauthorized),
            context.RequestAborted);
    }
}
