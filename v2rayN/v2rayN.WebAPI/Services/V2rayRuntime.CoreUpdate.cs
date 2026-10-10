using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Resx;
using ServiceLib.Services;
using v2rayN.WebAPI.Contracts;

namespace v2rayN.WebAPI.Services;

public sealed partial class V2rayRuntime
{
    private const string GeoFilesUpdateTarget = "GeoFiles";
    private const string WebUpdateTarget = "v2rayN.WebAPI";
    internal static string NormalizeWebUpdateTarget(string target) => target == "v2rayN.Web" ? WebUpdateTarget : target;

    internal static List<string>? NormalizeWebUpdateTargets(IEnumerable<string>? targets) =>
        targets?.Select(NormalizeWebUpdateTarget).Distinct(StringComparer.Ordinal).ToList();
    private readonly object _updateTaskGate = new();
    private readonly GeoFilesUpdateGate _geoUpdateGate = new();
    private readonly ConcurrentDictionary<ECoreType, Task> _coreUpdateTasks = new();
    private readonly ConcurrentDictionary<string, CoreUpdateProgressView> _updateProgress = new(StringComparer.Ordinal);
    private Task? _geoUpdateTask;
    private Task? _coreUpdateBatchTask;

    internal bool GetUpdatePreRelease(string target, bool? preRelease = null) =>
        preRelease ?? (Config.CheckUpdateItem.CheckPreReleaseCoreTypes?.Contains(target, StringComparer.Ordinal) ?? false);

    public CoreUpdateSettingsView GetCoreUpdateSettings()
    {
        var manager = CoreInfoManager.Instance;
        var selectedTypes = Config.CheckUpdateItem.SelectedCoreTypes;
        var targets = GetAvailableWebCoreUpdateTypes()
            .Select(coreType =>
            {
                var isSupported = manager.IsCheckUpdateSupported(coreType)
                    && manager.GetCoreInfo(coreType) is { } info
                    && IsCurrentPlatformDownloadSupported(info);
                var canInstall = isSupported
                    && coreType != ECoreType.v2rayN
                    && CoreUpdatePackageStager.SupportsCore(coreType);
                var unsupportedReason = coreType == ECoreType.v2rayN
                    ? "maintenance.webUpdateUnsupported"
                    : canInstall ? null : "maintenance.updateUnsupported";
                var storageName = coreType.ToString();
                return new CoreUpdateTargetView(
                    storageName,
                    GetCoreUpdateNameKey(coreType),
                    isSupported,
                    canInstall,
                    manager.GetCheckPreRelease(coreType, preRelease: true),
                    selectedTypes?.Contains(storageName, StringComparer.Ordinal) ?? true,
                    unsupportedReason);
            })
            .ToArray();

        var preReleaseNames = GetAvailablePreReleaseUpdateTargets();
        return new CoreUpdateSettingsView(
            targets,
            selectedTypes?.Contains(GeoFilesUpdateTarget, StringComparer.Ordinal) ?? true,
            GetUpdatePreRelease(WebUpdateTarget),
            Config.CheckUpdateItem.UpdateViaProxy,
            Config.CheckUpdateItem.CheckPreReleaseCoreTypes?
                .Where(preReleaseNames.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? []);
    }

    public IReadOnlyList<CoreUpdateProgressView> GetCoreUpdateProgress() =>
        _updateProgress.Values
            .Append(GetWebUpdateProgress())
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.CoreType, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(item => item.CoreType, StringComparer.Ordinal)
            .ToArray();

    public async Task<OperationView> SaveCoreUpdateSettingsAsync(CoreUpdateSettingsInput input)
    {
        var supportedNames = GetAvailableWebCoreUpdateTypes()
            .Select(type => type.ToString())
            .Append(WebUpdateTarget)
            .ToHashSet(StringComparer.Ordinal);
        var requestedNames = (input.SelectedCoreTypes ?? []).Select(NormalizeWebUpdateTarget)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var preReleaseNames = GetAvailablePreReleaseUpdateTargets();
        // Older clients still submit a single switch. An explicit per-target list takes
        // precedence, including an empty list, and is independent of update selections.
        var requestedPreReleaseNames = NormalizeWebUpdateTargets(input.CheckPreReleaseCoreTypes)?.ToArray()
            ?? (input.PreRelease ? preReleaseNames.ToArray() : []);
        if (requestedNames.Any(name => name != GeoFilesUpdateTarget && !supportedNames.Contains(name))
            || requestedPreReleaseNames.Any(name => !preReleaseNames.Contains(name)))
        {
            return OperationView.Fail("core_update_settings_invalid", ApiMessageKeys.CommonInvalidInput);
        }

        await _mutations.RunAsync(async () =>
        {
            var previous = NormalizeWebUpdateTargets(Config.CheckUpdateItem.SelectedCoreTypes) ?? [];
            // Keep selections from backups that this platform/Web updater cannot display,
            // just as the desktop updater keeps hidden Core configuration untouched.
            var hiddenSelections = previous.Where(name => name != GeoFilesUpdateTarget && !supportedNames.Contains(name));
            Config.CheckUpdateItem.SelectedCoreTypes = hiddenSelections
                .Concat(requestedNames)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var hiddenPreReleaseSelections = (NormalizeWebUpdateTargets(Config.CheckUpdateItem.CheckPreReleaseCoreTypes) ?? [])
                .Where(name => !preReleaseNames.Contains(name));
            Config.CheckUpdateItem.CheckPreReleaseCoreTypes = hiddenPreReleaseSelections
                .Concat(requestedPreReleaseNames)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Config.CheckUpdateItem.UpdateViaProxy = input.UseProxy;
            await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
        });

        return OperationView.Ok(ApiMessageKeys.CoreUpdateSettingsSaved);
    }

    public async Task<ApiEnvelope<CoreUpdateCheckView>> CheckCoreUpdateAsync(
        ECoreType coreType,
        bool? preRelease,
        bool? useProxy,
        CancellationToken cancellationToken)
    {
        if (!CanCheckCoreUpdate(coreType))
        {
            return ApiEnvelope<CoreUpdateCheckView>.Fail(
                "core_update_unsupported",
                ApiMessageKeys.CoreUpdateUnsupported);
        }

        var checkedUpdate = await CheckCoreUpdateResultAsync(coreType, preRelease, useProxy, cancellationToken);
        var result = checkedUpdate.Result;
        if (result.Success && result.Version is not null)
        {
            return ApiEnvelope<CoreUpdateCheckView>.Ok(
                new CoreUpdateCheckView(true, result.Version.ToString()),
                ApiMessageKeys.CoreUpdateAvailable);
        }
        if (checkedUpdate.IsUpToDate)
        {
            return ApiEnvelope<CoreUpdateCheckView>.Ok(
                new CoreUpdateCheckView(false, result.Version?.ToString(), IsUpToDate: true, Detail: result.Msg),
                ApiMessageKeys.CoreUpdateCurrent);
        }

        AddLog("update", $"{coreType} update check failed: {result.Msg}");
        return ApiEnvelope<CoreUpdateCheckView>.Fail(
            "core_update_check_failed",
            ApiMessageKeys.CoreUpdateCheckFailed,
            new CoreUpdateCheckView(false, null, Detail: result.Msg));
    }

    public Task<ApiEnvelope<CoreUpdateCheckView>> CheckXrayUpdateAsync(
        bool preRelease,
        bool useProxy,
        CancellationToken cancellationToken) =>
        CheckCoreUpdateAsync(ECoreType.Xray, preRelease, useProxy, cancellationToken);

    public OperationView StartCoreUpdate(ECoreType coreType, bool? preRelease = null, bool? useProxy = null)
    {
        if (!CanInstallCoreUpdate(coreType))
        {
            return OperationView.Fail("core_update_unsupported", ApiMessageKeys.CoreUpdateUnsupported);
        }

        lock (_updateTaskGate)
        {
            if (IsUpdateRunningLocked())
            {
                return OperationView.Fail("core_update_busy", ApiMessageKeys.CoreUpdateBusy);
            }

            var selected = Config.CheckUpdateItem.SelectedCoreTypes;
            if (selected is not null && !selected.Contains(coreType.ToString(), StringComparer.Ordinal))
            {
                return OperationView.Fail("core_update_not_selected", ApiMessageKeys.CoreUpdateUnsupported);
            }

            var task = Task.Run(() => RunCoreUpdateAsync(
                coreType,
                GetUpdatePreRelease(coreType.ToString(), preRelease),
                useProxy ?? Config.CheckUpdateItem.UpdateViaProxy));
            _coreUpdateTasks[coreType] = task;
        }

        return OperationView.Ok(ApiMessageKeys.CoreUpdateStarted, new { coreType = coreType.ToString() });
    }

    public bool StartXrayUpdate(bool preRelease, bool useProxy) =>
        StartCoreUpdate(ECoreType.Xray, preRelease, useProxy).Success;

    public OperationView StartSelectedCoreUpdateBatch(bool apply)
    {
        var selected = Config.CheckUpdateItem.SelectedCoreTypes;
        var webSupported = WebUpdatePackageStager.IsSupportedRid(WebBuildIdentity.Current.Rid);
        var selectedNames = selected is null
            ? GetAvailableWebCoreUpdateTypes().Select(coreType => coreType.ToString())
                .Append(GeoFilesUpdateTarget)
                .Concat(webSupported ? [WebUpdateTarget] : [])
                .ToArray()
            : selected.ToArray();
        var targets = GetAvailableWebCoreUpdateTypes()
            .Where(coreType => selectedNames.Contains(coreType.ToString(), StringComparer.Ordinal))
            .ToArray();
        var includeGeoFiles = selectedNames.Contains(GeoFilesUpdateTarget, StringComparer.Ordinal);
        var includeWeb = webSupported && selectedNames.Contains(WebUpdateTarget, StringComparer.Ordinal);
        if (targets.Length == 0 && !includeGeoFiles && !includeWeb)
        {
            return OperationView.Fail("core_update_batch_empty", ApiMessageKeys.CommonInvalidInput);
        }

        lock (_updateTaskGate)
        {
            if (IsUpdateRunningLocked())
            {
                return OperationView.Fail("core_update_busy", ApiMessageKeys.CoreUpdateBusy);
            }

            _coreUpdateBatchTask = Task.Run(() => RunSelectedCoreUpdateBatchAsync(
                targets,
                includeGeoFiles,
                includeWeb,
                apply,
                Config.CheckUpdateItem.UpdateViaProxy));
        }

        var targetNames = targets.Select(coreType => coreType.ToString()).ToList();
        if (includeGeoFiles) targetNames.Add(GeoFilesUpdateTarget);
        if (includeWeb) targetNames.Add(WebUpdateTarget);
        return OperationView.Ok(ApiMessageKeys.CoreUpdateBatchStarted, new { apply, targets = targetNames });
    }

    public OperationView StartGeoUpdate(bool? useProxy = null)
    {
        var selected = Config.CheckUpdateItem.SelectedCoreTypes;
        if (selected is not null && !selected.Contains(GeoFilesUpdateTarget, StringComparer.Ordinal))
        {
            return OperationView.Fail("geo_update_not_selected", ApiMessageKeys.GeoUpdateNotSelected);
        }

        lock (_updateTaskGate)
        {
            if (IsUpdateRunningLocked())
            {
                return OperationView.Fail("geo_update_busy", ApiMessageKeys.GeoUpdateBusy);
            }

            var proxy = useProxy ?? Config.CheckUpdateItem.UpdateViaProxy;
            _geoUpdateTask = Task.Run(() => RunGeoUpdateAsync(proxy));
        }
        return OperationView.Ok(ApiMessageKeys.GeoUpdateStarted);
    }

    internal IReadOnlyList<string> GetRunningCoreUpdateOperations()
    {
        lock (_updateTaskGate)
        {
            var operations = _coreUpdateTasks
                .Where(pair => !pair.Value.IsCompleted)
                .Select(pair => GetCoreUpdateOperationName(pair.Key))
                .ToList();
            if (_geoUpdateTask is { IsCompleted: false })
            {
                operations.Add("geo-update");
            }
            if (_webUpdateTask is { IsCompleted: false })
            {
                operations.Add("web-update");
            }
            if (_coreUpdateBatchTask is { IsCompleted: false })
            {
                operations.Add("core-update-batch");
            }
            return operations;
        }
    }

    private bool IsUpdateRunningLocked() =>
        _coreUpdateTasks.Values.Any(task => !task.IsCompleted)
        || _geoUpdateTask is { IsCompleted: false }
        || _webUpdateTask is { IsCompleted: false }
        || _coreUpdateBatchTask is { IsCompleted: false };

    private async Task RunSelectedCoreUpdateBatchAsync(
        IReadOnlyList<ECoreType> coreTypes,
        bool includeGeoFiles,
        bool includeWeb,
        bool apply,
        bool useProxy)
    {
        var staged = new List<CoreUpdateStage>();
        var overallSuccess = true;
        var currentTarget = string.Empty;
        WebUpdateStage? webStage = null;
        try
        {
            // Holding the runtime's exclusive mutation lease prevents a restore or profile
            // mutation from invalidating the checked/staged targets. Read-only status and log
            // requests remain available. No Core process is stopped during this stage.
            await using var maintenance = await _operations.EnterExclusiveAsync(
                _operations.ShutdownToken,
                allowReadOnlyObservations: true);
            var token = maintenance.Token;
            var checks = new List<CoreUpdateBatchCheck>(coreTypes.Count);

            foreach (var coreType in coreTypes)
            {
                currentTarget = coreType.ToString();
                var wasRunning = IsCoreTypeRunning(coreType);
                PublishCoreUpdateProgress(coreType, "checking", false, false, wasRunning, null, null, batch: true);
                var checkedUpdate = await CheckCoreUpdateResultAsync(coreType, null, useProxy, token);
                var check = checkedUpdate.Result;
                if (checkedUpdate.IsUpToDate)
                {
                    checks.Add(new CoreUpdateBatchCheck(coreType, check, IsUpToDate: true));
                    PublishCoreUpdateProgress(coreType, "completed", true, true, wasRunning,
                        check.Version?.ToString(), check.Msg ?? "Already up to date.", batch: true);
                    continue;
                }

                if (!check.Success || check.Version is null || string.IsNullOrWhiteSpace(check.Url))
                {
                    overallSuccess = false;
                    PublishCoreUpdateProgress(coreType, "failed", true, false, wasRunning, null, check.Msg, batch: true);
                    AddLog("update", $"Batch update check failed for {coreType}: {check.Msg}");
                    checks.Add(new CoreUpdateBatchCheck(coreType, check, IsUpToDate: false, CheckFailed: true));
                    continue;
                }

                checks.Add(new CoreUpdateBatchCheck(coreType, check, IsUpToDate: false));
                if (!apply)
                {
                    PublishCoreUpdateProgress(coreType, "completed", true, true, wasRunning,
                        check.Version.ToString(), check.Msg ?? "An update is available.", batch: true);
                }
            }

            if (includeGeoFiles)
            {
                PublishGeoUpdateProgress("checking", false, false,
                    "ServiceLib has no read-only GeoFiles check; the transactional GeoFiles update runs only during batch apply.", batch: true);
                if (!apply)
                {
                    PublishGeoUpdateProgress("completed", true, true,
                        "GeoFiles will be downloaded, validated, and applied only when batch apply is enabled.", batch: true);
                }
            }

            WebUpdateReleaseCheck? webCheck = null;
            if (includeWeb)
            {
                currentTarget = WebUpdateTarget;
                PublishWebUpdateProgress("checking", false, false, null, batch: true);
                webCheck = await CheckWebUpdateReleaseAsync(GetUpdatePreRelease(WebUpdateTarget), useProxy, token);
                Volatile.Write(ref _latestWebUpdateVersion, webCheck.Version);
                if (!string.IsNullOrEmpty(webCheck.Error))
                {
                    overallSuccess = false;
                    PublishWebUpdateProgress("failed", true, false, webCheck.Error, webCheck.Version, batch: true);
                }
                else if (!webCheck.IsUpdateAvailable)
                {
                    PublishWebUpdateProgress("completed", true, true, webCheck.Detail ?? "The Web application is up to date.",
                        webCheck.Version, batch: true);
                }
                else if (!apply)
                {
                    var detail = CanInstallWebUpdate()
                        ? "An update is available."
                        : $"An update is available; {GetWebUpdateInstallReasonKey()}";
                    PublishWebUpdateProgress("completed", true, true, detail, webCheck.Version, batch: true);
                }
                else if (!CanInstallWebUpdate())
                {
                    overallSuccess = false;
                    PublishWebUpdateProgress("failed", true, false, GetWebUpdateInstallReasonKey(), webCheck.Version, batch: true);
                }
            }

            if (!apply)
            {
                _events.Publish("core-update-batch-completed", new { success = overallSuccess, apply = false });
                return;
            }

            if (checks.Any(item => item.CheckFailed) || webCheck is { Error: { Length: > 0 } })
            {
                overallSuccess = false;
                foreach (var item in checks.Where(item => !item.CheckFailed && !item.IsUpToDate))
                {
                    PublishCoreUpdateProgress(item.CoreType, "failed", true, false, IsCoreTypeRunning(item.CoreType),
                        item.Check.Version?.ToString(), "No targets were installed because at least one selected update check failed.", batch: true);
                }
                if (includeGeoFiles)
                {
                    PublishGeoUpdateProgress("failed", true, false, "No updates were installed because a selected update check failed.", batch: true);
                }
                if (webCheck is { Error: { Length: > 0 } })
                {
                    PublishWebUpdateProgress("failed", true, false, "No updates were installed because a selected update check failed.",
                        webCheck.Version, batch: true);
                }
                _events.Publish("core-update-batch-completed", new { success = false, apply = true });
                return;
            }

            var targetsToStage = checks.Where(item => !item.IsUpToDate).ToArray();
            var unsupportedTargets = targetsToStage.Where(item => !CanInstallCoreUpdate(item.CoreType)).ToArray();
            if (unsupportedTargets.Length > 0)
            {
                overallSuccess = false;
                foreach (var item in unsupportedTargets)
                {
                    PublishCoreUpdateProgress(item.CoreType, "failed", true, false, IsCoreTypeRunning(item.CoreType),
                        item.Check.Version?.ToString(), "This Web deployment can check but cannot safely install this target.", batch: true);
                }
                targetsToStage = targetsToStage.Where(item => CanInstallCoreUpdate(item.CoreType)).ToArray();
            }
            var shouldInstallWeb = includeWeb && webCheck is { IsUpdateAvailable: true } && CanInstallWebUpdate();
            if (targetsToStage.Length == 0 && !includeGeoFiles && !shouldInstallWeb)
            {
                _events.Publish("core-update-batch-completed", new { success = overallSuccess, apply = true });
                return;
            }

            // Complete the GeoFiles transaction while the serving Core remains available.
            // The Web layer snapshots/validates/rolls back files around the upstream updater.
            async Task PrepareAdditionalUpdatesBeforeCoreApplyAsync()
            {
                if (includeGeoFiles)
                {
                    currentTarget = GeoFilesUpdateTarget;
                    PublishGeoUpdateProgress("downloading", false, false, null, batch: true);
                    await ApplyGeoFilesUpdateAsync(useProxy, token);
                    PublishGeoUpdateProgress("completed", true, true, null, batch: true);
                }
                if (shouldInstallWeb && webCheck is { } availableWebCheck)
                {
                    currentTarget = WebUpdateTarget;
                    webStage = await StageWebUpdateAsync(availableWebCheck, useProxy, token, batch: true);
                }
            }

            await CoreUpdateWorkflow.StageAllThenApplyAsync(
                targetsToStage,
                async target =>
                {
                    currentTarget = target.CoreType.ToString();
                    var result = await DownloadAndVerifyCoreUpdateAsync(target.CoreType, target.Check, useProxy, token, batch: true);
                    staged.Add(result);
                    return result;
                },
                async completeStage =>
                {
                    // This callback is reached only after every selected package has been
                    // downloaded, extracted, and version-verified successfully.
                    foreach (var stage in completeStage)
                    {
                        PublishCoreUpdateProgress(stage.CoreType, "waiting", false, false,
                            IsCoreTypeRunning(stage.CoreType), stage.VersionOutput, "All selected Core packages are staged and verified.", batch: true);
                    }

                    foreach (var stage in completeStage)
                    {
                        currentTarget = stage.CoreType.ToString();
                        var result = await ApplyCoreUpdateAsync(stage, token, batch: true);
                        overallSuccess &= result.Success;
                        PublishCoreUpdateProgress(stage.CoreType,
                            result.Success ? "completed" : "failed",
                            isComplete: true,
                            success: result.Success,
                            result.CoreWasRunning,
                            result.Version ?? stage.VersionOutput,
                            result.Detail,
                            batch: true);
                    }
                    return true;
                },
                includeGeoFiles || shouldInstallWeb ? PrepareAdditionalUpdatesBeforeCoreApplyAsync : null);

            if (webStage is not null)
            {
                currentTarget = WebUpdateTarget;
                await ApplyStagedWebUpdateAsync(webStage, token, batch: true);
                // The helper takes over only after every selected Core/Geo package has
                // completed its apply phase; no remaining batch work follows the restart.
                return;
            }

            _events.Publish("core-update-batch-completed", new { success = overallSuccess, apply = true });
        }
        catch (OperationCanceledException) when (_operations.IsStopping)
        {
            overallSuccess = false;
            if (currentTarget == GeoFilesUpdateTarget)
            {
                PublishGeoUpdateProgress("failed", true, false, "Canceled during graceful shutdown.", batch: true);
            }
            else if (currentTarget == WebUpdateTarget)
            {
                PublishWebUpdateProgress("failed", true, false, "Canceled during graceful shutdown.", batch: true);
            }
            else if (Enum.TryParse<ECoreType>(currentTarget, out var coreType))
            {
                PublishCoreUpdateProgress(coreType, "failed", true, false, IsCoreTypeRunning(coreType), null,
                    "Canceled during graceful shutdown.", batch: true);
            }
            _events.Publish("core-update-batch-completed", new { success = false, apply });
        }
        catch (Exception exception)
        {
            overallSuccess = false;
            AddLog("update", $"Core update batch failed at {currentTarget}: {exception}");
            if (Enum.TryParse<ECoreType>(currentTarget, out var coreType))
            {
                PublishCoreUpdateProgress(coreType, "failed", true, false, IsCoreTypeRunning(coreType), null, exception.Message, batch: true);
            }
            else if (currentTarget == GeoFilesUpdateTarget)
            {
                PublishGeoUpdateProgress("failed", true, false, exception.Message, batch: true);
            }
            else if (currentTarget == WebUpdateTarget)
            {
                PublishWebUpdateProgress("failed", true, false, exception.Message, batch: true);
            }
            foreach (var notApplied in staged)
            {
                var progress = _updateProgress.GetValueOrDefault(notApplied.CoreType.ToString());
                if (progress is { IsComplete: false })
                {
                    PublishCoreUpdateProgress(notApplied.CoreType, "failed", true, false,
                        IsCoreTypeRunning(notApplied.CoreType), notApplied.VersionOutput,
                        "The staged package was not applied because another selected target failed.", batch: true);
                }
            }
            _events.Publish("core-update-batch-completed", new { success = false, apply });
        }
        finally
        {
            CleanupWebUpdateStage(webStage);
            foreach (var stage in staged) CleanupCoreUpdateStage(stage);
            lock (_updateTaskGate)
            {
                _coreUpdateBatchTask = null;
            }
        }
    }

    private HashSet<string> GetAvailablePreReleaseUpdateTargets() =>
        GetAvailableWebCoreUpdateTypes()
            .Where(coreType => CoreInfoManager.Instance.GetCheckPreRelease(coreType, true))
            .Select(coreType => coreType.ToString())
            .Append(WebUpdateTarget)
            .ToHashSet(StringComparer.Ordinal);

    private IReadOnlyList<ECoreType> GetAvailableWebCoreUpdateTypes()
    {
        var manager = CoreInfoManager.Instance;
        return manager.GetCheckUpdateCoreTypes()
            .Where(coreType => coreType != ECoreType.v2rayN
                && manager.IsCheckUpdateSupported(coreType)
                && manager.GetCoreInfo(coreType) is { } coreInfo
                && IsCurrentPlatformDownloadSupported(coreInfo))
            .ToArray();
    }

    private bool CanCheckCoreUpdate(ECoreType coreType)
    {
        var manager = CoreInfoManager.Instance;
        return coreType != ECoreType.v2rayN
            && manager.GetCheckUpdateCoreTypes().Contains(coreType)
            && manager.IsCheckUpdateSupported(coreType)
            && manager.GetCoreInfo(coreType) is { } coreInfo
            && IsCurrentPlatformDownloadSupported(coreInfo);
    }

    private bool CanInstallCoreUpdate(ECoreType coreType) =>
        coreType != ECoreType.v2rayN
        && CanCheckCoreUpdate(coreType)
        && CoreUpdatePackageStager.SupportsCore(coreType);

    private async Task<CoreUpdateCheckResult> CheckCoreUpdateResultAsync(
        ECoreType coreType,
        bool? preRelease,
        bool? useProxy,
        CancellationToken cancellationToken)
    {
        var updateService = new UpdateService(Config, (_, _) => Task.CompletedTask);
        var result = await updateService.CheckHasUpdateOnly(
            coreType,
            GetUpdatePreRelease(coreType.ToString(), preRelease),
            useProxy ?? Config.CheckUpdateItem.UpdateViaProxy,
            cancellationToken);
        return new CoreUpdateCheckResult(result, IsUpToDateResult(coreType, result.Msg));
    }

    private async Task RunCoreUpdateAsync(ECoreType coreType, bool preRelease, bool useProxy)
    {
        CoreUpdateStage? stage = null;
        var wasRunning = IsCoreTypeRunning(coreType);
        try
        {
            var result = await CoreUpdateWorkflow.StageThenApplyAsync(
                async () =>
                {
                    await using var operation = await _operations.EnterOperationAsync(_operations.ShutdownToken);
                    PublishCoreUpdateProgress(coreType, "checking", isComplete: false, success: false, wasRunning, null, null);
                    var checkedUpdate = await CheckCoreUpdateResultAsync(coreType, preRelease, useProxy, operation.Token);
                    var check = checkedUpdate.Result;
                    if (checkedUpdate.IsUpToDate)
                    {
                        return new CoreUpdatePrepareResult(null, check);
                    }
                    if (!check.Success || check.Version is null || string.IsNullOrWhiteSpace(check.Url))
                    {
                        throw new InvalidOperationException(check.Msg ?? $"Could not check {coreType} updates.");
                    }

                    stage = await DownloadAndVerifyCoreUpdateAsync(coreType, check, useProxy, operation.Token);
                    return new CoreUpdatePrepareResult(stage, check);
                },
                async prepared =>
                {
                    if (prepared.Stage is null)
                    {
                        return new CoreUpdateApplyResult(true, false, wasRunning, prepared.Check.Msg ?? "Already up to date.", prepared.Check.Version?.ToString());
                    }

                    await using var maintenance = await _operations.EnterExclusiveAsync(
                        _operations.ShutdownToken,
                        allowReadOnlyObservations: true);
                    return await ApplyCoreUpdateAsync(prepared.Stage, maintenance.Token);
                });

            PublishCoreUpdateProgress(
                coreType,
                "completed",
                isComplete: true,
                success: result.Success,
                result.CoreWasRunning,
                result.Version ?? stage?.VersionOutput,
                result.Detail);
            if (!result.Success)
            {
                AddLog("update", $"{coreType} update failed: {result.Detail}");
            }
        }
        catch (OperationCanceledException) when (_operations.IsStopping)
        {
            PublishCoreUpdateProgress(coreType, "failed", isComplete: true, success: false, wasRunning, stage?.VersionOutput, "Canceled during graceful shutdown.");
        }
        catch (Exception exception)
        {
            AddLog("update", $"{coreType} update failed: {exception.Message}");
            PublishCoreUpdateProgress(coreType, "failed", isComplete: true, success: false, wasRunning, stage?.VersionOutput, exception.Message);
        }
        finally
        {
            CleanupCoreUpdateStage(stage);
            lock (_updateTaskGate)
            {
                _coreUpdateTasks.TryRemove(coreType, out _);
            }
        }
    }

    private async Task<CoreUpdateStage> DownloadAndVerifyCoreUpdateAsync(
        ECoreType coreType,
        UpdateResult check,
        bool useProxy,
        CancellationToken cancellationToken,
        bool batch = false)
    {
        var installPath = Path.GetFullPath(Utils.GetBinPath(string.Empty, coreType.ToString()));
        var installParent = Path.GetDirectoryName(installPath)
            ?? throw new InvalidOperationException($"The {coreType} installation path has no parent directory.");
        var archivePath = Utils.GetTempPath($"core-update-{coreType}-{Guid.NewGuid():N}{GetArchiveSuffix(check.Url!)}");
        var packagePath = Path.Combine(installParent, $".{coreType}-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packagePath);

        try
        {
            PublishCoreUpdateProgress(coreType, "downloading", isComplete: false, success: false, IsCoreTypeRunning(coreType), check.Version?.ToString(), check.Url, batch);
            var download = new DownloadService();
            var downloadFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            download.Error += (_, args) => downloadFailure.TrySetResult(args.GetException());
            download.UpdateCompleted += (_, progress) =>
            {
                PublishCoreUpdateProgress(coreType, "downloading", isComplete: false, success: false, IsCoreTypeRunning(coreType), check.Version?.ToString(), progress.Msg, batch);
            };
            await download.DownloadFileAsync(new FileDownloadRequest
            {
                FileUrl = check.Url!,
                FilePath = archivePath,
                DisplayFileName = Path.GetFileName(archivePath),
            },
            useProxy,
            cancellationToken);

            if (downloadFailure.Task.IsCompletedSuccessfully)
            {
                throw new IOException("The core package download failed.", await downloadFailure.Task);
            }
            if (!File.Exists(archivePath))
            {
                throw new IOException("The core package download did not produce an archive.");
            }

            PublishCoreUpdateProgress(coreType, "verifying", isComplete: false, success: false, IsCoreTypeRunning(coreType), check.Version?.ToString(), null, batch);
            await CoreUpdatePackageStager.ExtractAsync(coreType, archivePath, packagePath, cancellationToken);
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType)
                ?? throw new InvalidOperationException($"{coreType} version metadata is unavailable.");
            var stagedExecutable = FindCoreExecutable(coreInfo, packagePath)
                ?? throw new InvalidDataException($"The downloaded {coreType} archive does not contain a usable executable.");
            await Utils.SetLinuxChmod(stagedExecutable);
            var versionOutput = await VerifyCoreExecutableAsync(coreType, coreInfo, stagedExecutable, cancellationToken);

            return new CoreUpdateStage(coreType, installPath, archivePath, packagePath, versionOutput);
        }
        catch
        {
            CleanupCoreUpdateStageFiles(archivePath, packagePath);
            throw;
        }
    }

    private async Task<CoreUpdateApplyResult> ApplyCoreUpdateAsync(CoreUpdateStage stage, CancellationToken cancellationToken, bool batch = false)
    {
        await _coreGate.WaitAsync(cancellationToken);
        var installPath = stage.InstallPath;
        var parentDirectory = Path.GetDirectoryName(installPath)
            ?? throw new InvalidOperationException("The Core installation path has no parent directory.");
        var candidatePath = Path.Combine(parentDirectory, $".{stage.CoreType}-candidate-{Guid.NewGuid():N}");
        var backupPath = Path.Combine(parentDirectory, $".{stage.CoreType}-backup-{Guid.NewGuid():N}");
        var hadInstalledCore = Directory.Exists(installPath);
        var originalCoreWasRunning = IsCoreTypeRunning(stage.CoreType);
        ProfileItem? runningProfile = null;
        var oldCoreWasStopped = false;
        var candidateInstalled = false;

        try
        {
            if (originalCoreWasRunning)
            {
                runningProfile = await AppManager.Instance.GetProfileItem(CurrentCoreRuntime.ProfileId ?? string.Empty);
                if (runningProfile is null)
                {
                    return new CoreUpdateApplyResult(false, false, true, "The active profile could not be loaded; the running Core was left unchanged.");
                }
            }

            // Prepare and verify the complete replacement while the serving Core is still
            // alive. This phase performs no network access; proxy-backed downloads and all
            // archive verification have already completed in DownloadAndVerifyCoreUpdateAsync.
            if (hadInstalledCore)
            {
                CopyCoreDirectory(installPath, candidatePath);
            }
            else
            {
                Directory.CreateDirectory(candidatePath);
            }
            foreach (var packageFile in Directory.EnumerateFiles(stage.PackagePath, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(packageFile, Path.Combine(candidatePath, Path.GetFileName(packageFile)), overwrite: true);
            }
            if (stage.CoreType == ECoreType.Xray)
            {
                CopyLatestGeoFilesForApply(installPath, candidatePath);
            }

            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(stage.CoreType)
                ?? throw new InvalidOperationException($"{stage.CoreType} version metadata is unavailable.");
            var candidateExecutable = FindCoreExecutable(coreInfo, candidatePath)
                ?? throw new InvalidDataException($"The prepared {stage.CoreType} directory has no executable.");
            await Utils.SetLinuxChmod(candidateExecutable);
            await VerifyCoreExecutableAsync(stage.CoreType, coreInfo, candidateExecutable, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            PublishCoreUpdateProgress(stage.CoreType, "stopping-core", isComplete: false, success: false, originalCoreWasRunning, stage.VersionOutput,
                originalCoreWasRunning ? null : "The updated Core was not running and will remain stopped.", batch);
            if (originalCoreWasRunning)
            {
                SetCoreRuntime(CurrentCoreRuntime with { State = CoreRuntimeState.Stopping, LastFailure = null });
                await StopCoreMonitorAsync();
                await StopCoreAndConfirmAsync(cancellationToken);
                SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
                oldCoreWasStopped = true;
            }

            PublishCoreUpdateProgress(stage.CoreType, "installing", isComplete: false, success: false, originalCoreWasRunning, stage.VersionOutput, null, batch);
            if (hadInstalledCore)
            {
                Directory.Move(installPath, backupPath);
            }
            try
            {
                Directory.Move(candidatePath, installPath);
                candidateInstalled = true;
            }
            catch
            {
                if (hadInstalledCore && Directory.Exists(backupPath) && !Directory.Exists(installPath))
                {
                    Directory.Move(backupPath, installPath);
                }
                throw;
            }

            await CoreManager.Instance.Init(Config, OnCoreMessageAsync);
            if (stage.CoreType == ECoreType.Xray)
            {
                _xrayPath = FindXrayExecutable(out var missingXrayMessage);
                if (_xrayPath is null)
                {
                    throw new InvalidDataException(missingXrayMessage);
                }
            }

            if (originalCoreWasRunning && runningProfile is not null)
            {
                PublishCoreUpdateProgress(stage.CoreType, "restarting-core", isComplete: false, success: false, true, stage.VersionOutput, null, batch);
                var restart = await StartCoreLockedAsync(runningProfile, cancellationToken);
                if (!restart.Success)
                {
                    throw new InvalidOperationException($"The updated {stage.CoreType} failed to restart ({restart.Code}).");
                }
            }

            if (Directory.Exists(backupPath))
            {
                try
                {
                    Directory.Delete(backupPath, recursive: true);
                }
                catch (Exception exception)
                {
                    AddLog("update", $"{stage.CoreType} updated; previous Core cleanup deferred: {exception.Message}");
                }
            }

            return new CoreUpdateApplyResult(true, false, originalCoreWasRunning, "Update installed and verified.", stage.VersionOutput);
        }
        catch (OperationCanceledException)
        {
            if (candidateInstalled || oldCoreWasStopped || Directory.Exists(backupPath))
            {
                var rollback = await RollBackCoreUpdateAsync(
                    stage.CoreType,
                    installPath,
                    backupPath,
                    candidateInstalled,
                    hadInstalledCore,
                    oldCoreWasStopped,
                    originalCoreWasRunning,
                    runningProfile,
                    batch);
                if (!rollback.Completed)
                {
                    AddLog("update", rollback.FilesRestored
                        ? $"{stage.CoreType} previous files were restored, but the old Core process could not be relaunched."
                        : $"{stage.CoreType} rollback did not complete; previous files remain at {backupPath}.");
                }
            }
            throw;
        }
        catch (Exception exception)
        {
            AddLog("update", $"{stage.CoreType} apply failed: {exception.Message}");
            if (CurrentCoreRuntime.State is CoreRuntimeState.Stopping or CoreRuntimeState.Restarting or CoreRuntimeState.Starting)
            {
                SetCoreRuntime(CurrentCoreRuntime with
                {
                    State = CoreRuntimeState.Faulted,
                    ProcessIds = GetActiveCoreProcessIds(),
                    LastFailure = exception.Message,
                });
            }
            var rollback = new CoreUpdateRollbackResult(FilesRestored: true, CoreRestarted: true);
            if (candidateInstalled || oldCoreWasStopped || Directory.Exists(backupPath))
            {
                rollback = await RollBackCoreUpdateAsync(
                    stage.CoreType,
                    installPath,
                    backupPath,
                    candidateInstalled,
                    hadInstalledCore,
                    oldCoreWasStopped,
                    originalCoreWasRunning,
                    runningProfile,
                    batch);
            }
            var detail = rollback.Completed
                ? $"{exception.Message} The previous Core was restored."
                : rollback.FilesRestored
                    ? $"{exception.Message} The previous Core files were restored, but its process could not be restarted."
                    : $"{exception.Message} Rollback did not complete; see the runtime log for the retained backup path.";
            return new CoreUpdateApplyResult(false, rollback.Completed, originalCoreWasRunning, detail, stage.VersionOutput);
        }
        finally
        {
            try
            {
                if (Directory.Exists(candidatePath))
                {
                    Directory.Delete(candidatePath, recursive: true);
                }
            }
            catch (Exception exception)
            {
                AddLog("update", $"{stage.CoreType} candidate cleanup failed: {exception.Message}");
            }
            _coreGate.Release();
        }
    }

    private async Task<CoreUpdateRollbackResult> RollBackCoreUpdateAsync(
        ECoreType coreType,
        string installPath,
        string backupPath,
        bool candidateInstalled,
        bool hadInstalledCore,
        bool oldCoreWasStopped,
        bool wasRunning,
        ProfileItem? runningProfile,
        bool batch = false)
    {
        var filesRestored = false;
        try
        {
            return await CoreUpdateWorkflow.RollBackAndRestoreAsync(
                async () =>
                {
                    // Do not stop an unrelated active Core when the target being updated was idle.
                    if (oldCoreWasStopped && wasRunning)
                    {
                        await StopCoreMonitorAsync();
                        await StopCoreAndConfirmAsync(CancellationToken.None);
                        SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
                    }
                    CoreUpdateWorkflow.RestorePreviousDirectory(
                        installPath, backupPath, candidateInstalled, hadInstalledCore);
                    filesRestored = true;
                },
                async () =>
                {
                    await CoreManager.Instance.Init(Config, OnCoreMessageAsync);
                    if (coreType == ECoreType.Xray)
                    {
                        _xrayPath = FindXrayExecutable(out _);
                    }
                },
                wasRunning && runningProfile is not null,
                async () =>
                {
                    PublishCoreUpdateProgress(coreType, "restarting-core", isComplete: false, success: false,
                        true, null, "Restarting the restored Core.", batch);
                    return (await StartCoreLockedAsync(runningProfile!, CancellationToken.None)).Success;
                });
        }
        catch (Exception exception)
        {
            AddLog("update", $"{coreType} rollback failed: {exception.Message}");
            SetCoreRuntime(CurrentCoreRuntime with
            {
                State = CoreRuntimeState.Faulted,
                ProcessIds = GetActiveCoreProcessIds(),
                LastFailure = exception.Message,
            });
            return new CoreUpdateRollbackResult(filesRestored, CoreRestarted: false);
        }
    }

    private async Task<string> VerifyCoreExecutableAsync(
        ECoreType coreType,
        CoreInfo coreInfo,
        string executablePath,
        CancellationToken cancellationToken)
    {
        var versionArgument = coreInfo.VersionArg
            ?? throw new InvalidDataException($"{coreType} has no version-check command configured.");
        var versionOutput = await Utils.GetCliWrapOutput(executablePath, versionArgument, cancellationToken);
        var expectedName = coreInfo.Match ?? coreType.ToString();
        if (!CoreExecutableVersionCheck.IsValid(versionOutput, expectedName))
        {
            throw new InvalidDataException($"The staged {coreType} executable did not pass its version check.");
        }
        return versionOutput!.Trim();
    }

    private static string? FindCoreExecutable(CoreInfo coreInfo, string directory) =>
        (coreInfo.CoreExes ?? [])
            .Select(name => Path.Combine(directory, Utils.GetExeName(name)))
            .FirstOrDefault(File.Exists);

    private static bool IsCurrentPlatformDownloadSupported(CoreInfo coreInfo)
    {
        var downloadUrl = Utils.IsWindows()
            ? RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo.DownloadUrlWinArm64,
                Architecture.X64 => coreInfo.DownloadUrlWin64,
                _ => null,
            }
            : Utils.IsLinux()
                ? RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => coreInfo.DownloadUrlLinuxArm64,
                    Architecture.RiscV64 => coreInfo.DownloadUrlLinuxRiscV64,
                    Architecture.LoongArch64 => coreInfo.DownloadUrlLinuxLoong64,
                    Architecture.X64 => coreInfo.DownloadUrlLinux64,
                    _ => null,
                }
                : Utils.IsMacOS()
                    ? RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.Arm64 => coreInfo.DownloadUrlOSXArm64,
                        Architecture.X64 => coreInfo.DownloadUrlOSX64,
                        _ => null,
                    }
                    : null;

        return !string.IsNullOrWhiteSpace(downloadUrl);
    }

    internal static bool IsUpToDateResult(ECoreType coreType, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string typeMarker = "__V2RAYN_CORE_TYPE__";
        const string versionMarker = "__V2RAYN_CORE_VERSION__";
        var template = string.Format(ResUI.IsLatestCore, typeMarker, versionMarker);
        var typePosition = template.IndexOf(typeMarker, StringComparison.Ordinal);
        var versionPosition = template.IndexOf(versionMarker, StringComparison.Ordinal);
        if (typePosition < 0 || versionPosition <= typePosition)
        {
            return false;
        }

        var expectedPrefix = template[..typePosition]
            + coreType
            + template[(typePosition + typeMarker.Length)..versionPosition];
        var suffix = template[(versionPosition + versionMarker.Length)..];
        var normalizedMessage = message.Trim();
        if (!normalizedMessage.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
            || !normalizedMessage.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var versionLength = normalizedMessage.Length - expectedPrefix.Length - suffix.Length;
        return versionLength > 0
            && normalizedMessage.Substring(expectedPrefix.Length, versionLength).All(character => !char.IsWhiteSpace(character));
    }

    private bool IsCoreTypeRunning(ECoreType coreType) =>
        CurrentCoreRuntime.State is CoreRuntimeState.Running or CoreRuntimeState.Faulted
        && CurrentCoreRuntime.CoreType == coreType
        && HasTrackedCoreProcesses;

    private static string GetArchiveSuffix(string downloadUrl)
    {
        var path = Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ? uri.AbsolutePath : downloadUrl;
        return path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            ? ".tar.gz"
            : Path.GetExtension(path);
    }

    private static void CopyCoreDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), overwrite: true);
        }
        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyCoreDirectory(sourceSubdirectory, Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory)));
        }
    }

    private void CleanupCoreUpdateStage(CoreUpdateStage? stage)
    {
        if (stage is not null)
        {
            CleanupCoreUpdateStageFiles(stage.ArchivePath, stage.PackagePath);
        }
    }

    private void CleanupCoreUpdateStageFiles(string archivePath, string packagePath)
    {
        try
        {
            if (Directory.Exists(packagePath))
            {
                Directory.Delete(packagePath, recursive: true);
            }
        }
        catch (Exception exception)
        {
            AddLog("update", $"Core package staging cleanup failed: {exception.Message}");
        }
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
        catch (Exception exception)
        {
            AddLog("update", $"Core archive cleanup failed: {exception.Message}");
        }
    }

    private async Task RunGeoUpdateAsync(bool useProxy)
    {
        try
        {
            PublishGeoUpdateProgress("downloading", isComplete: false, success: false, null);
            await using var operation = await _operations.EnterExclusiveAsync(
                _operations.ShutdownToken,
                allowReadOnlyObservations: true);
            await ApplyGeoFilesUpdateAsync(useProxy, operation.Token);
            PublishGeoUpdateProgress("completed", isComplete: true, success: true, null);
        }
        catch (OperationCanceledException) when (_operations.IsStopping)
        {
            PublishGeoUpdateProgress("failed", isComplete: true, success: false, "Canceled during graceful shutdown.");
        }
        catch (Exception exception)
        {
            AddLog("update", $"GeoFiles update failed: {exception.Message}");
            PublishGeoUpdateProgress("failed", isComplete: true, success: false, exception.Message);
        }
        finally
        {
            lock (_updateTaskGate)
            {
                _geoUpdateTask = null;
            }
        }
    }

    private async Task ApplyGeoFilesUpdateAsync(
        bool useProxy,
        CancellationToken cancellationToken,
        bool publishProgress = true,
        Func<Func<Task>, CancellationToken, Task>? beforeCommit = null)
    {
        await _geoUpdateGate.RunAsync(async () =>
        {
            var completion = new GeoFilesUpdateCompletion();
            var updater = new UpdateService(Config, (success, message) =>
            {
                AddLog("update", message);
                completion.Report(success, message, IsGeoDownloadProgressMessage(message));
                if (publishProgress)
                {
                    _events.Publish("geo-update-progress", new
                    {
                        success,
                        code = success ? "ok" : "geo_update_progress",
                        messageKey = success ? ApiMessageKeys.CommonCompleted : ApiMessageKeys.GeoUpdateProgress,
                        rawLog = message,
                    });
                }
                return Task.CompletedTask;
            });

            var requiredFiles = GetRequiredGeoFiles();
            var managedFiles = await GetManagedGeoFilesAsync();
            await GeoFilesUpdateTransaction.ApplyAsync(
                managedFiles,
                requiredFiles,
                async token =>
                {
                    // Keep download selection, URLs, and installation behavior in the upstream
                    // public API. Its legacy callback reports progress/errors separately from its
                    // final success notification, so reject non-progress failures before commit.
                    await updater.UpdateGeoFileAll(useProxy, token);
                    completion.EnsureSuccessful();
                },
                cancellationToken,
                beforeCommit);
        }, cancellationToken);
    }

    private async Task<string[]> GetManagedGeoFilesAsync()
    {
        var files = new HashSet<string>(GetRequiredGeoFiles(), StringComparerForPaths);
        var geoipRules = new List<string>();
        var geositeRules = new List<string>();

        var routingItems = await AppManager.Instance.RoutingItems();
        foreach (var routing in routingItems ?? [])
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
            foreach (var rule in rules ?? [])
            {
                AddPrefixedItems(rule.Ip, Global.GeoIPPrefix, geoipRules);
                AddPrefixedItems(rule.Domain, Global.GeoSitePrefix, geositeRules);
            }
        }

        var dnsItem = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        if (dnsItem is not null)
        {
            ExtractDnsRuleSets(dnsItem.NormalDNS, geoipRules, geositeRules);
            ExtractDnsRuleSets(dnsItem.TunDNS, geoipRules, geositeRules);
        }

        // Match ServiceLib.UpdateService.GetSrsFileAllRequest exactly: configured rule sets,
        // plus its default geosite downloads. Do not treat every .srs or geo*-named file in
        // the Core directory as owned by this transaction.
        geositeRules.AddRange(["google", "cn", "geolocation-cn", "category-ads-all"]);
        var srsDirectory = Path.GetFullPath(Utils.GetBinPath("srss"));
        foreach (var (type, names) in new[] { ("geoip", geoipRules), ("geosite", geositeRules) })
        {
            foreach (var name in names.Distinct(StringComparer.Ordinal))
            {
                files.Add(GetManagedSrsPath(srsDirectory, type, name));
            }
        }

        return files.OrderBy(path => path, StringComparerForPaths).ToArray();
    }

    private static string GetManagedSrsPath(string srsDirectory, string type, string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name is "." or ".."
            || name.Contains('/')
            || name.Contains('\\')
            || name.Contains(':')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(name, name.TrimEnd(' ', '.'), StringComparison.Ordinal)
            || Path.GetFileName(name) != name)
        {
            throw new InvalidDataException("A GeoFiles rule-set name is not a safe file name.");
        }

        var directory = Path.GetFullPath(srsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(directory, $"{type}-{name}.srs"));
        if (!string.Equals(Path.GetDirectoryName(path), directory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("A GeoFiles rule-set path escapes the managed rules directory.");
        }
        return path;
    }

    private static void AddPrefixedItems(IEnumerable<string>? items, string prefix, ICollection<string> output)
    {
        if (items is null) return;
        foreach (var item in items)
        {
            if (item.StartsWith(prefix, StringComparison.Ordinal))
            {
                output.Add(item[prefix.Length..]);
            }
        }
    }

    private static void ExtractDnsRuleSets(string? dnsJson, ICollection<string> geoipRules, ICollection<string> geositeRules)
    {
        if (string.IsNullOrEmpty(dnsJson)) return;
        try
        {
            var dns = JsonUtils.Deserialize<Dns4Sbox>(dnsJson);
            foreach (var rule in dns?.rules ?? [])
            {
                ExtractSrsRuleSets(rule, geoipRules, geositeRules);
            }
        }
        catch
        {
            // Match the updater: malformed optional DNS JSON does not stop GeoFiles updates.
        }
    }

    private static void ExtractSrsRuleSets(Rule4Sbox? rule, ICollection<string> geoipRules, ICollection<string> geositeRules)
    {
        if (rule is null) return;
        AddPrefixedItems(rule.rule_set, "geosite-", geositeRules);
        AddPrefixedItems(rule.rule_set, "geoip-", geoipRules);
        foreach (var nestedRule in rule.rules ?? [])
        {
            ExtractSrsRuleSets(nestedRule, geoipRules, geositeRules);
        }
    }

    private string[] GetRequiredGeoFiles()
    {
        var binDirectory = Utils.GetBinPath(string.Empty);
        var required = new List<string>
        {
            Path.Combine(binDirectory, "geosite.dat"),
            Path.Combine(binDirectory, "geoip.dat"),
        };
        if (string.IsNullOrEmpty(Config.ConstItem.GeoSourceUrl))
        {
            required.AddRange(Global.OtherGeoUrls.Select(url => Path.Combine(binDirectory, Path.GetFileName(url))));
        }
        return required.Distinct(StringComparerForPaths).ToArray();
    }

    private static bool IsGeoDownloadProgressMessage(string message)
    {
        var normalized = message.TrimStart();
        return normalized.StartsWith(ResUI.Downloading, StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(normalized, @"^\d+/\d+\s*\|", RegexOptions.CultureInvariant);
    }

    private static StringComparer StringComparerForPaths => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private void PublishCoreUpdateProgress(
        ECoreType coreType,
        string phase,
        bool isComplete,
        bool success,
        bool coreWasRunning,
        string? version,
        string? detail,
        bool batch = false)
    {
        var progress = new CoreUpdateProgressView(coreType.ToString(), phase, isComplete, success, coreWasRunning, version, detail, batch);
        if (!_updateProgress.TryGetValue(coreType.ToString(), out var previous) || previous.Phase != phase)
        {
            AddLog("update", $"{coreType} update phase: {phase}");
        }
        _updateProgress[coreType.ToString()] = progress;
        _events.Publish("core-update-progress", progress);
        if (coreType == ECoreType.Xray && isComplete)
        {
            _events.Publish("xray-update-completed", success
                ? OperationView.Ok(ApiMessageKeys.XrayUpdateCompleted, new { version, coreRestarted = coreWasRunning })
                : OperationView.Fail("xray_update_failed", ApiMessageKeys.XrayUpdateFailed, new { coreWasRunning, detail }));
        }
    }

    private void PublishGeoUpdateProgress(string phase, bool isComplete, bool success, string? detail, bool batch = false)
    {
        var progress = new CoreUpdateProgressView(GeoFilesUpdateTarget, phase, isComplete, success, false, null, detail, batch);
        if (!_updateProgress.TryGetValue(GeoFilesUpdateTarget, out var previous) || previous.Phase != phase)
        {
            AddLog("update", $"GeoFiles update phase: {phase}");
        }
        _updateProgress[GeoFilesUpdateTarget] = progress;
        _events.Publish("core-update-progress", progress);
        if (isComplete)
        {
            _events.Publish("geo-update-completed", success
                ? OperationView.Ok(ApiMessageKeys.CommonCompleted, new { batch })
                : OperationView.Fail("geo_update_failed", ApiMessageKeys.CoreUpdateFailed, new { batch }));
        }
    }

    private static string GetCoreUpdateOperationName(ECoreType coreType) =>
        $"core-update-{coreType.ToString().ToLowerInvariant()}";

    private static string GetCoreUpdateNameKey(ECoreType coreType) => coreType switch
    {
        ECoreType.Xray => "maintenance.coreNames.xray",
        ECoreType.mihomo => "maintenance.coreNames.mihomo",
        ECoreType.sing_box => "maintenance.coreNames.singBox",
        ECoreType.v2rayN => "maintenance.coreNames.v2rayN",
        _ => "maintenance.coreNames.other",
    };

    internal static void CopyLatestGeoFilesForApply(string installPath, string stagingPath)
    {
        if (!Directory.Exists(installPath))
        {
            return;
        }
        foreach (var source in Directory.EnumerateFiles(installPath, "geo*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(source, Path.Combine(stagingPath, Path.GetFileName(source)), overwrite: true);
        }
    }

    internal sealed record CoreUpdateStage(ECoreType CoreType, string InstallPath, string ArchivePath, string PackagePath, string VersionOutput);
    private sealed record CoreUpdateCheckResult(UpdateResult Result, bool IsUpToDate);
    private sealed record CoreUpdateBatchCheck(ECoreType CoreType, UpdateResult Check, bool IsUpToDate, bool CheckFailed = false);
    private sealed record CoreUpdatePrepareResult(CoreUpdateStage? Stage, UpdateResult Check);
    private sealed record CoreUpdateApplyResult(bool Success, bool RolledBack, bool CoreWasRunning, string Detail, string? Version = null);
}

internal static class CoreUpdateRuntimePolicy
{
    public static bool IsTargetRunning(ECoreType target, ECoreType runningCore, DateTimeOffset? startedAt) =>
        startedAt is not null && target == runningCore;
}

internal static class CoreUpdateWorkflow
{
    public static void RestorePreviousDirectory(
        string installPath,
        string backupPath,
        bool candidateInstalled,
        bool hadInstalledCore)
    {
        if (candidateInstalled && Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }
        if (hadInstalledCore && Directory.Exists(backupPath) && !Directory.Exists(installPath))
        {
            Directory.Move(backupPath, installPath);
        }
    }

    public static async Task<CoreUpdateRollbackResult> RollBackAndRestoreAsync(
        Func<Task> restoreFilesAsync,
        Func<Task> initializePreviousCoreAsync,
        bool wasRunning,
        Func<Task<bool>> restartPreviousCoreAsync)
    {
        await restoreFilesAsync();
        await initializePreviousCoreAsync();
        var restarted = !wasRunning || await restartPreviousCoreAsync();
        return new CoreUpdateRollbackResult(FilesRestored: true, CoreRestarted: restarted);
    }

    public static async Task<TResult> StageThenApplyAsync<TStage, TResult>(
        Func<Task<TStage>> stageAsync,
        Func<TStage, Task<TResult>> applyAsync)
    {
        var staged = await stageAsync();
        return await applyAsync(staged);
    }

    public static async Task<TResult> StageAllThenApplyAsync<TTarget, TStage, TResult>(
        IReadOnlyList<TTarget> targets,
        Func<TTarget, Task<TStage>> stageAsync,
        Func<IReadOnlyList<TStage>, Task<TResult>> applyAsync,
        Func<Task>? beforeApplyAsync = null)
    {
        var staged = new List<TStage>(targets.Count);
        foreach (var target in targets)
        {
            staged.Add(await stageAsync(target));
        }
        if (beforeApplyAsync is not null)
        {
            await beforeApplyAsync();
        }
        return await applyAsync(staged);
    }
}

internal sealed record CoreUpdateRollbackResult(bool FilesRestored, bool CoreRestarted)
{
    public bool Completed => FilesRestored && CoreRestarted;
}
