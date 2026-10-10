using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Handler.Builder;
using ServiceLib.Handler.Fmt;
using ServiceLib.Manager;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using v2rayN.WebAPI.Contracts;

namespace v2rayN.WebAPI.Services;

public sealed partial class V2rayRuntime
{
    public async Task<IReadOnlyList<ProfileGroupView>> GetProfileGroupsAsync()
    {
        var subscriptions = await AppManager.Instance.SubItems() ?? [];
        var profiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var counts = profiles.GroupBy(item => item.Subid).ToDictionary(group => group.Key, group => group.Count());
        var groups = new List<ProfileGroupView>
        {
            new(string.Empty, "all", null, ApiMessageKeys.ProfilesAllGroup, profiles.Count, string.IsNullOrEmpty(Config.SubIndexId)),
        };

        groups.AddRange(subscriptions.Select(item => new ProfileGroupView(
            item.Id,
            "subscription",
            item.Remarks,
            null,
            counts.GetValueOrDefault(item.Id),
            item.Id == Config.SubIndexId)));

        return groups;
    }

    public async Task<OperationView> SelectProfileGroupAsync(string? subscriptionId)
    {
        if (!string.IsNullOrWhiteSpace(subscriptionId)
            && await AppManager.Instance.GetSubItem(subscriptionId) is null)
        {
            return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound);
        }

        await _mutations.RunAsync(async () =>
        {
            Config.SubIndexId = subscriptionId ?? string.Empty;
            await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
        });
        _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.CommonCompleted, new { subscriptionId = Config.SubIndexId });
    }

    public async Task<OperationView> ImportProfilesAsync(ProfileImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return OperationView.Fail("profile_content_required", ApiMessageKeys.CommonInvalidInput, new { imported = 0 });
        }

        var groupId = request.SubscriptionId ?? Config.SubIndexId ?? string.Empty;
        if (!string.IsNullOrEmpty(groupId) && await AppManager.Instance.GetSubItem(groupId) is null)
        {
            return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound, new { imported = 0 });
        }

        var count = await _mutations.RunAsync(async () =>
        {
            var imported = await ConfigHandler.AddBatchServers(Config, request.Content, groupId, request.IsSubscription);
            if (imported > 0)
            {
                await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
            }
            return imported;
        });
        if (count <= 0)
        {
            return OperationView.Fail("profile_import_empty", ApiMessageKeys.ProfileInvalid, new { imported = Math.Max(count, 0) });
        }

        _events.Publish("profiles-changed", new { subscriptionId = groupId });
        AddLog("profile", $"Imported {count} profile(s).");
        return OperationView.Ok(ApiMessageKeys.ProfileImported, new { imported = count });
    }

    public async Task<OperationView> SaveProfileAsync(ProfileItem profile, string? profileId = null)
    {
        if (profile is null)
        {
            return OperationView.Fail("profile_data_required", ApiMessageKeys.CommonInvalidInput);
        }

        var isNew = string.IsNullOrWhiteSpace(profileId);
        if (!isNew)
        {
            var existing = await AppManager.Instance.GetProfileItem(profileId!);
            if (existing is null)
            {
                return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
            }
            if (!IsConfigTypeUnchanged(profile, existing))
            {
                return OperationView.Fail("profile_config_type_immutable", ApiMessageKeys.ProfileInvalid);
            }
            profile.IndexId = profileId!;
            profile.Subid = existing.Subid;
            profile.IsSub = existing.IsSub;
        }
        else
        {
            profile.IndexId = string.Empty;
            profile.Subid = Config.SubIndexId ?? string.Empty;
            profile.IsSub = false;
            if (!string.IsNullOrEmpty(profile.Subid)
                && await AppManager.Instance.GetSubItem(profile.Subid) is null)
            {
                AddLog("profile", $"Rejected profile creation for missing subscription group {profile.Subid}.");
                return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound,
                    new { subscriptionId = profile.Subid });
            }
        }

        if (profile.ConfigType.IsGroupType())
        {
            profile.CoreType ??= ECoreType.Xray;
            var protocolExtra = profile.GetProtocolExtra();
            var childIds = Utils.String2List(protocolExtra.ChildItems) ?? [];
            if (string.IsNullOrWhiteSpace(profile.Remarks)
                || (childIds.Count == 0 && string.IsNullOrWhiteSpace(protocolExtra.SubChildItems))
                || profile.CoreType is not (ECoreType.Xray or ECoreType.sing_box))
            {
                return OperationView.Fail("profile_group_invalid", ApiMessageKeys.ProfileInvalid);
            }
            if (!string.IsNullOrWhiteSpace(protocolExtra.SubChildItems)
                && await AppManager.Instance.GetSubItem(protocolExtra.SubChildItems) is null)
            {
                return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound);
            }
            var children = await AppManager.Instance.GetProfileItemsByIndexIds(childIds);
            if (children.Count != childIds.Distinct(StringComparer.Ordinal).Count()
                || await GroupProfileManager.HasCycle(profile))
            {
                return OperationView.Fail("profile_group_children_invalid", ApiMessageKeys.ProfileInvalid);
            }
        }
        else if (profile.ConfigType is EConfigType.Custom or EConfigType.Outbound)
        {
            if (string.IsNullOrWhiteSpace(profile.Remarks) || string.IsNullOrWhiteSpace(profile.Address))
            {
                return OperationView.Fail("profile_validation_failed", ApiMessageKeys.ProfileInvalid);
            }
        }
        else if (profile.ConfigType.IsComplexType())
        {
            return OperationView.Fail("profile_complex_type_requires_import", ApiMessageKeys.CommonInvalidInput);
        }
        else if (!TryValidateProfile(profile, out var code, out var messageKey))
        {
            return OperationView.Fail(code, messageKey);
        }

        var wasRunning = !isNew
            && profile.IndexId == CurrentCoreRuntime.ProfileId
            && CurrentCoreRuntime.State == CoreRuntimeState.Running;
        int result;
        try
        {
            result = await _mutations.RunAsync(() => profile.ConfigType.IsGroupType()
                ? ConfigHandler.AddServerCommon(Config, profile)
                : profile.ConfigType switch
                {
                    EConfigType.Custom when isNew => ConfigHandler.AddCustomServer(Config, profile, blDelete: false),
                    EConfigType.Outbound when isNew => ConfigHandler.AddCustomOutboundServer(Config, profile, blDelete: false, toFile: true),
                    EConfigType.Custom or EConfigType.Outbound => ConfigHandler.EditCustomServer(Config, profile),
                    _ => ConfigHandler.AddServer(Config, profile),
                });
        }
        catch (Exception exception)
        {
            AddLog("profile", $"Profile save failed for {profile.ConfigType} ({profile.IndexId}): {exception}");
            return OperationView.Fail("profile_persistence_failed", ApiMessageKeys.ProfileSaveFailed,
                new { stage = "servicelib_or_sqlite_write", exceptionType = exception.GetType().Name, detail = exception.Message });
        }
        if (result != 0)
        {
            AddLog("profile", $"ServiceLib rejected profile save for {profile.ConfigType} ({profile.IndexId}); result={result}.");
            return OperationView.Fail("profile_save_rejected", ApiMessageKeys.ProfileSaveFailed,
                new { stage = "servicelib_validation_or_persistence", result });
        }

        if (wasRunning)
        {
            var restart = await RestartCoreAsync(CancellationToken.None);
            _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
            return restart.Success
                ? OperationView.Ok(ApiMessageKeys.ProfileSaved, new { profileId = profile.IndexId, coreRestarted = true })
                : OperationView.Fail("profile_saved_core_restart_failed", ApiMessageKeys.ProfileSaved,
                    new { profileId = profile.IndexId, coreRestartRequired = true, coreResultCode = restart.Code });
        }

        _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.ProfileSaved, new { profileId = profile.IndexId, coreRestarted = false });
    }

    internal static bool TryValidateProfile(ProfileItem profile, out string code, out string messageKey)
    {
        if (string.IsNullOrEmpty(profile.Remarks)
            || string.IsNullOrEmpty(profile.Address)
            || profile.Port <= 0
            || profile.Port >= Global.MaxPort
            || (profile.ConfigType is not EConfigType.SOCKS and not EConfigType.HTTP
                && string.IsNullOrEmpty(profile.Password)))
        {
            code = "profile_validation_failed";
            messageKey = ApiMessageKeys.ProfileInvalid;
            return false;
        }

        var protocolExtra = profile.GetProtocolExtra();
        if (profile.ConfigType == EConfigType.Shadowsocks && string.IsNullOrEmpty(protocolExtra.SsMethod))
        {
            code = "profile_validation_failed";
            messageKey = ApiMessageKeys.ProfileInvalid;
            return false;
        }

        if (profile.ConfigType == EConfigType.HTTP
            && !string.IsNullOrEmpty(protocolExtra.HttpHeaders)
            && JsonUtils.ParseJson(protocolExtra.HttpHeaders) is null)
        {
            code = "profile_http_headers_invalid";
            messageKey = ApiMessageKeys.ProfileInvalid;
            return false;
        }

        code = "ok";
        messageKey = string.Empty;
        return true;
    }

    internal static bool IsConfigTypeUnchanged(ProfileItem input, ProfileItem existing) =>
        input.ConfigType == existing.ConfigType;

    public async Task<OperationView> DeleteProfilesAsync(IEnumerable<string> profileIds)
    {
        var ids = profileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return OperationView.Fail("profile_selection_required", ApiMessageKeys.ProfileSelectRequired);
        }

        var selected = await AppManager.Instance.GetProfileItemsByIndexIds(ids);
        if (selected.Count == 0)
        {
            return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
        }

        var runningProfileId = CurrentCoreRuntime.ProfileId;
        var removesCurrent = ids.Contains(runningProfileId ?? Config.IndexId, StringComparer.Ordinal);
        var wasRunning = CurrentCoreRuntime.State == CoreRuntimeState.Running;
        if (removesCurrent && wasRunning)
        {
            var stop = await StopCoreAsync(CancellationToken.None);
            if (!stop.Success)
            {
                return stop;
            }
        }

        await _mutations.RunAsync(async () =>
        {
            if (await ConfigHandler.RemoveServers(Config, selected) != 0)
            {
                throw new IOException("ServiceLib could not remove the selected profiles.");
            }
            if (removesCurrent)
            {
                _ = await ConfigHandler.GetDefaultServer(Config);
                await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
            }
        });

        if (removesCurrent && wasRunning && !string.IsNullOrEmpty(Config.IndexId))
        {
            var restart = await StartCoreAsync(null, CancellationToken.None);
            _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
            return restart.Success
                ? OperationView.Ok(ApiMessageKeys.ProfileDeleted, new { deleted = selected.Count, coreRestarted = true })
                : OperationView.Fail("profiles_deleted_core_restart_failed", ApiMessageKeys.ProfileDeleted,
                    new { deleted = selected.Count, coreRestartRequired = true, coreResultCode = restart.Code });
        }

        _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.ProfileDeleted, new { deleted = selected.Count });
    }

    public async Task<OperationView> CopyProfilesAsync(IEnumerable<string> profileIds)
    {
        var ids = profileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        var items = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(ids);
        if (items.Count == 0)
        {
            return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
        }

        if (await _mutations.RunAsync(() => ConfigHandler.CopyServer(Config, items)) != 0)
        {
            return OperationView.Fail("profile_copy_failed", ApiMessageKeys.CommonInvalidInput);
        }
        _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.ProfileCopied, new { copied = items.Count });
    }

    public async Task<OperationView> MoveProfilesToGroupAsync(MoveProfilesRequest request)
    {
        if (!string.IsNullOrEmpty(request.SubscriptionId)
            && await AppManager.Instance.GetSubItem(request.SubscriptionId) is null)
        {
            return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound);
        }

        var items = await AppManager.Instance.GetProfileItemsByIndexIds(request.ProfileIds);
        if (items.Count == 0)
        {
            return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
        }

        if (await _mutations.RunAsync(() => ConfigHandler.MoveToGroup(Config, items, request.SubscriptionId ?? string.Empty)) != 0)
        {
            return OperationView.Fail("profile_move_failed", ApiMessageKeys.CommonInvalidInput);
        }
        _events.Publish("profiles-changed", new { subscriptionId = Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.ProfileMoved, new { moved = items.Count, subscriptionId = request.SubscriptionId });
    }

    public async Task<OperationView> MoveProfileAsync(MoveProfileRequest request)
    {
        var groupId = Config.SubIndexId ?? string.Empty;
        var profiles = await GetProfilesAsync(groupId, null);
        var orderedIds = profiles.Select(item => item.IndexId).ToList();
        var index = orderedIds.IndexOf(request.ProfileId);
        if (index < 0)
        {
            return OperationView.Fail("profile_not_in_group", ApiMessageKeys.ProfileNotInGroup);
        }

        var result = await _mutations.RunAsync(() => ConfigHandler.MoveServer(Config, orderedIds, index, request.Direction, request.Position));
        _events.Publish("profiles-changed", new { subscriptionId = groupId });
        return result == 0
            ? OperationView.Ok(ApiMessageKeys.ProfileOrderSaved)
            : OperationView.Fail("profile_order_failed", ApiMessageKeys.CommonInvalidInput);
    }

    public async Task<OperationView> SortProfilesAsync(SortProfilesRequest request)
    {
        var groupId = request.SubscriptionId ?? Config.SubIndexId ?? string.Empty;
        if (!Enum.TryParse<EServerColName>(request.Column, true, out _))
        {
            return OperationView.Fail("profile_sort_column_invalid", ApiMessageKeys.CommonInvalidInput);
        }

        var result = await _mutations.RunAsync(() => ConfigHandler.SortServers(Config, groupId, request.Column, request.Ascending));
        _events.Publish("profiles-changed", new { subscriptionId = groupId });
        return result == 0
            ? OperationView.Ok(ApiMessageKeys.ProfileOrderSaved)
            : OperationView.Fail("profile_sort_failed", ApiMessageKeys.CommonInvalidInput);
    }

    public async Task<OperationView> RemoveDuplicateProfilesAsync(string? subscriptionId)
    {
        var result = await _mutations.RunAsync(() => ConfigHandler.DedupServerList(Config, subscriptionId ?? Config.SubIndexId ?? string.Empty));
        _events.Publish("profiles-changed", new { subscriptionId = subscriptionId ?? Config.SubIndexId });
        return OperationView.Ok(ApiMessageKeys.ProfileDeduplicated, new { removed = result.Item1 - result.Item2 });
    }

    public async Task<OperationView> RemoveInvalidProfilesAsync(string? subscriptionId)
    {
        var count = await _mutations.RunAsync(() => ConfigHandler.RemoveInvalidServerResult(Config, subscriptionId ?? Config.SubIndexId ?? string.Empty));
        _events.Publish("profiles-changed", new { subscriptionId = subscriptionId ?? Config.SubIndexId });
        return count < 0
            ? OperationView.Ok(ApiMessageKeys.CommonCompleted, new { removed = 0 })
            : OperationView.Ok(ApiMessageKeys.ProfileInvalidRemoved, new { removed = count });
    }

    public async Task<OperationView> GenerateProfileGroupsAsync(string? subscriptionId, bool byRegion)
    {
        SubItem? subscription = null;
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            subscription = await AppManager.Instance.GetSubItem(subscriptionId);
            if (subscription is null)
            {
                return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound);
            }
        }

        var result = await _mutations.RunAsync(() => byRegion
            ? ConfigHandler.AddGroupRegionServer(Config, subscription)
            : ConfigHandler.AddGroupAllServer(Config, subscription));
        if (!result.Success)
        {
            return OperationView.Fail("profile_group_empty", ApiMessageKeys.ProfileGrouped, result.Data);
        }

        _events.Publish("profiles-changed", new { subscriptionId = subscription?.Id ?? string.Empty });
        return OperationView.Ok(ApiMessageKeys.ProfileGrouped, result.Data);
    }

    public async Task<IReadOnlyList<ProfileExportItem>> ExportProfileDataAsync(ProfileExportRequest request)
    {
        var items = await AppManager.Instance.GetProfileItemsOrderedByIndexIds(request.ProfileIds);
        var output = new List<ProfileExportItem>();

        if (request.IncludeShareUris)
        {
            var uris = items.Select(FmtHandler.GetShareUri).Where(uri => !string.IsNullOrWhiteSpace(uri)).ToArray();
            if (uris.Length > 0)
            {
                var content = string.Join(Environment.NewLine, uris) + Environment.NewLine;
                output.Add(new(string.Empty, string.Empty, request.Base64ShareUris ? "share-uri-base64" : "share-uri",
                    request.Base64ShareUris ? Utils.Base64Encode(content) : content));
            }
        }

        if (request.IncludeInnerUri && items.Count > 0)
        {
            var innerUri = InnerFmt.ToUri(items);
            if (!string.IsNullOrWhiteSpace(innerUri))
            {
                output.Add(new(string.Empty, string.Empty, "inner-uri", innerUri));
            }
        }

        if (request.IncludeClientConfig)
        {
            foreach (var item in items)
            {
            var (context, validation) = await CoreConfigContextBuilder.Build(Config, item);
            if (validation.Success)
            {
                var generated = await CoreConfigHandler.GenerateClientConfig(context, null);
                if (generated.Success && generated.Data is string configText)
                {
                    output.Add(new(item.IndexId, item.Remarks, "client-config", configText));
                }
            }
            }
        }
        return output;
    }

    public async Task<string?> GetSubscriptionShareAsync(string id)
    {
        return (await AppManager.Instance.GetSubItem(id))?.Url;
    }

    public async Task<OperationView> StartSpeedTestAsync(SpeedTestRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var action = request.Action == ESpeedActionType.FastRealping ? ESpeedActionType.Realping : request.Action;
        var profiles = request.ProfileIds is { Length: > 0 }
            ? await AppManager.Instance.GetProfileItemsOrderedByIndexIds(request.ProfileIds)
            : await AppManager.Instance.ProfileItems(Config.SubIndexId ?? string.Empty) ?? [];
        if (profiles.Count == 0)
        {
            return OperationView.Fail("speedtest_profile_selection_required", ApiMessageKeys.SpeedTestSelectRequired);
        }

        if (action != ESpeedActionType.Tcping
            && profiles.Where(item => !item.ConfigType.IsComplexType())
                .Any(item => AppManager.Instance.GetCoreType(item, item.ConfigType) is not (ECoreType.Xray or ECoreType.sing_box)))
        {
            return OperationView.Fail("speedtest_core_unsupported", ApiMessageKeys.SpeedTestUnsupportedCore);
        }

        lock (_speedtestGate)
        {
            if (_speedtestTask is { IsCompleted: false })
            {
                return OperationView.Fail("speedtest_busy", ApiMessageKeys.SpeedTestBusy);
            }
            var speedtestService = _speedtestService ??= CreateSpeedtestService();
            var speedtestCancellation = new CancellationTokenSource();
            _speedtestCancellation = speedtestCancellation;
            _speedtestTask = Task.Run(async () =>
            {
                try
                {
                    using var requested = CancellationTokenSource.CreateLinkedTokenSource(_operations.ShutdownToken, speedtestCancellation.Token);
                    await using var operation = await _operations.EnterOperationAsync(requested.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    await speedtestService.RunLoop(action, profiles, operation.Token);
                }
                catch (OperationCanceledException) when (_operations.IsStopping)
                {
                    // Expected during graceful service shutdown.
                }
                catch (OperationCanceledException) when (speedtestCancellation.IsCancellationRequested)
                {
                    // Expected when the caller stops the active speed test.
                }
                catch (Exception ex)
                {
                    AddLog("speedtest", ex.Message);
                }
                finally
                {
                    lock (_speedtestGate)
                    {
                        if (ReferenceEquals(_speedtestCancellation, speedtestCancellation))
                        {
                            _speedtestCancellation = null;
                        }
                    }
                    speedtestCancellation.Dispose();
                }
            });
        }
        _events.Publish("speedtest-started", new
        {
            code = "speedtest_started",
            messageKey = ApiMessageKeys.SpeedTestStarted,
            action = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(action.ToString()),
            profileIds = profiles.Select(item => item.IndexId),
        });
        return OperationView.Ok(ApiMessageKeys.SpeedTestStarted, new { action = action.ToString(), profileCount = profiles.Count });
    }

    private SpeedtestService CreateSpeedtestService() => new(Config, result =>
    {
        int? delay = int.TryParse(result.Delay, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedDelay)
            ? parsedDelay
            : null;
        decimal? speed = decimal.TryParse(result.Speed, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedSpeed)
            ? parsedSpeed
            : null;
        _events.Publish("speedtest-result", new
        {
            code = ApiMessageKeys.SpeedTestResult,
            messageKey = ApiMessageKeys.SpeedTestResult,
            indexId = result.IndexId,
            delay,
            speed,
            ipInfo = result.IpInfo,
            rawResult = delay is null && speed is null ? (result.Delay ?? result.Speed) : null,
        });
        if (!string.IsNullOrEmpty(result.IndexId))
        {
            AddLog("speedtest", $"{result.IndexId}: delay={result.Delay}, speed={result.Speed}");
        }
        return Task.CompletedTask;
    });

    public OperationView StopSpeedTests()
    {
        lock (_speedtestGate)
        {
            _speedtestCancellation?.Cancel();
            _speedtestService?.ExitLoop();
        }
        return OperationView.Ok(ApiMessageKeys.CommonCompleted);
    }

    public IReadOnlyList<string> GetRunningSubscriptionUpdates()
    {
        lock (_subscriptionGate)
        {
            return _subscriptionTasks.Where(pair => !pair.Value.IsCompleted).Select(pair => pair.Key).ToArray();
        }
    }
}
