using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Handler.Builder;
using ServiceLib.Helper;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using NLog;
using NLog.Config;
using NLog.Targets;
using v2rayN.Web.Adapters;
using v2rayN.Web.Contracts;

namespace v2rayN.Web.Services;

public sealed partial class V2rayRuntime(
    EventHub events,
    LogBuffer logs,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    RuntimeOperationCoordinator operations)
{
    private readonly EventHub _events = events;
    private readonly LogBuffer _logs = logs;
    private readonly IConfiguration _configuration = configuration;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly RuntimeOperationCoordinator _operations = operations;
    private readonly RuntimeMutationGate _mutations = new();
    private readonly SemaphoreSlim _coreGate = new(1, 1);
    private readonly SemaphoreSlim _regionalPresetGate = new(1, 1);
    private readonly SemaphoreSlim _coreMonitorGate = new(1, 1);
    private readonly object _subscriptionGate = new();
    private readonly object _speedtestGate = new();
    private readonly Dictionary<string, Task> _subscriptionTasks = new(StringComparer.Ordinal);
    private SpeedtestService? _speedtestService;
    private Task? _speedtestTask;
    private CancellationTokenSource? _speedtestCancellation;
    private CoreRuntimeSnapshot _coreRuntime = CoreRuntimeSnapshot.Stopped;
    private CancellationTokenSource? _coreMonitorCancellation;
    private Task? _coreMonitorTask;
    private CoreProcessIdentity[] _trackedCoreProcesses = [];
    private string? _xrayPath;
    private ServerSpeedItem? _latestTraffic;
    private bool _initialized;
    private bool _restoring;
    private int _restoreAndRestartRequested;

    private Config Config => AppManager.Instance.Config;
    private CoreRuntimeSnapshot CurrentCoreRuntime => Volatile.Read(ref _coreRuntime);
    public bool RestoreAndRestartRequested => Volatile.Read(ref _restoreAndRestartRequested) != 0;

    public int[] GetCoreProcessIds() => GetActiveCoreProcessIds();

    public string? GetCoreRuntimeProfileId() => CurrentCoreRuntime.ProfileId;

    public string GetCoreRuntimeState() => CurrentCoreRuntime.State.ToString().ToLowerInvariant();

    private void SetCoreRuntime(CoreRuntimeSnapshot snapshot)
    {
        snapshot = snapshot with { ProcessIds = GetActiveCoreProcessIds() };
        Volatile.Write(ref _coreRuntime, snapshot);
        if (snapshot.State != CoreRuntimeState.Running)
        {
            _coreMonitorCancellation?.Cancel();
        }
        _events.Publish("core-state", new
        {
            state = snapshot.State.ToString().ToLowerInvariant(),
            profileId = snapshot.ProfileId,
            coreType = snapshot.CoreType is { } coreType
                ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(coreType.ToString())
                : null,
            startedAt = snapshot.StartedAt,
            proxyPort = snapshot.ProxyPort,
            apiPort = snapshot.ApiPort,
            processIds = snapshot.ProcessIds,
            lastFailure = snapshot.LastFailure,
        });
    }

    private async Task StartCoreMonitorAsync()
    {
        await StopCoreMonitorAsync();
        var cancellation = new CancellationTokenSource();
        _coreMonitorCancellation = cancellation;
        _coreMonitorTask = Task.Run(() => MonitorCoreRuntimeAsync(cancellation.Token));
    }

    private async Task StopCoreMonitorAsync()
    {
        await _coreMonitorGate.WaitAsync();
        try
        {
            var cancellation = _coreMonitorCancellation;
            var task = _coreMonitorTask;
            _coreMonitorCancellation = null;
            _coreMonitorTask = null;
            if (cancellation is null) return;
            cancellation.Cancel();
            if (task is not null)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (OperationCanceledException) { }
                catch (TimeoutException) { AddLog("core", "Core runtime monitor did not stop within its cleanup budget."); }
            }
            cancellation.Dispose();
        }
        finally
        {
            _coreMonitorGate.Release();
        }
    }

    private CoreProcessIdentity[] GetActiveCoreProcesses() =>
        CoreProcessTracker.GetActiveProcesses(Volatile.Read(ref _trackedCoreProcesses));

    private int[] GetActiveCoreProcessIds() =>
        GetActiveCoreProcesses().Select(process => process.ProcessId).ToArray();

    private bool HasTrackedCoreProcesses => GetActiveCoreProcesses().Length > 0;

    private void SetTrackedCoreProcesses(IEnumerable<CoreProcessIdentity> processes) =>
        Volatile.Write(ref _trackedCoreProcesses, processes.DistinctBy(process => process.ProcessId).ToArray());

    private async Task StopCoreAndConfirmAsync(CancellationToken cancellationToken)
    {
        await CoreManager.Instance.CoreStop();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activeProcesses = GetActiveCoreProcesses();
            // Port occupancy is not process ownership: another program may bind a just-released
            // TCP or UDP port before this check. Only Web's captured process identities can
            // keep stop pending or cause a stop failure.
            if (activeProcesses.Length == 0)
            {
                SetTrackedCoreProcesses([]);
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                var details = new List<string>();
                if (activeProcesses.Length > 0)
                {
                    details.Add($"Core processes remain: {string.Join(",", activeProcesses.Select(process => process.ProcessId))}.");
                }
                throw new TimeoutException(string.Join(" ", details));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private async Task MonitorCoreRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                var snapshot = CurrentCoreRuntime;
                if (snapshot.State != CoreRuntimeState.Running || snapshot.ProxyPort is not int port)
                {
                    continue;
                }

                var processIds = GetActiveCoreProcessIds();
                var listening = await IsListeningAsync(port, cancellationToken);
                if (!ReferenceEquals(snapshot, CurrentCoreRuntime) || CurrentCoreRuntime.State != CoreRuntimeState.Running)
                {
                    continue;
                }
                if (processIds.Length > 0 && listening)
                {
                    continue;
                }

                var failure = processIds.Length == 0
                    ? "The tracked Core child process exited unexpectedly."
                    : "The running Core no longer owns its expected proxy listener.";
                SetCoreRuntime(snapshot with
                {
                    State = CoreRuntimeState.Faulted,
                    ProcessIds = processIds,
                    LastFailure = failure,
                });
                AddLog("core", failure);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the Core is stopped, restarted, or the Web host shuts down.
        }
        catch (Exception exception)
        {
            AddLog("core", $"Core runtime monitor failed: {exception}");
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken, bool suppressCoreAutostart = false)
    {
        var restoreState = await LoadRestoreRuntimeStateAsync(cancellationToken);
        var updateStatePath = WebUpdateRuntimeStatePath;
        var updateRuntimeState = restoreState is null
            ? await LoadRuntimeRestartIntentAsync(updateStatePath, cancellationToken)
            : null;
        var startupRuntimeIntent = restoreState ?? updateRuntimeState;
        if (!AppManager.Instance.InitApp())
        {
            throw new InvalidOperationException("ServiceLib could not load its configuration.");
        }
        ConfigureServiceLibConsoleLogging();

        AppManager.Instance.WindowDialog = new HeadlessWindowDialog();
        if (!AppManager.Instance.InitComponents())
        {
            throw new InvalidOperationException("ServiceLib component initialization failed.");
        }

        await _mutations.RunAsync(async () =>
        {
            EnsureInboundDefaults(Config);
            EnsureCoreTypeMappings();
            var proxyPortOverride = _configuration.GetValue<int?>("V2RAYN_WEB_PROXY_PORT");
            if (proxyPortOverride is <= 0 or > 65535)
            {
                throw new InvalidOperationException("V2RAYN_WEB_PROXY_PORT must be a valid TCP/UDP port (1-65535).");
            }
            if (proxyPortOverride is int configuredProxyPort)
            {
                Config.Inbound[0].LocalPort = configuredProxyPort;
            }
            if (_configuration.GetValue<bool?>("V2RAYN_WEB_PROXY_LISTEN_ALL") is bool allowProxyFromLan)
            {
                Config.Inbound[0].AllowLANConn = allowProxyFromLan;
            }
            await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
        });
        await ConfigHandler.InitBuiltinDNS(Config);
        await BuiltinDnsProfileBootstrap.EnsureAsync(
            Config,
            async () => await AppManager.Instance.DNSItems() ?? [],
            ConfigHandler.SaveDNSItems);
        await ConfigHandler.InitBuiltinFullConfigTemplate(Config);
        // Keep startup aligned with Desktop's StatusBarViewModel. In particular,
        // InitBuiltinRouting owns the RoutingIndexId -> IsActive migration and must
        // run even when a restored Desktop database already has routing rows.
        await ConfigHandler.InitBuiltinRouting(Config);
        _ = await ConfigHandler.GetDefaultRouting(Config);
        var restoredSubscriptions = await AppManager.Instance.SubItems() ?? [];
        var normalizedSubIndexId = NormalizeSelectedSubscriptionId(
            Config.SubIndexId,
            restoredSubscriptions.Select(item => item.Id).ToArray());
        if (Config.SubIndexId != normalizedSubIndexId)
        {
            AddLog("restore", "The selected subscription group no longer exists; normalized the selection to all profiles.");
            Config.SubIndexId = normalizedSubIndexId;
        }
        var selectedProfileId = Config.IndexId;
        var defaultProfile = await ConfigHandler.GetDefaultServer(Config);
        await _mutations.RunAsync(() => EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config)));
        await ProfileExManager.Instance.Init();
        await CertPemManager.Instance.Init(Config);
        // The Web frontend owns its runtime and rejects generated launch contexts that enable TUN.
        await CoreManager.Instance.Init(Config, OnCoreMessageAsync);
        if (Config.GuiItem.EnableStatistics || Config.GuiItem.DisplayRealTimeSpeed)
        {
            await StatisticsManager.Instance.Init(Config, OnStatisticsUpdateAsync);
        }
        _xrayPath = FindXrayExecutable(out var missingXrayMessage);
        if (_xrayPath is null)
        {
            AddLog("core", missingXrayMessage);
        }

        _initialized = true;
        AddLog("web", "serviceLib.initialized");
        StartScheduledOperations(cancellationToken);

        if (startupRuntimeIntent is not null)
        {
            if (!RuntimeRestartRecovery.ShouldRecoverCore(startupRuntimeIntent, suppressCoreAutostart))
            {
                if (suppressCoreAutostart && startupRuntimeIntent.WasRunning)
                {
                    AddLog("core", "Core startup was suppressed until first-run Management Key setup is complete.");
                }
            }
            else
            {
                await RuntimeRestartRecovery.RecoverAsync(
                    startupRuntimeIntent,
                    selectedProfileId,
                    defaultProfile?.IndexId,
                    async (profileId, _) => await AppManager.Instance.GetProfileItem(profileId) is not null,
                    StartCoreAsync,
                    message => AddLog(restoreState is not null ? "restore" : "update", message),
                    cancellationToken);
            }
            if (restoreState is not null)
            {
                await CommitRestoreRuntimeStateAsync();
            }
        }
        else if (RuntimeRestartRecovery.ShouldAutoStart(
            startupRuntimeIntent,
            _configuration.GetValue("V2RAYN_WEB_AUTOSTART", false),
            suppressCoreAutostart))
        {
            var result = await StartCoreAsync(null, cancellationToken);
            if (!result.Success)
            {
                AddLog("core", result.MessageKey);
            }
        }
        else if (suppressCoreAutostart)
        {
            AddLog("core", "Core autostart was suppressed until first-run Management Key setup is complete.");
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        var steps = new List<ShutdownCleanupStep>
        {
            new("restore shutdown request", async deadline =>
            {
                await WaitForRestoreShutdownRequestAsync(deadline.Token);
                return true;
            }),
            new("scheduled operations stop", deadline => StopScheduledOperationsAsync(deadline.Token)),
            new("operation drain", async deadline =>
            {
                var drained = await _operations.StopAndDrainAsync(deadline.Token, deadline.Remaining);
                if (!drained)
                {
                    AddLog("web", "Shutdown operation drain timed out; skipped all Core, state-save, and SQLite cleanup.");
                }
                return drained;
            }),
        };

        var saveServiceLibState = _initialized && !_restoring;
        if (saveServiceLibState)
        {
            steps.Add(new("Core stop", async deadline =>
            {
                try
                {
                    await StopCoreMonitorAsync();
                    await StopCoreAndConfirmAsync(deadline.Token);
                    SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
                    return true;
                }
                catch (Exception exception)
                {
                    SetCoreRuntime(CurrentCoreRuntime with
                    {
                        State = CoreRuntimeState.Faulted,
                        ProcessIds = GetActiveCoreProcessIds(),
                        LastFailure = exception.Message,
                    });
                    throw;
                }
            }));
            steps.Add(new("profile save", async _ =>
            {
                await ProfileExManager.Instance.SaveTo();
                return true;
            }));
            steps.Add(new("statistics save", async _ =>
            {
                await StatisticsManager.Instance.SaveTo();
                return true;
            }));
            steps.Add(new("statistics close", _ =>
            {
                StatisticsManager.Instance.Close();
                return Task.FromResult(true);
            }));
            steps.Add(new("configuration save", async deadline =>
            {
                await _mutations.RunAsync(
                    () => EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config)),
                    deadline.Token);
                return true;
            }));
            steps.Add(new("database close", async _ =>
            {
                await SQLiteHelper.Instance.DisposeDbConnectionAsync();
                return true;
            }));
        }

        var completed = await ShutdownCleanupSequence.RunAsync(
            steps,
            RuntimeShutdownBudgets.RuntimeCleanup,
            cancellationToken,
            message => AddLog("web", message),
            ShutdownDiagnostics.SetStage);
        if (completed && saveServiceLibState)
        {
            AddLog("web", "serviceLib.stopped");
        }
    }

    internal static async Task EnsureConfigSaveSucceededAsync(Func<Task<int>> save)
    {
        if (await save() != 0)
        {
            throw new IOException("ServiceLib could not persist the configuration.");
        }
    }

    public async Task<IReadOnlyList<SubscriptionView>> GetSubscriptionsAsync()
    {
        var items = await AppManager.Instance.SubItems() ?? [];
        return items.Select(ToSubscriptionView).ToArray();
    }

    public async Task<SubscriptionMutationResult> AddSubscriptionAsync(SubscriptionInput input)
    {
        if (!TryValidateSubscription(input, out var code, out var messageKey))
        {
            return new(false, code, messageKey, null);
        }

        var item = ToSubItem(input, null);
        var result = await _mutations.RunAsync(() => ConfigHandler.AddSubItem(Config, item));
        if (result != 0)
        {
            return new(false, "subscription_save_failed", ApiMessageKeys.SubscriptionSaveFailed, null);
        }

        var saved = await AppManager.Instance.GetSubItem(item.Id);
        return saved is null
            ? new(false, "subscription_save_failed", ApiMessageKeys.SubscriptionSaveFailed, null)
            : new(true, "ok", ApiMessageKeys.SubscriptionAdded, ToSubscriptionView(saved));
    }

    public async Task<SubscriptionMutationResult> UpdateSubscriptionAsync(string id, SubscriptionInput input)
    {
        if (!TryValidateSubscription(input, out var code, out var messageKey))
        {
            return new(false, code, messageKey, null);
        }

        return await _mutations.RunAsync(async () =>
        {
            var existing = await AppManager.Instance.GetSubItem(id);
            if (existing is null)
            {
                return new SubscriptionMutationResult(false, "subscription_not_found", ApiMessageKeys.SubscriptionNotFound, null);
            }

            var item = ToSubItem(input, existing);
            item.Id = id;
            var result = await ConfigHandler.AddSubItem(Config, item);
            if (result != 0)
            {
                return new SubscriptionMutationResult(false, "subscription_save_failed", ApiMessageKeys.SubscriptionSaveFailed, null);
            }

            var saved = await AppManager.Instance.GetSubItem(id);
            return saved is not null
                ? new SubscriptionMutationResult(true, "ok", ApiMessageKeys.SubscriptionSaved, ToSubscriptionView(saved))
                : new SubscriptionMutationResult(false, "subscription_save_failed", ApiMessageKeys.SubscriptionSaveFailed, null);
        });
    }

    public async Task<OperationView> DeleteSubscriptionAsync(string id)
    {
        var subscription = await AppManager.Instance.GetSubItem(id);
        if (subscription is null)
        {
            return OperationView.Fail("subscription_not_found", ApiMessageKeys.SubscriptionNotFound);
        }

        var selectedProfile = await AppManager.Instance.GetProfileItem(Config.IndexId);
        var removesCurrentProfile = selectedProfile?.Subid == id;
        if (removesCurrentProfile)
        {
            await StopCoreAsync(CancellationToken.None);
        }

        await _mutations.RunAsync(async () =>
        {
            if (await ConfigHandler.DeleteSubItem(Config, id) != 0)
            {
                throw new IOException("ServiceLib could not delete the subscription.");
            }
            if (removesCurrentProfile)
            {
                _ = await ConfigHandler.GetDefaultServer(Config);
                await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
            }
        });

        AddLog("subscription", ApiMessageKeys.SubscriptionDeleted);
        return OperationView.Ok(ApiMessageKeys.SubscriptionDeleted);
    }

    public bool StartSubscriptionUpdate(string id, bool useProxy)
    {
        return StartSubscriptionUpdateTask(id, useProxy, _operations.ShutdownToken) is not null;
    }

    private Task? StartSubscriptionUpdateTask(string id, bool useProxy, CancellationToken operationToken)
    {
        lock (_subscriptionGate)
        {
            var hasConflictingUpdate = _subscriptionTasks.Any(pair =>
                !pair.Value.IsCompleted
                && (pair.Key.Length == 0 || id.Length == 0 || pair.Key == id));
            if (hasConflictingUpdate)
            {
                return null;
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    await using var operation = await _operations.EnterOperationAsync(operationToken);
                    await _mutations.RunAsync(async () =>
                    {
                        await SubscriptionHandler.UpdateProcess(Config, id, useProxy, (success, message) =>
                        {
                            var payload = new
                            {
                                subscriptionId = id,
                                success,
                                code = success ? "ok" : "subscription_update_progress",
                                messageKey = success ? ApiMessageKeys.SubscriptionSaved : ApiMessageKeys.SubscriptionUpdateProgress,
                                rawLog = message,
                            };
                            _events.Publish("subscription-progress", payload);
                            AddLog("subscription", message);
                            return Task.CompletedTask;
                        });

                        operation.Token.ThrowIfCancellationRequested();
                        await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
                        await UpdateSubscriptionTimestampLockedAsync(id);
                    }, operation.Token);
                    _events.Publish("profiles-changed", new { subscriptionId = id });
                }
                catch (OperationCanceledException) when (_operations.IsStopping || operationToken.IsCancellationRequested)
                {
                    // Expected during graceful service shutdown.
                }
                catch (Exception ex)
                {
                    AddLog("subscription", ex.Message);
                }
                finally
                {
                    lock (_subscriptionGate)
                    {
                        _subscriptionTasks.Remove(id);
                    }
                }
            });

            _subscriptionTasks[id] = task;
            return task;
        }
    }

    public async Task<IReadOnlyList<ProfileView>> GetProfilesAsync(string? subscriptionId, string? filter)
    {
        var groupId = subscriptionId ?? Config.SubIndexId ?? string.Empty;
        var profiles = await AppManager.Instance.ProfileItems(groupId) ?? [];
        var profileModels = await AppManager.Instance.ProfileModels(groupId, string.Empty) ?? [];
        var modelMap = profileModels.ToDictionary(item => item.IndexId);
        var subscriptions = (await AppManager.Instance.SubItems() ?? []).ToDictionary(item => item.Id, item => item.Remarks);
        var extensions = await ProfileExManager.Instance.GetProfileExs();
        var extensionMap = extensions.ToDictionary(item => item.IndexId);
        var statistics = (Config.GuiItem.EnableStatistics ? StatisticsManager.Instance.ServerStat : null) ?? [];
        var statisticsMap = statistics.ToDictionary(item => item.IndexId);
        var query = filter?.Trim();

        return profiles
            .Where(item => string.IsNullOrEmpty(query)
                || Utils.IsRegexMatch(item.Remarks, query)
                || Utils.IsRegexMatch(item.Address, query))
            .Select(item =>
            {
                extensionMap.TryGetValue(item.IndexId, out var extension);
                modelMap.TryGetValue(item.IndexId, out var model);
                statisticsMap.TryGetValue(item.IndexId, out var statistic);
                var delay = extension?.Delay ?? 0;
                var speed = extension?.Speed ?? 0;
                return new ProfileView(
                    item.IndexId,
                    item.Remarks,
                    System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(item.ConfigType.ToString()),
                    item.Address,
                    item.Port,
                    item.GetNetwork(),
                    item.StreamSecurity,
                    item.Subid,
                    model?.SubRemarks ?? subscriptions.GetValueOrDefault(item.Subid),
                    System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(AppManager.Instance.GetCoreType(item, item.ConfigType).ToString()),
                    extension?.Sort ?? 0,
                    delay,
                    speed,
                    extension?.IpInfo,
                    statistic?.TodayUp ?? 0,
                    statistic?.TodayDown ?? 0,
                    statistic?.TotalUp ?? 0,
                    statistic?.TotalDown ?? 0,
                    item.IndexId == Config.IndexId,
                    !item.IsComplex() && item.Port > 0);
            })
            .OrderBy(item => item.Sort)
            .ToArray();
    }

    public async Task<ProfileItem?> GetProfileDetailsAsync(string profileId) =>
        await AppManager.Instance.GetProfileItem(profileId);

    public async Task<StatusView> GetStatusAsync()
    {
        var selectedProfile = await AppManager.Instance.GetProfileItem(Config.IndexId);
        var profileItems = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var subscriptions = await AppManager.Instance.SubItems() ?? [];
        var snapshot = CurrentCoreRuntime;
        var processIds = GetActiveCoreProcessIds();
        var listenerDefinitions = snapshot.ProxyPort.HasValue && snapshot.State != CoreRuntimeState.Stopped
            ? snapshot.Listeners
            : GetConfiguredListenerSnapshots();
        var listeners = new List<ListenerView>(listenerDefinitions.Length);
        foreach (var listener in listenerDefinitions)
        {
            listeners.Add(new ListenerView(
                listener.Name,
                listener.Protocols,
                listener.ListenAddress,
                listener.Port,
                await IsListeningAsync(listener.Port, CancellationToken.None)));
        }

        var mainListener = listeners.FirstOrDefault(listener => listener.Name == "local");
        var listening = mainListener?.Listening == true;
        var inbound = Config.Inbound.FirstOrDefault();
        var sniffingEnabled = inbound?.SniffingEnabled ?? false;
        var sniffingTypes = NormalizeDestOverride(sniffingEnabled, inbound?.DestOverride);
        var coreRunning = snapshot.State == CoreRuntimeState.Running && processIds.Length > 0 && listening;
        if (snapshot.State == CoreRuntimeState.Running && !coreRunning)
        {
            snapshot = snapshot with
            {
                State = CoreRuntimeState.Faulted,
                ProcessIds = processIds,
                LastFailure = processIds.Length == 0
                    ? "The tracked Core child process exited unexpectedly."
                    : "The running Core no longer owns its expected proxy listener.",
            };
            SetCoreRuntime(snapshot);
        }

        var runningProfile = string.IsNullOrEmpty(snapshot.ProfileId)
            ? null
            : await AppManager.Instance.GetProfileItem(snapshot.ProfileId);
        var configuredProxyPort = Config.Inbound.FirstOrDefault()?.LocalPort;
        return new StatusView(
            coreRunning,
            snapshot.CoreType is { } coreType
                ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(coreType.ToString())
                : null,
            selectedProfile?.IndexId,
            selectedProfile?.Remarks,
            snapshot.StartedAt,
            listeners.ToArray(),
            !string.IsNullOrEmpty(_xrayPath),
            Utils.GetRuntimeInfo(),
            profileItems.Count,
            subscriptions.Count,
            DateTimeOffset.UtcNow,
            Config.GuiItem.EnableStatistics,
            _latestTraffic is null ? null : new TrafficView(
                _latestTraffic.ProxyUp,
                _latestTraffic.ProxyDown,
                _latestTraffic.DirectUp,
                _latestTraffic.DirectDown),
            snapshot.State.ToString().ToLowerInvariant(),
            configuredProxyPort,
            snapshot.State == CoreRuntimeState.Stopped ? null : snapshot.ProxyPort,
            snapshot.ProfileId,
            runningProfile?.Remarks,
            snapshot.ApiPort,
            processIds,
            snapshot.LastFailure,
            WebBuildIdentity.Current.Version,
            WebBuildIdentity.Current.Commit,
            WebBuildIdentity.Current.Rid,
            WebBuildIdentity.Current.BuildDate,
            sniffingEnabled,
            sniffingTypes,
            WebApiCapabilities.Current);

    }

    public async Task<OperationView> SelectProfileAsync(string profileId, CancellationToken cancellationToken)
    {
        var profile = await AppManager.Instance.GetProfileItem(profileId);
        if (profile is null)
        {
            return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
        }

        return await StartCoreAsync(profile, cancellationToken, selectProfile: true);
    }

    public async Task<OperationView> StartCoreAsync(string? profileId, CancellationToken cancellationToken)
    {
        ProfileItem? profile = null;
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            profile = await AppManager.Instance.GetProfileItem(profileId);
            if (profile is null)
            {
                return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound);
            }
        }

        return await StartCoreAsync(profile, cancellationToken);
    }

    private async Task<OperationView> StartCoreAsync(ProfileItem? requestedProfile, CancellationToken cancellationToken, bool selectProfile = false)
    {
        await _coreGate.WaitAsync(cancellationToken);
        try
        {
            return await StartCoreLockedAsync(requestedProfile, cancellationToken, selectProfile);
        }
        finally
        {
            _coreGate.Release();
        }
    }

    private async Task<OperationView> StartCoreLockedAsync(ProfileItem? requestedProfile, CancellationToken cancellationToken, bool selectProfile = false)
    {
        var profile = requestedProfile ?? await GetDefaultProfileAsync();
        if (profile is null)
        {
            return OperationView.Fail("profile_not_selected", ApiMessageKeys.ProfileNoneSelected);
        }

        var current = CurrentCoreRuntime;
        if (!selectProfile
            && current.State == CoreRuntimeState.Running
            && current.ProfileId == profile.IndexId
            && HasTrackedCoreProcesses)
        {
            return OperationView.Ok(ApiMessageKeys.CoreStarted, new { profileId = profile.IndexId, alreadyRunning = true });
        }

        var preflight = await BuildAndValidateCoreLaunchAsync(profile);
        if (preflight.Failure is { } preflightFailure)
        {
            return preflightFailure;
        }

        if (selectProfile)
        {
            await _mutations.RunAsync(async () =>
            {
                Config.IndexId = profile.IndexId;
                await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
            });
        }

        return await LaunchPreflightedCoreLockedAsync(preflight, cancellationToken);
    }

    private async Task<ProfileItem?> GetDefaultProfileAsync() => await _mutations.RunAsync(async () =>
    {
        var selected = await ConfigHandler.GetDefaultServer(Config);
        if (selected is not null)
        {
            await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
        }
        return selected;
    });

    private async Task<CoreLaunchPreflightResult> BuildAndValidateCoreLaunchAsync(ProfileItem profile)
    {
        var preflight = await CoreLaunchPreflight.BuildAndValidateAsync(Config, profile, coreType =>
        {
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
            var executable = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var missingCoreMessage);
            if (!string.IsNullOrEmpty(executable))
            {
                return null;
            }

            AddLog("core", missingCoreMessage);
            return OperationView.Fail("core_binary_missing", ApiMessageKeys.CoreBinaryMissing,
                new { coreType = coreType.ToString() });
        });

        if (preflight.Failure?.Code == "profile_validation_failed")
        {
            foreach (var message in preflight.BuiltContext.CombinedValidatorResult.Errors
                         .Concat(preflight.BuiltContext.CombinedValidatorResult.Warnings))
            {
                AddLog("core-validation", message);
            }
        }

        if (preflight.Success)
        {
            RuntimeListenerSnapshot[] currentlyOwnedListeners = HasTrackedCoreProcesses
                ? CurrentCoreRuntime.Listeners
                : [];
            foreach (var listener in GetConfiguredListenerSnapshots())
            {
                if (await IsProjectedListenerUnavailableAsync(listener, currentlyOwnedListeners, CancellationToken.None))
                {
                    return new CoreLaunchPreflightResult(
                        preflight.Profile,
                        preflight.BuiltContext,
                        OperationView.Fail("proxy_port_in_use", ApiMessageKeys.CorePortInUse,
                            new { port = listener.Port, listener = listener.Name }));
                }
            }
        }

        return preflight;
    }

    private async Task<OperationView> LaunchPreflightedCoreLockedAsync(
        CoreLaunchPreflightResult preflight,
        CancellationToken cancellationToken)
    {
        var profile = preflight.Profile;
        var built = preflight.BuiltContext;
        var port = Config.Inbound.FirstOrDefault()?.LocalPort ?? 0;
        if (!HasTrackedCoreProcesses && CurrentCoreRuntime.State == CoreRuntimeState.Stopped
            && await IsListeningAsync(port, cancellationToken))
        {
            return OperationView.Fail("proxy_port_in_use", ApiMessageKeys.CorePortInUse, new { port });
        }

        if (CurrentCoreRuntime.State != CoreRuntimeState.Stopped || HasTrackedCoreProcesses)
        {
            SetCoreRuntime(CurrentCoreRuntime with { State = CoreRuntimeState.Stopping, LastFailure = null });
            try
            {
                await StopCoreMonitorAsync();
                await StopCoreAndConfirmAsync(cancellationToken);
                SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
            }
            catch (Exception exception)
            {
                var failedStop = CurrentCoreRuntime with
                {
                    State = CoreRuntimeState.Faulted,
                    ProcessIds = GetActiveCoreProcessIds(),
                    LastFailure = exception.Message,
                };
                SetCoreRuntime(failedStop);
                AddLog("core", $"Core stop before launch failed: {exception}");
                return OperationView.Fail("core_stop_failed", ApiMessageKeys.CoreStopFailed,
                    new { detail = exception.Message, processIds = failedStop.ProcessIds });
            }
        }

        var expectedCoreType = built.PreSocksResult?.Context.RunCoreType ?? built.MainResult.Context.RunCoreType;
        var listeners = GetConfiguredListenerSnapshots();
        SetCoreRuntime(new CoreRuntimeSnapshot(
            CoreRuntimeState.Starting,
            profile.IndexId,
            expectedCoreType,
            null,
            listeners.FirstOrDefault(listener => listener.Name == "local")?.Port,
            AppManager.Instance.StatePort,
            listeners,
            [],
            null));

        try
        {
            var existingProcessIds = CoreProcessTracker.CaptureExistingProcessIds();
            var expectedExecutablePaths = GetExpectedCoreExecutablePaths(built);
            var expectedConfigPaths = GetExpectedCoreConfigPaths(built);
            try
            {
                await CoreManager.Instance.LoadCore(built.MainResult.Context, built.PreSocksResult?.Context);
            }
            finally
            {
                SetTrackedCoreProcesses(CoreProcessTracker.CaptureStartedProcesses(
                    existingProcessIds,
                    expectedExecutablePaths,
                    expectedConfigPaths));
            }
            var started = await WaitForListenerAsync(port, cancellationToken);
            var processIds = GetActiveCoreProcessIds();
            if (!started || processIds.Length == 0)
            {
                var failure = !started
                    ? "The Core process did not open its configured proxy listener."
                    : "The Web runtime could not identify a live Core child process.";
                try
                {
                    await StopCoreAndConfirmAsync(CancellationToken.None);
                    processIds = GetActiveCoreProcessIds();
                }
                catch (Exception stopException)
                {
                    failure += $" Cleanup also failed: {stopException.Message}";
                }
                SetCoreRuntime(CurrentCoreRuntime with
                {
                    State = CoreRuntimeState.Faulted,
                    ProcessIds = processIds,
                    LastFailure = failure,
                });
                AddLog("core", failure);
                _events.Publish("status", await GetStatusAsync());
                return OperationView.Fail("core_start_failed", ApiMessageKeys.CoreStartFailed,
                    new { profileId = profile.IndexId, detail = failure, processIds });
            }

            SetCoreRuntime(CurrentCoreRuntime with
            {
                State = CoreRuntimeState.Running,
                CoreType = AppManager.Instance.RunningCoreType,
                StartedAt = DateTimeOffset.UtcNow,
                ProcessIds = processIds,
                LastFailure = null,
            });
            await StartCoreMonitorAsync();
            AddLog("core", ApiMessageKeys.CoreStarted);
            _events.Publish("status", await GetStatusAsync());
            return OperationView.Ok(ApiMessageKeys.CoreStarted, new { profileId = profile.IndexId, processIds });
        }
        catch (Exception exception)
        {
            AddLog("core", $"Core start failed: {exception}");
            var processIds = GetActiveCoreProcessIds();
            if (CurrentCoreRuntime.State == CoreRuntimeState.Starting || processIds.Length > 0)
            {
                try
                {
                    await StopCoreAndConfirmAsync(CancellationToken.None);
                    processIds = GetActiveCoreProcessIds();
                }
                catch (Exception stopException)
                {
                    AddLog("core", $"Core start cleanup failed: {stopException}");
                }
            }
            SetCoreRuntime(CurrentCoreRuntime with
            {
                State = CoreRuntimeState.Faulted,
                ProcessIds = processIds,
                LastFailure = exception.Message,
            });
            _events.Publish("status", await GetStatusAsync());
            return OperationView.Fail("core_start_failed", ApiMessageKeys.CoreStartFailed,
                new { profileId = profile.IndexId, detail = exception.Message, processIds });
        }
    }

    internal static OperationView? GetTunLaunchRejection(CoreConfigContextBuilderAllResult built) =>
        CoreLaunchPreflight.GetTunLaunchRejection(built);

    internal static string NormalizeSelectedSubscriptionId(string? selectedId, IReadOnlyCollection<string> availableIds) =>
        string.IsNullOrEmpty(selectedId) || availableIds.Contains(selectedId, StringComparer.Ordinal)
            ? selectedId ?? string.Empty
            : string.Empty;

    public async Task<OperationView> StopCoreAsync(CancellationToken cancellationToken)
    {
        await _coreGate.WaitAsync(cancellationToken);
        try
        {
            return await StopCoreLockedAsync(cancellationToken);
        }
        finally
        {
            _coreGate.Release();
        }
    }

    private async Task<OperationView> StopCoreLockedAsync(CancellationToken cancellationToken)
    {
        if (CurrentCoreRuntime.State == CoreRuntimeState.Stopped && !HasTrackedCoreProcesses)
        {
            return OperationView.Ok(ApiMessageKeys.CoreStopped);
        }

        SetCoreRuntime(CurrentCoreRuntime with { State = CoreRuntimeState.Stopping, LastFailure = null });
        try
        {
            await StopCoreMonitorAsync();
            await StopCoreAndConfirmAsync(cancellationToken);
            SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
            AddLog("core", ApiMessageKeys.CoreStopped);
            _events.Publish("status", await GetStatusAsync());
            return OperationView.Ok(ApiMessageKeys.CoreStopped);
        }
        catch (Exception exception)
        {
            var processIds = GetActiveCoreProcessIds();
            SetCoreRuntime(CurrentCoreRuntime with
            {
                State = CoreRuntimeState.Faulted,
                ProcessIds = processIds,
                LastFailure = exception.Message,
            });
            AddLog("core", $"Core stop failed: {exception}");
            _events.Publish("status", await GetStatusAsync());
            return OperationView.Fail("core_stop_failed", ApiMessageKeys.CoreStopFailed,
                new { detail = exception.Message, processIds });
        }
    }

    public async Task<OperationView> RestartCoreAsync(CancellationToken cancellationToken)
    {
        await _coreGate.WaitAsync(cancellationToken);
        try
        {
            return await RestartCoreLockedAsync(cancellationToken);
        }
        finally
        {
            _coreGate.Release();
        }
    }

    private async Task<OperationView> RestartCoreLockedAsync(CancellationToken cancellationToken)
    {
        var previous = CurrentCoreRuntime;
            if (previous.State is not (CoreRuntimeState.Running or CoreRuntimeState.Faulted)
                || string.IsNullOrEmpty(previous.ProfileId))
            {
                return OperationView.Fail("core_not_running", ApiMessageKeys.CoreNotRunning,
                    new { runtimeState = previous.State.ToString(), processIds = GetActiveCoreProcessIds() });
            }

            var profile = await AppManager.Instance.GetProfileItem(previous.ProfileId);
            if (profile is null)
            {
                return OperationView.Fail("profile_not_found", ApiMessageKeys.ProfileNotFound,
                    new { profileId = previous.ProfileId });
            }

            AppManager.Instance.Reset();
            var restartStage = "preflight";
            try
            {
                return await CoreRestartFlow.ExecuteAsync(
                    () => BuildAndValidateCoreLaunchAsync(profile),
                    async () =>
                    {
                        restartStage = "stop";
                        SetCoreRuntime(previous with { State = CoreRuntimeState.Restarting, LastFailure = null });
                        await StopCoreMonitorAsync();
                        await StopCoreAndConfirmAsync(cancellationToken);
                    },
                    () =>
                    {
                        SetCoreRuntime(CoreRuntimeSnapshot.Stopped);
                        restartStage = "start";
                    },
                    preflight => LaunchPreflightedCoreLockedAsync(preflight, cancellationToken));
            }
            catch (Exception exception)
            {
                var processIds = GetActiveCoreProcessIds();
                if (restartStage != "preflight")
                {
                    SetCoreRuntime(CurrentCoreRuntime with
                    {
                        State = CoreRuntimeState.Faulted,
                        ProcessIds = processIds,
                        LastFailure = exception.Message,
                    });
                }
                AddLog("core", $"Core restart failed during {restartStage}: {exception}");
                return restartStage == "stop"
                    ? OperationView.Fail("core_stop_failed", ApiMessageKeys.CoreStopFailed,
                        new { detail = exception.Message, processIds })
                    : OperationView.Fail("core_restart_failed", ApiMessageKeys.CoreStartFailed,
                        new { stage = restartStage, detail = exception.Message, processIds });
            }
    }

    public async Task<OperationView> StartLatencyTestAsync(string profileId)
    {
        var profile = await AppManager.Instance.GetProfileItem(profileId);
        if (profile is null || profile.IsComplex() || profile.Port <= 0)
        {
            return OperationView.Fail("speedtest_invalid_profile", ApiMessageKeys.SpeedTestInvalidProfile);
        }

        return await StartSpeedTestAsync(new SpeedTestRequest(ESpeedActionType.Tcping, [profileId]), CancellationToken.None);
    }

    public OperationView StopLatencyTests()
    {
        _speedtestService?.ExitLoop();
        return OperationView.Ok(ApiMessageKeys.CommonCompleted);
    }

    public IReadOnlyList<LogView> GetRecentLogs(int limit, string? filter = null) => _logs.Recent(limit, filter);

    public LogPageView GetRecentLogsPage(int page, int pageSize, string? filter = null) => _logs.RecentPage(page, pageSize, filter);

    public long ClearLogs()
    {
        var generation = _logs.Clear();
        _events.Publish("logs-cleared", new { generation });
        return generation;
    }

    private async Task OnCoreMessageAsync(bool notify, string message)
    {
        AddLog("core", message);
        await Task.CompletedTask;
    }

    private void AddLog(string source, string message)
    {
        var entry = _logs.Add(source, message);
        _events.Publish("log", entry);
        Console.Out.WriteLine($"[{source}] {message.TrimEnd()}");
    }

    private static void ConfigureServiceLibConsoleLogging()
    {
        var logging = LogManager.Configuration;
        if (logging is null)
        {
            return;
        }

        var target = new ConsoleTarget("web-console")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}",
        };
        logging.AddTarget(target);
        logging.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Debug, target));
        LogManager.Configuration = logging;
    }

    private Task OnStatisticsUpdateAsync(ServerSpeedItem update)
    {
        _latestTraffic = update;
        _events.Publish("traffic", update);
        return Task.CompletedTask;
    }

    // The subscription update owns the mutation gate while calling this helper.
    private async Task UpdateSubscriptionTimestampLockedAsync(string subscriptionId)
    {
        var updateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (subscriptionId.Length > 0)
        {
            var item = await AppManager.Instance.GetSubItem(subscriptionId);
            if (item is not null)
            {
                item.UpdateTime = updateTime;
                if (await ConfigHandler.AddSubItem(Config, item) != 0)
                {
                    throw new IOException("ServiceLib could not save the subscription timestamp.");
                }
            }
            return;
        }

        foreach (var item in await AppManager.Instance.SubItems() ?? [])
        {
            if (item.Enabled)
            {
                item.UpdateTime = updateTime;
                if (await ConfigHandler.AddSubItem(Config, item) != 0)
                {
                    throw new IOException("ServiceLib could not save the subscription timestamp.");
                }
            }
        }
    }

    internal static bool TryValidateSubscription(SubscriptionInput input, out string code, out string messageKey)
    {
        if (string.IsNullOrWhiteSpace(input.Remarks))
        {
            code = "subscription_name_required";
            messageKey = ApiMessageKeys.SubscriptionNameRequired;
            return false;
        }
        if (!string.IsNullOrWhiteSpace(input.Url)
            && (!Uri.TryCreate(input.Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
        {
            code = "subscription_url_invalid";
            messageKey = ApiMessageKeys.SubscriptionInvalidUrl;
            return false;
        }
        if (!HttpRequestHeadersHelper.TryParse(input.RequestHeaders, out _))
        {
            code = "subscription_headers_invalid";
            messageKey = ApiMessageKeys.CommonInvalidInput;
            return false;
        }

        code = "ok";
        messageKey = string.Empty;
        return true;
    }

    internal static SubItem ToSubItem(SubscriptionInput input, SubItem? existing) => new()
    {
        Id = existing?.Id ?? string.Empty,
        Remarks = input.Remarks.Trim(),
        Url = input.Url?.Trim() ?? string.Empty,
        MoreUrl = input.MoreUrl?.Trim() ?? existing?.MoreUrl ?? string.Empty,
        Enabled = input.Enabled ?? existing?.Enabled ?? true,
        UserAgent = input.UserAgent?.Trim() ?? existing?.UserAgent ?? string.Empty,
        RequestHeaders = input.RequestHeaders ?? existing?.RequestHeaders,
        Filter = input.Filter ?? existing?.Filter,
        AutoUpdateInterval = Math.Max(input.AutoUpdateInterval ?? existing?.AutoUpdateInterval ?? 0, 0),
        ConvertTarget = input.ConvertTarget ?? existing?.ConvertTarget,
        Memo = input.Memo ?? existing?.Memo,
        Sort = input.Sort ?? existing?.Sort ?? 0,
        UpdateTime = existing?.UpdateTime ?? 0,
        PrevProfile = input.PrevProfile ?? existing?.PrevProfile,
        NextProfile = input.NextProfile ?? existing?.NextProfile,
        PreSocksPort = input.PreSocksPort,
        CustomCoreType = input.CustomCoreType,
    };

    internal static SubscriptionView ToSubscriptionView(SubItem item) => new(
        item.Id,
        item.Remarks,
        item.Url,
        item.Enabled,
        item.MoreUrl,
        item.Filter,
        item.AutoUpdateInterval,
        item.UpdateTime,
        item.UserAgent,
        item.RequestHeaders,
        item.ConvertTarget,
        item.Memo,
        item.Sort,
        item.PrevProfile,
        item.NextProfile,
        item.PreSocksPort,
        item.CustomCoreType is null ? null : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(item.CustomCoreType.Value.ToString()));

    private static void EnsureInboundDefaults(Config config)
    {
        if (config.Inbound.Count == 0)
        {
            config.Inbound.Add(new InItem
            {
                Protocol = nameof(EInboundProtocol.socks),
                LocalPort = 10808,
                UdpEnabled = true,
                SniffingEnabled = true,
            });
        }

        if (config.Inbound[0].LocalPort is <= 0 or > 65535)
        {
            config.Inbound[0].LocalPort = 10808;
        }
    }

    private RuntimeListenerSnapshot[] GetConfiguredListenerSnapshots()
    {
        var inbound = Config.Inbound.FirstOrDefault();
        if (inbound is null || inbound.LocalPort is <= 0 or > 65535)
        {
            return [];
        }

        var listeners = new List<RuntimeListenerSnapshot>();
        string[] localProtocols = inbound.UdpEnabled ? ["http", "socks", "udp"] : ["http", "socks"];
        var localAddress = inbound.AllowLANConn && !inbound.NewPort4LAN ? "0.0.0.0" : "127.0.0.1";
        listeners.Add(new RuntimeListenerSnapshot("local", localProtocols, localAddress, inbound.LocalPort));
        if (inbound.SecondLocalPortEnabled)
        {
            var secondaryPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks2);
            if (secondaryPort is > 0 and <= 65535)
            {
                listeners.Add(new RuntimeListenerSnapshot("local-secondary", localProtocols, "127.0.0.1", secondaryPort));
            }
        }
        if (inbound.AllowLANConn && inbound.NewPort4LAN)
        {
            var lanPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks3);
            if (lanPort is > 0 and <= 65535)
            {
                listeners.Add(new RuntimeListenerSnapshot("lan", localProtocols, "0.0.0.0", lanPort));
            }
        }
        return listeners.ToArray();
    }

    private static string[] GetExpectedCoreExecutablePaths(CoreConfigContextBuilderAllResult built)
    {
        var coreTypes = new ECoreType?[]
            {
                built.MainResult.Context.RunCoreType,
                built.PreSocksResult?.Context.RunCoreType,
            }
            .Where(coreType => coreType.HasValue)
            .Select(coreType => coreType!.Value)
            .Distinct();

        return coreTypes
            .Select(coreType => CoreInfoManager.Instance.GetCoreInfo(coreType))
            .Select(info => info is null ? string.Empty : CoreInfoManager.Instance.GetCoreExecFile(info, out _))
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetExpectedCoreConfigPaths(CoreConfigContextBuilderAllResult built)
    {
        var fileNames = built.PreSocksResult is null
            ? new[] { Global.CoreConfigFileName }
            : new[] { Global.CoreConfigFileName, Global.CorePreConfigFileName };
        return fileNames
            .Select(Utils.GetBinConfigPath)
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static string? FindXrayExecutable(out string message)
    {
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray);
        foreach (var name in info?.CoreExes ?? [])
        {
            var path = Utils.GetBinPath(Utils.GetExeName(name), ECoreType.Xray.ToString());
            if (File.Exists(path))
            {
                message = string.Empty;
                return path;
            }
        }

        message = $"Xray-core executable was not found under {Utils.GetBinPath(string.Empty, ECoreType.Xray.ToString())}.";
        return null;
    }

    private static async Task<bool> WaitForListenerAsync(int port, CancellationToken cancellationToken)
    {
        if (port is <= 0 or > 65535)
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await IsListeningAsync(port, cancellationToken))
            {
                return true;
            }
            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    private static async Task<bool> IsListeningAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
            await client.ConnectAsync("127.0.0.1", port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IWebProxy?> GetRegionalPresetProxyAsync(CancellationToken cancellationToken)
    {
        var runtime = CurrentCoreRuntime;
        var inbound = Config.Inbound.FirstOrDefault();
        if (runtime.State != CoreRuntimeState.Running
            || !HasTrackedCoreProcesses
            || runtime.ProxyPort is not int port
            || inbound is null
            || port != inbound.LocalPort
            || !runtime.Listeners.Any(listener => listener.Name == "local"
                && listener.Port == port
                && listener.Protocols.Contains("socks", StringComparer.OrdinalIgnoreCase)))
        {
            return null;
        }

        // IsListeningAsync has a 250 ms connect budget. If the live mixed listener is not
        // already present, do not probe a configured-but-stopped SOCKS port via DownloadService.
        if (!await IsListeningAsync(port, cancellationToken)
            || !ReferenceEquals(runtime, CurrentCoreRuntime))
        {
            return null;
        }

        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    internal static async Task<bool> IsProjectedListenerUnavailableAsync(
        RuntimeListenerSnapshot target,
        IReadOnlyCollection<RuntimeListenerSnapshot> runtimeOwned,
        CancellationToken cancellationToken)
    {
        foreach (var address in GetProbeAddresses(target.ListenAddress))
        {
            var ownsTcp = runtimeOwned.Any(listener => listener.Port == target.Port
                && ListenerOwnsAddress(listener.ListenAddress, address));
            if (!ownsTcp
                && (await IsListeningOnAddressAsync(address, target.Port, cancellationToken)
                    || !CanBind(address, target.Port, SocketType.Stream, ProtocolType.Tcp)))
            {
                return true;
            }
            var targetUsesUdp = target.Protocols.Contains("udp", StringComparer.OrdinalIgnoreCase);
            var ownsUdp = runtimeOwned.Any(listener => listener.Port == target.Port
                && listener.Protocols.Contains("udp", StringComparer.OrdinalIgnoreCase)
                && ListenerOwnsAddress(listener.ListenAddress, address));
            if (targetUsesUdp && !ownsUdp
                && !CanBind(address, target.Port, SocketType.Dgram, ProtocolType.Udp))
            {
                return true;
            }
        }
        return false;
    }

    private static IPAddress[] GetProbeAddresses(string listenAddress)
    {
        if (IPAddress.TryParse(listenAddress, out var explicitAddress)
            && !explicitAddress.Equals(IPAddress.Any))
        {
            return [explicitAddress];
        }

        var addresses = new HashSet<IPAddress> { IPAddress.Loopback };
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var address in network.GetIPProperties().UnicastAddresses
                         .Select(item => item.Address)
                         .Where(address => address.AddressFamily == AddressFamily.InterNetwork))
            {
                addresses.Add(address);
            }
        }
        return addresses.ToArray();
    }

    private static bool ListenerOwnsAddress(string listenAddress, IPAddress address) =>
        IPAddress.TryParse(listenAddress, out var parsed)
        && (parsed.Equals(IPAddress.Any) || parsed.Equals(address));

    private static async Task<bool> IsListeningOnAddressAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
            await client.ConnectAsync(address, port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanBind(IPAddress address, int port, SocketType socketType, ProtocolType protocolType)
    {
        try
        {
            using var socket = new Socket(address.AddressFamily, socketType, protocolType)
            {
                ExclusiveAddressUse = true,
            };
            socket.Bind(new IPEndPoint(address, port));
            if (socketType == SocketType.Stream) socket.Listen(1);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

}
