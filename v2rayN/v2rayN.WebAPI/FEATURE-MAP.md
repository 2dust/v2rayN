# v2rayN.WebAPI feature map

The API uses existing public ServiceLib capabilities without changing desktop or Core behavior.
API scheduling and cancellation live in `V2rayRuntime.Scheduling.cs`; the desktop `TaskManager`
is not registered. `RuntimeMutationGate` serializes API-side writes to configuration and SQLite.

## ServiceLib → WebAPI capabilities

| Capability | Existing ServiceLib capability | WebAPI |
|---|---|---|
| Runtime status and operations | `MainWindowViewModel`; `StatusBarViewModel`; `CoreManager` | `GET /api/status`; `GET /api/operations` |
| Subscription groups and current group | `AppManager.SubItems`; `Config.SubIndexId`; `ProfilesViewModel.RefreshSubscriptions` | `GET /api/profile-groups`; `PUT /api/profile-groups/current` |
| Profiles and filtering | `AppManager.ProfileModels/ProfileItems`; `ProfileExManager`; `StatisticsManager`; `Utils.IsRegexMatch` | `GET /api/profiles?subscriptionId=&filter=`; filtering uses ServiceLib's regex guard |
| Current profile and Core lifecycle | `ConfigHandler.SetDefaultServerIndex`; `CoreConfigContextBuilder.BuildAll`; `CoreManager.LoadCore` | `POST /api/profiles/{id}/select`; `POST /api/core/start`, `/stop`, `/restart` |
| Add/edit profiles and group profiles | `ConfigHandler.AddServer/AddServerCommon`; protocol-specific `Add*Server`; `ProfileItem.IsValid`; `GroupProfileManager` | `GET /api/profiles/{id}`; `POST /api/profiles`; `PUT /api/profiles/{id}` |
| Import profiles and custom configuration | `ConfigHandler.AddBatchServers`; `FmtHandler.ResolveConfig`; custom-config parsers | `POST /api/profiles/import` |
| Remove/copy profiles | `ConfigHandler.RemoveServers/CopyServer` | `DELETE /api/profiles`; `POST /api/profiles/copy` |
| Deduplicate and remove failed test results | `ConfigHandler.DedupServerList/RemoveInvalidServerResult` | `POST /api/profiles/deduplicate`; `DELETE /api/profiles/invalid-test-results` |
| Move/reorder profiles | `ConfigHandler.MoveToGroup/MoveServer`; `ProfileExManager.SetSort` | `POST /api/profiles/move-to-group`; `POST /api/profiles/move` |
| Sort profiles | `ConfigHandler.SortServers`; `EServerColName` | `POST /api/profiles/sort` |
| Latency, UDP and speed tests | `SpeedtestService.RunLoop/ExitLoop`; `ESpeedActionType` | `POST /api/profiles/{id}/latency`; `POST/DELETE /api/speedtests`; progress/results through SSE |
| Share/export profiles and Core configuration | `FmtHandler.GetShareUri`; `InnerFmt.ToUri`; `CoreConfigContextBuilder.Build`; `CoreConfigHandler.GenerateClientConfig` | `POST /api/profiles/export` |
| Generate all-node/regional groups | `ConfigHandler.AddGroupAllServer/AddGroupRegionServer` | `POST /api/profile-groups/generate/all`; `POST /api/profile-groups/generate/regions` |
| Manage/share subscriptions | `SubItem`; `ConfigHandler.AddSubItem/DeleteSubItem`; `SubSettingViewModel` | `GET/POST/PUT/DELETE /api/subscriptions`; `GET /api/subscriptions/{id}/share` |
| Update subscriptions | `SubscriptionHandler.UpdateProcess` | `POST /api/subscriptions/update`; group/proxy options; progress through `GET /api/operations` and SSE |
| Scheduled subscription updates | `SubItem.AutoUpdateInterval/UpdateTime`; `SubscriptionHandler.UpdateProcess` | `V2rayRuntime.Scheduling.RunScheduledSubscriptionUpdatesAsync` checks intervals and starts API-owned background tasks |
| Core/GeoFiles updates | `CoreInfoManager`; `UpdateService`; `CoreManager` | `GET/POST /api/core-updates/{coreType}/check`, `/update`; `POST /api/core-updates/batch`; `POST /api/core/geo/update`; GeoFiles backup/verify/rollback |
| API self-update (separate from Desktop) | API-owned release manifest, package validation and native helper | `GET /api/web-updates`; `GET /api/web-updates/check`; `POST /api/web-updates/update`; protected systemd/container deployments are check-only |
| Inbound listeners | `Config.Inbound`; `StatusBarViewModel.InboundDisplayStatus`; `AppManager.GetLocalPort` | `GET /api/status`; `GET/PUT /api/settings/inbound` |
| Traffic/statistics | `StatisticsManager`; `ServerStatItem`; `ServerSpeedItem` | Profile byte counters; `GET /api/status`; SSE `traffic`; `DELETE /api/statistics` |
| Logs | `MsgViewModel`; `AppEvents.SendMsgViewRequested`; `CoreManager` callback | `GET/DELETE /api/logs`; `GET /api/logs/page?page=&pageSize=&filter=`; `/api/events` log/state/progress events |
| General settings | `OptionSettingViewModel`; `Config`; `ConfigHandler.SaveConfig` | `GET /api/settings`; atomic `PUT /api/settings/apply`; section settings APIs |
| Routing profiles | `AppManager.RoutingItems`; `ConfigHandler.SaveRoutingItem/RemoveRoutingItem/SetDefaultRouting/InitRouting` | `GET/POST/PUT/DELETE /api/settings/routing-profiles`; `/activate`; `/import`; `PUT /api/settings/routing` |
| Routing rules | `RoutingItem.RuleSet`; `RulesItem`; `ConfigHandler.AddBatchRoutingRules/MoveRoutingRule` | `GET/PUT /api/settings/routing-profiles/{id}/rules`; `/rules/import`; `/rules/move`; `/rules/{ruleId}` |
| Simple/per-Core DNS | `Config.SimpleDNSItem`; `DNSItem`; `ConfigHandler.SaveDNSItems/GetExternalDNSItem` | `GET/PUT /api/settings/dns/simple`; `GET/PUT /api/settings/dns/profiles` |
| Core configuration templates | `FullConfigTemplateItem`; `ConfigHandler.SaveFullConfigTemplate` | `GET /api/settings/core-templates`; `PUT /api/settings/core-templates/{coreType}`; stored TUN fields are preserved, not editable |
| Regional presets | `ConfigHandler.ApplyRegionalPreset/InitRouting` | `POST /api/settings/regional-presets/{preset}` |
| WebDAV and backup/restore | `WebDavManager`; existing `guiConfigs/` ZIP format; `FileUtils` | `GET/PUT /api/settings/webdav`; `/api/backup/webdav/check`; `/webdav`; `/webdav/restore`; `GET /api/backup/download`; multipart `POST /api/backup/restore` |

Core selection/startup remain delegated to ServiceLib's `CoreInfoManager` and `CoreManager`,
and require the selected Core executable. Generated speed-test configurations support Xray
and sing-box; TCPing is available for any profile with a port. Release/container artifacts
copy the complete architecture-matched official `2dust/v2rayN-core-bin` payload.

## Unsupported desktop capabilities

TUN settings/control, desktop system proxy, Clash Proxies/Connections, tray icons, global
hotkeys, window placement, desktop startup integration and native dialogs/scanners are outside
the headless API boundary. Core start/restart/autostart rejects generated TUN configurations
without changing saved settings. These exclusions are not claims of full desktop parity.

## Response contract

JSON field names and enum/state codes are stable identifiers. Operation/error responses use
`{ success, code, messageKey, data }`; SSE uses stable event/type codes plus data. User-provided
names, URLs, remarks and log lines remain data. The API does not render HTML or translate responses.
