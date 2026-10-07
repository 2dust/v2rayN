using ServiceLib.Enums;

namespace v2rayN.WebAPI.Contracts;

public sealed record SubscriptionInput(
    string Remarks,
    string Url,
    string? MoreUrl = null,
    bool? Enabled = null,
    string? UserAgent = null,
    string? RequestHeaders = null,
    string? Filter = null,
    int? AutoUpdateInterval = null,
    string? ConvertTarget = null,
    string? Memo = null,
    int? Sort = null,
    string? PrevProfile = null,
    string? NextProfile = null,
    int? PreSocksPort = null,
    ECoreType? CustomCoreType = null);

public sealed record SubscriptionView(
    string Id,
    string Remarks,
    string Url,
    bool Enabled,
    string? MoreUrl,
    string? Filter,
    int AutoUpdateInterval,
    long UpdateTime,
    string UserAgent,
    string? RequestHeaders,
    string? ConvertTarget,
    string? Memo,
    int Sort,
    string? PrevProfile,
    string? NextProfile,
    int? PreSocksPort,
    string? CustomCoreType);

public sealed record SubscriptionMutationResult(bool Success, string Code, string MessageKey, SubscriptionView? Data);

public sealed record ProfileView(
    string IndexId,
    string Remarks,
    string Protocol,
    string Address,
    int Port,
    string Network,
    string StreamSecurity,
    string? SubscriptionId,
    string? SubscriptionName,
    string CoreType,
    int Sort,
    int Delay,
    decimal Speed,
    string? IpInfo,
    long TodayUp,
    long TodayDown,
    long TotalUp,
    long TotalDown,
    bool IsCurrent,
    bool CanTest);

public sealed record ProfileGroupView(string Id, string Code, string? Name, string? MessageKey, int ProfileCount, bool IsCurrent);

public sealed record ProfileImportRequest(string Content, string? SubscriptionId = null, bool IsSubscription = false);

public sealed record ProfileIdsRequest(string[] ProfileIds);

public sealed record ProfileExportRequest(
    string[] ProfileIds,
    bool IncludeShareUris = true,
    bool Base64ShareUris = false,
    bool IncludeInnerUri = false,
    bool IncludeClientConfig = true);

public sealed record MoveProfilesRequest(string[] ProfileIds, string SubscriptionId);

public sealed record SortProfilesRequest(string? SubscriptionId, string Column, bool Ascending);

public sealed record MoveProfileRequest(string ProfileId, EMove Direction, int Position = -1);

public sealed record ProfileGroupSelectionRequest(string? SubscriptionId);

public sealed record SpeedTestRequest(ESpeedActionType Action, string[]? ProfileIds = null);

public sealed record SubscriptionUpdateRequest(string? SubscriptionId = null, bool UseProxy = false);

public sealed record InboundSettingsView(
    int LocalPort,
    bool SecondLocalPortEnabled,
    bool UdpEnabled,
    bool SniffingEnabled,
    string[] DestOverride,
    bool RouteOnly,
    bool AllowLANConn,
    bool NewPort4LAN,
    string? User,
    string? Pass);

public sealed record CoreSettingsView(
    bool LogEnabled,
    string? Loglevel,
    string? DefFingerprint,
    string? DefUserAgent,
    string? SendThrough,
    string? BindInterface,
    int? Mux4RayConcurrency,
    int? Mux4RayXudpConcurrency,
    string? Mux4RayXudpProxyUDP443,
    string? Mux4SboxProtocol,
    bool? Mux4SboxPadding,
    bool EnableCacheFile4Sbox,
    int Hy2UpMbps,
    int Hy2DownMbps,
    bool EnableFragment,
    bool EnableFinalFragment,
    string? FragmentPackets,
    string[] FragmentLengths,
    string[] FragmentDelays,
    string? FragmentMaxSplit);

public sealed record AppSettingsView(
    bool EnableStatistics,
    bool DisplayRealTimeSpeed,
    bool KeepOlderDedupl,
    int GeoAutoUpdateInterval,
    string? RootCertProvider,
    string? GeoSourceUrl,
    string? SrsSourceUrl,
    string? RouteRulesTemplateSourceUrl,
    string? SubConvertUrl);

public sealed record SpeedTestSettingsView(
    int SpeedTestTimeout,
    string? SpeedTestUrl,
    string? SpeedPingTestUrl,
    int MixedConcurrencyCount,
    string? IPAPIUrl,
    string? UdpTestTarget);

public sealed record WebSettingsOptionsView(
    string[] LogLevels,
    string[] RootCertProviders,
    string[] Fingerprints,
    string[] UserAgents,
    string[] Mux4SboxProtocols,
    string[] Mux4RayXudpProxyUDP443Options,
    string[] FragmentPacketsOptions,
    string[] DestOverrideProtocols,
    string[] RoutingBasicDomainStrategies,
    string[] RoutingBasicDomainStrategies4Singbox,
    string[] RoutingProfileDomainStrategies,
    string[] RoutingProfileDomainStrategies4Singbox,
    int[] MixedConcurrencyCounts,
    int[] SpeedTestTimeouts,
    string[] SpeedTestUrls,
    string[] SpeedPingTestUrls,
    string[] UdpTestTargets,
    string[] IPAPIUrls,
    string[] SubConvertUrls,
    string[] GeoFilesSources,
    string[] SingboxRulesetSources,
    string[] RoutingRulesSources);

public sealed record CoreTypeMapping(EConfigType ConfigType, ECoreType CoreType);

public sealed record WebSettingsView(
    InboundSettingsView Inbound,
    CoreSettingsView Core,
    AppSettingsView App,
    SpeedTestSettingsView SpeedTest,
    string? DomainStrategy,
    string? DomainStrategy4Singbox,
    IReadOnlyList<CoreTypeMapping> CoreTypes,
    WebSettingsOptionsView Options,
    bool ShowIpInfoColumn);

public sealed record InboundSettingsInput(
    int LocalPort,
    bool SecondLocalPortEnabled,
    bool UdpEnabled,
    bool SniffingEnabled,
    string[]? DestOverride,
    bool RouteOnly,
    bool AllowLANConn,
    bool NewPort4LAN,
    string? User,
    string? Pass);

public sealed record CoreSettingsInput(
    bool LogEnabled,
    string? Loglevel,
    string? DefFingerprint,
    string? DefUserAgent,
    string? SendThrough,
    string? BindInterface,
    int? Mux4RayConcurrency,
    int? Mux4RayXudpConcurrency,
    string? Mux4RayXudpProxyUDP443,
    string? Mux4SboxProtocol,
    bool? Mux4SboxPadding,
    bool EnableCacheFile4Sbox,
    int Hy2UpMbps,
    int Hy2DownMbps,
    bool EnableFragment,
    bool EnableFinalFragment,
    string? FragmentPackets,
    string[]? FragmentLengths,
    string[]? FragmentDelays,
    string? FragmentMaxSplit);

public sealed record AppSettingsInput(
    bool EnableStatistics,
    bool DisplayRealTimeSpeed,
    bool KeepOlderDedupl,
    int GeoAutoUpdateInterval,
    string? RootCertProvider,
    string? GeoSourceUrl,
    string? SrsSourceUrl,
    string? RouteRulesTemplateSourceUrl,
    string? SubConvertUrl);

public sealed record SpeedTestSettingsInput(
    int SpeedTestTimeout,
    string SpeedTestUrl,
    string SpeedPingTestUrl,
    int MixedConcurrencyCount,
    string IPAPIUrl,
    string UdpTestTarget);

public sealed record RoutingSettingsInput(string? DomainStrategy, string? DomainStrategy4Singbox);

public sealed record CoreConfigTemplateView(
    string Id,
    string Remarks,
    bool Enabled,
    ECoreType CoreType,
    string? Config,
    string? TunConfig,
    bool? AddProxyOnly,
    string? ProxyDetour);

public sealed record CoreConfigTemplateInput(
    string? Remarks,
    bool Enabled,
    string? Config,
    string? TunConfig,
    bool? AddProxyOnly,
    string? ProxyDetour);

public sealed record DnsProfileView(
    string Id,
    string Remarks,
    bool Enabled,
    ECoreType CoreType,
    bool UseSystemHosts,
    string? NormalDNS,
    string? TunDNS,
    string? DomainStrategy4Freedom,
    string? DomainDNSAddress);

public sealed record DnsProfileInput(
    string? Remarks,
    bool Enabled,
    bool UseSystemHosts,
    string? NormalDNS,
    string? TunDNS,
    string? DomainStrategy4Freedom,
    string? DomainDNSAddress);

public sealed record DnsDefaultProfileView(ECoreType CoreType, string NormalDNS, string TunDNS);

public sealed record DnsEditorOptionsView(
    string[] CoreTypes,
    string[] DomainStrategies4Freedom,
    string[] DomainStrategies4Singbox,
    string[] DomainDnsAddresses,
    string[] DirectDnsAddresses,
    string[] RemoteDnsAddresses,
    string[] BootstrapDnsAddresses,
    string[] FakeIpRanges,
    string[] ExpectedIps);

public sealed record SubscriptionEditorOptionsView(string[] ConvertTargets, string[] CustomCoreTypes);

public sealed record ProfileEditorOptionsView(
    string[] CoreTypes,
    string[] ConfigTypes,
    string[] SingboxOnlyConfigTypes,
    string[] Networks,
    string DefaultNetwork,
    string DefaultStreamSecurity,
    string DefaultSecurity,
    string DefaultVlessEncryption,
    string DefaultXhttpMode,
    string DefaultGrpcMode,
    int DefaultWireGuardMtu,
    string[] RawHeaderTypes,
    string[] XhttpModes,
    string[] KcpHeaderTypes,
    string[] GrpcModes,
    string[] VmessSecurities,
    string[] Flows,
    string[] TuicCongestionControls,
    string[] NaiveCongestionControls,
    string[] Fingerprints,
    string[] Alpns,
    IReadOnlyDictionary<string, string[]> ShadowsocksSecurities,
    string[] StreamSecurityTypes,
    string[] MultipleLoadStrategies);

public sealed record WebEditorOptionsView(
    SubscriptionEditorOptionsView Subscriptions,
    ProfileEditorOptionsView Profiles,
    DnsEditorOptionsView Dns);

public sealed record RouteRulesImportInput(string Content, bool Append = false);

public sealed record RouteRulesUrlImportInput(bool Append = false);

public sealed record ProfileExportItem(string IndexId, string Remarks, string Format, string Content);

public sealed record TrafficView(long ProxyUp, long ProxyDown, long DirectUp, long DirectDown);

public sealed record CoreUpdateCheckView(bool UpdateAvailable, string? Version, bool IsUpToDate = false, string? Detail = null);

public sealed record CoreUpdateTargetView(
    string CoreType,
    string NameKey,
    bool IsSupported,
    bool CanInstall,
    bool SupportsPreRelease,
    bool Selected,
    string? UnsupportedReasonKey);

public sealed record CoreUpdateSettingsView(
    IReadOnlyList<CoreUpdateTargetView> Targets,
    bool GeoFilesSelected,
    bool PreRelease,
    bool UseProxy,
    IReadOnlyList<string>? CheckPreReleaseCoreTypes = null);

public sealed record CoreUpdateSettingsInput(
    string[] SelectedCoreTypes,
    bool PreRelease,
    bool UseProxy,
    string[]? CheckPreReleaseCoreTypes = null);

public sealed record CoreUpdateBatchInput(bool Apply);

public sealed record CoreUpdateProgressView(
    string CoreType,
    string Phase,
    bool IsComplete,
    bool Success,
    bool CoreWasRunning,
    string? Version,
    string? Detail,
    bool Batch = false,
    bool? RollbackSucceeded = null);

public sealed record WebUpdateTargetView(
    string Name,
    string Version,
    string Commit,
    string BuildDate,
    string Rid,
    string Deployment,
    bool IsSupported,
    bool CanCheck,
    bool CanInstall,
    bool Selected,
    string? InstallReasonKey,
    string? LatestVersion = null);

public sealed record WebUpdateCheckView(
    bool UpdateAvailable,
    string CurrentVersion,
    string CurrentCommit,
    string Rid,
    string? LatestVersion,
    string? LatestCommit,
    bool CanInstall,
    string? InstallReasonKey,
    string? Detail = null);

public sealed record WebDavSettingsView(string? Url, string? UserName, string? DirName, bool HasPassword);

public sealed record WebDavSettingsInput(string? Url, string? UserName, string? Password, string? DirName);

public sealed record RouteRulesMoveRequest(string RuleId, EMove Direction, int Position = -1);

public sealed record CoreTypeMappingsInput(IReadOnlyList<CoreTypeMapping> Mappings);

public sealed record SettingsApplyInput(
    InboundSettingsInput Inbound,
    CoreSettingsInput Core,
    AppSettingsInput Application,
    SpeedTestSettingsInput SpeedTest,
    IReadOnlyList<CoreTypeMapping> CoreTypes,
    string? DomainStrategy,
    string? DomainStrategy4Singbox);

public sealed record ApiEnvelope<T>(bool Success, string Code, string MessageKey, T? Data)
{
    public static ApiEnvelope<T> Ok(T? data, string messageKey = ApiMessageKeys.CommonLoaded) => new(true, "ok", messageKey, data);

    public static ApiEnvelope<T> Fail(string code, string messageKey, T? data = default) => new(false, code, messageKey, data);
}

public sealed record OperationView(bool Success, string Code, string MessageKey, object? Data = null)
{
    public static OperationView Ok(string messageKey, object? data = null) => new(true, "ok", messageKey, data);

    public static OperationView Fail(string code, string messageKey, object? data = null) => new(false, code, messageKey, data);
}

public static class ApiMessageKeys
{
    public const string CommonLoaded = "common.loaded";
    public const string CommonCompleted = "common.completed";
    public const string CommonSaved = "common.saved";
    public const string CommonDeleted = "common.deleted";
    public const string CommonNotFound = "errors.notFound";
    public const string CommonInvalidInput = "errors.invalidInput";
    public const string CommonUnauthorized = "errors.unauthorized";
    public const string CommonInternal = "errors.internal";
    public const string CommonRouteNotFound = "errors.routeNotFound";
    public const string CommonCoreRestartRequired = "settings.savedRestartRequired";
    public const string CoreStarted = "core.started";
    public const string CoreStopped = "core.stopped";
    public const string CoreRestarted = "core.restarted";
    public const string CoreStartFailed = "core.startFailed";
    public const string CoreStopFailed = "core.stopFailed";
    public const string CoreNotRunning = "core.notRunning";
    public const string CoreRuntimeBusy = "core.runtimeBusy";
    public const string WebUpdateCheckFailed = "maintenance.webUpdateCheckFailed";
    public const string CoreBinaryMissing = "core.binaryMissing";
    public const string CorePortInUse = "core.portInUse";
    public const string CoreTunNotSupported = "core.tunNotSupported";
    public const string CoreUnsupported = "core.unsupported";
    public const string ProfileNotFound = "profiles.notFound";
    public const string ProfileDetailLoaded = "profiles.detailLoaded";
    public const string ProfileNoneSelected = "profiles.noneSelected";
    public const string ProfileInvalid = "profiles.invalid";
    public const string ProfileUnsupportedCore = "profiles.unsupportedCore";
    public const string ProfileSelectRequired = "profiles.selectRequired";
    public const string ProfileNotInGroup = "profiles.notInGroup";
    public const string ProfileImported = "profiles.imported";
    public const string ProfileDeleted = "profiles.deleted";
    public const string ProfileCopied = "profiles.copied";
    public const string ProfileMoved = "profiles.moved";
    public const string ProfileOrderSaved = "profiles.orderSaved";
    public const string ProfileDeduplicated = "profiles.deduplicated";
    public const string ProfileInvalidRemoved = "profiles.invalidRemoved";
    public const string ProfileGrouped = "profiles.grouped";
    public const string ProfileSaved = "profiles.saved";
    public const string ProfileSaveFailed = "profiles.saveFailed";
    public const string SubscriptionNotFound = "subscriptions.notFound";
    public const string SubscriptionAdded = "subscriptions.added";
    public const string SubscriptionSaved = "subscriptions.saved";
    public const string SubscriptionDeleted = "subscriptions.deleted";
    public const string SubscriptionUpdateStarted = "subscriptions.updateStarted";
    public const string SubscriptionUpdateBusy = "subscriptions.updateBusy";
    public const string SubscriptionInvalidUrl = "subscriptions.invalidUrl";
    public const string SubscriptionNameRequired = "subscriptions.nameRequired";
    public const string SubscriptionSaveFailed = "subscriptions.saveFailed";
    public const string SpeedTestStarted = "speedtest.started";
    public const string SpeedTestBusy = "speedtest.busy";
    public const string SpeedTestUnsupportedCore = "speedtest.unsupportedCore";
    public const string SpeedTestSelectRequired = "speedtest.selectRequired";
    public const string SpeedTestInvalidProfile = "speedtest.invalidProfile";
    public const string SpeedTestSettingsSaved = "speedtest.settingsSaved";
    public const string SpeedTestResult = "speedtest.result";
    public const string SettingsInvalidPort = "settings.invalidPort";
    public const string SettingsInvalidCoreLogLevel = "settings.invalidCoreLogLevel";
    public const string SettingsInvalidCoreValue = "settings.invalidCoreValue";
    public const string SettingsInvalidFragment = "settings.invalidFragment";
    public const string SettingsInvalidSpeedTest = "settings.invalidSpeedTest";
    public const string SettingsCoreApplyFailed = "settings.coreApplyFailed";
    public const string SettingsInvalidRoutingStrategy = "settings.invalidRoutingStrategy";
    public const string DnsProfileNotFound = "dns.profileNotFound";
    public const string DnsSaveFailed = "dns.saveFailed";
    public const string RoutingNameRequired = "routing.nameRequired";
    public const string RoutingProfileNotFound = "routing.profileNotFound";
    public const string RoutingRulesInvalid = "routing.rulesInvalid";
    public const string RoutingRuleNotFound = "routing.ruleNotFound";
    public const string RoutingSaveFailed = "routing.saveFailed";
    public const string RegionalPresetInvalid = "settings.invalidRegionalPreset";
    public const string RegionalPresetFailed = "settings.regionalPresetFailed";
    public const string XrayUpdateCheckFailed = "core.updateCheckFailed";
    public const string XrayUpdateStarted = "core.updateStarted";
    public const string XrayUpdateBusy = "core.updateBusy";
    public const string XrayUpdateFailed = "core.updateFailed";
    public const string XrayUpdateCompleted = "core.updateCompleted";
    public const string XrayUpdateAvailable = "core.updateAvailable";
    public const string XrayUpdateCurrent = "core.updateCurrent";
    public const string CoreUpdateCheckFailed = "maintenance.updateCheckFailed";
    public const string CoreUpdateStarted = "maintenance.updateStarted";
    public const string CoreUpdateBatchStarted = "maintenance.batchStarted";
    public const string CoreUpdateBusy = "maintenance.updateBusy";
    public const string CoreUpdateFailed = "maintenance.updateFailed";
    public const string CoreUpdateCompleted = "maintenance.updateCompleted";
    public const string CoreUpdateSettingsSaved = "maintenance.updateSettingsSaved";
    public const string CoreUpdateUnsupported = "maintenance.updateUnsupported";
    public const string CoreUpdateAvailable = "maintenance.updateAvailableGeneric";
    public const string CoreUpdateCurrent = "maintenance.upToDateGeneric";
    public const string GeoUpdateStarted = "updates.geoStarted";
    public const string GeoUpdateBusy = "updates.geoBusy";
    public const string GeoUpdateProgress = "updates.geoProgress";
    public const string GeoUpdateNotSelected = "maintenance.geoNotSelected";
    public const string SubscriptionUpdateProgress = "subscriptions.updateProgress";
    public const string ProfilesAllGroup = "profiles.allGroup";
    public const string BackupCreated = "backup.created";
    public const string BackupRestoreStarted = "backup.restoreStarted";
    public const string BackupRestoreFailed = "backup.restoreFailed";
    public const string BackupArchiveInvalid = "backup.archiveInvalid";
    public const string BackupDatabaseIncompatible = "backup.databaseIncompatible";
    public const string WebDavSettingsSaved = "backup.webdavSettingsSaved";
    public const string WebDavCheckSucceeded = "backup.webdavCheckSucceeded";
    public const string WebDavCheckFailed = "backup.webdavCheckFailed";
    public const string WebDavBackupSucceeded = "backup.webdavBackupSucceeded";
    public const string WebDavRestoreFailed = "backup.webdavRestoreFailed";
}

public sealed record ListenerView(
    string Name,
    string[] Protocols,
    string ListenAddress,
    int Port,
    bool Listening);

public sealed record StatusView(
    bool CoreRunning,
    string? CoreType,
    string? CurrentProfileId,
    string? CurrentProfileName,
    DateTimeOffset? CoreStartedAt,
    ListenerView[] Listeners,
    bool XrayAvailable,
    string Runtime,
    int ProfileCount,
    int SubscriptionCount,
    DateTimeOffset CheckedAt,
    bool StatisticsEnabled,
    TrafficView? Traffic,
    string RuntimeState = "stopped",
    int? ConfiguredProxyPort = null,
    int? RunningProxyPort = null,
    string? RunningProfileId = null,
    string? RunningProfileName = null,
    int? ApiPort = null,
    int[]? CoreProcessIds = null,
    string? RuntimeFailure = null,
    string? WebVersion = null,
    string? GitCommit = null,
    string? RuntimeIdentifier = null,
    string? WebBuildDate = null,
    bool SniffingEnabled = false,
    string[]? DestOverride = null,
    string[]? Capabilities = null);

public sealed record LogView(DateTimeOffset Timestamp, string Source, string Message, long Generation = 0);

public sealed record LogPageView(IReadOnlyList<LogView> Items, int Page, int PageSize, int Total, int TotalPages);

public sealed record WebEvent(string Type, object? Data, DateTimeOffset Timestamp);
