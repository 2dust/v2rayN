using System.Net;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;

namespace v2rayN.WebAPI.Services;

internal sealed record RegionalRoutingPlan(
    string? TemplateVersion,
    IReadOnlyList<RoutingItem> Items,
    string? DefaultRemarks = null,
    IReadOnlyList<string>? RemoveItemIds = null,
    string? ExistingDefaultItemId = null,
    bool ClearLegacyRoutingIndex = false,
    bool IsBuiltinFallback = false);

internal sealed record RegionalPresetStage(
    EPresetType Preset,
    string GeoSourceUrl,
    string SrsSourceUrl,
    string RouteRulesTemplateSourceUrl,
    SimpleDNSItem SimpleDnsItem,
    IReadOnlyList<DNSItem> DnsItems,
    RegionalRoutingPlan Routing);

internal sealed class RegionalPresetStager(
    Func<string, IWebProxy?, CancellationToken, Task<string?>> download,
    TimeSpan? timeout = null)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<RegionalPresetStage> StageAsync(
        EPresetType preset,
        IReadOnlyList<DNSItem> existingDnsItems,
        IReadOnlyList<RoutingItem> existingRoutingItems,
        IWebProxy? proxy,
        CancellationToken cancellationToken,
        string? legacyRoutingIndexId = null)
    {
        if (preset == EPresetType.Default)
        {
            return new(
                preset,
                string.Empty,
                string.Empty,
                string.Empty,
                ConfigHandler.InitBuiltinSimpleDNS(),
                [CreateDefaultDns(ECoreType.Xray, "V2ray"), CreateDefaultDns(ECoreType.sing_box, "sing-box")],
                new(null, []));
        }

        var sourceIndex = preset switch
        {
            EPresetType.Russia => 1,
            EPresetType.Iran => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported regional preset."),
        };
        var xrayItem = existingDnsItems.FirstOrDefault(item => item.CoreType == ECoreType.Xray)
            ?? throw new InvalidOperationException("The Xray DNS profile is missing.");
        var singboxItem = existingDnsItems.FirstOrDefault(item => item.CoreType == ECoreType.sing_box)
            ?? throw new InvalidOperationException("The sing-box DNS profile is missing.");
        var dnsBaseUrl = Global.DNSTemplateSources[sourceIndex];
        var routeTemplateUrl = Global.RoutingRulesSources[sourceIndex];

        var xrayTask = StageDnsProfileAsync(ECoreType.Xray, xrayItem, dnsBaseUrl + "v2ray.json", proxy, cancellationToken);
        var singboxTask = StageDnsProfileAsync(ECoreType.sing_box, singboxItem, dnsBaseUrl + "sing_box.json", proxy, cancellationToken);
        var simpleDnsTask = StageSimpleDnsAsync(dnsBaseUrl + "simple_dns.json", proxy, cancellationToken);
        var routingTask = StageRoutingAsync(routeTemplateUrl, existingRoutingItems, legacyRoutingIndexId, proxy, cancellationToken);
        await Task.WhenAll(xrayTask, singboxTask, simpleDnsTask, routingTask);

        var xrayDns = await xrayTask;
        var singboxDns = await singboxTask;
        var simpleDns = await simpleDnsTask;
        if (simpleDns.UsedFallback)
        {
            // Desktop enables both Core-specific DNS profiles when simple_dns.json is unavailable.
            xrayDns.Enabled = true;
            singboxDns.Enabled = true;
        }

        return new(
            preset,
            Global.GeoFilesSources[sourceIndex],
            Global.SingboxRulesetSources[sourceIndex],
            routeTemplateUrl,
            simpleDns.Item,
            [xrayDns, singboxDns],
            await routingTask);
    }

    private async Task<DNSItem> StageDnsProfileAsync(
        ECoreType coreType,
        DNSItem existing,
        string url,
        IWebProxy? proxy,
        CancellationToken cancellationToken)
    {
        var content = await TryDownloadAsync(url, proxy, cancellationToken);
        var template = content is null ? null : JsonUtils.Deserialize<DNSItem>(content);
        if (template is null)
        {
            // ConfigHandler.GetExternalDNSItem returns the current DNS item when the
            // template cannot be downloaded or parsed.
            return CloneDnsItem(existing);
        }

        var normalDnsTask = string.IsNullOrWhiteSpace(template.NormalDNS)
            ? Task.FromResult<string?>(null)
            : TryDownloadAsync(template.NormalDNS, proxy, cancellationToken);
        var tunDnsTask = string.IsNullOrWhiteSpace(template.TunDNS)
            ? Task.FromResult<string?>(null)
            : TryDownloadAsync(template.TunDNS, proxy, cancellationToken);
        await Task.WhenAll(normalDnsTask, tunDnsTask);

        // Desktop keeps a successfully downloaded template even when its referenced
        // NormalDNS/TunDNS resource fails; TryDownloadString then leaves that field empty.
        if (!string.IsNullOrWhiteSpace(template.NormalDNS))
        {
            template.NormalDNS = await normalDnsTask ?? string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(template.TunDNS))
        {
            template.TunDNS = await tunDnsTask ?? string.Empty;
        }

        template.Id = existing.Id;
        template.Remarks = existing.Remarks;
        template.Enabled = existing.Enabled;
        template.CoreType = coreType;
        return template;
    }

    private async Task<(SimpleDNSItem Item, bool UsedFallback)> StageSimpleDnsAsync(
        string url,
        IWebProxy? proxy,
        CancellationToken cancellationToken)
    {
        var content = await TryDownloadAsync(url, proxy, cancellationToken);
        var template = content is null ? null : JsonUtils.Deserialize<SimpleDNSItem>(content);
        return template is null
            ? (ConfigHandler.InitBuiltinSimpleDNS(), true)
            : (template, false);
    }

    private async Task<RegionalRoutingPlan> StageRoutingAsync(
        string url,
        IReadOnlyList<RoutingItem> existingItems,
        string? legacyRoutingIndexId,
        IWebProxy? proxy,
        CancellationToken cancellationToken)
    {
        var content = await TryDownloadAsync(url, proxy, cancellationToken);
        var template = content is null ? null : JsonUtils.Deserialize<RoutingTemplate>(content);
        if (template is null || string.IsNullOrWhiteSpace(template.Version) || template.RoutingItems is null)
        {
            return StageBuiltinRouting(existingItems, legacyRoutingIndexId);
        }

        if (existingItems.Any(item => item.Remarks?.StartsWith(template.Version, StringComparison.Ordinal) == true))
        {
            // Importing an already-present template version must not change the active route.
            return new(template.Version, []);
        }

        var stagedTasks = template.RoutingItems
            .Select(sourceItem => StageRoutingItemAsync(sourceItem, template.Version, proxy, cancellationToken))
            .ToArray();
        var stagedResults = await Task.WhenAll(stagedTasks);
        var stagedItems = stagedResults.Where(item => item is not null).Cast<RoutingItem>().ToArray();
        for (var index = 0; index < stagedItems.Length; index++)
        {
            stagedItems[index].Sort = existingItems.Count + index + 1;
        }

        return new(template.Version, stagedItems, stagedItems.FirstOrDefault()?.Remarks);
    }

    private async Task<RoutingItem?> StageRoutingItemAsync(
        RoutingItem? sourceItem,
        string templateVersion,
        IWebProxy? proxy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceItem is null || (string.IsNullOrWhiteSpace(sourceItem.Url) && string.IsNullOrWhiteSpace(sourceItem.RuleSet)))
        {
            return null;
        }

        var rulesContent = !string.IsNullOrWhiteSpace(sourceItem.RuleSet)
            ? sourceItem.RuleSet
            : await TryDownloadAsync(sourceItem.Url, proxy, cancellationToken);
        var rules = rulesContent is null ? null : JsonUtils.Deserialize<List<RulesItem>>(rulesContent);
        if (rules is null)
        {
            // Desktop skips an entry whose URL returns no content. Invalid rule JSON is
            // likewise not a usable AddBatchRoutingRules payload, so skip only that entry.
            return null;
        }

        var item = JsonUtils.DeepCopy(sourceItem);
        if (item is null)
        {
            return null;
        }
        item.Id = string.Empty;
        item.Remarks = $"{templateVersion}-{item.Remarks}";
        item.Enabled = true;
        item.Url = string.Empty;
        item.IsActive = false;
        item.RuleNum = rules.Count;
        item.RuleSet = JsonUtils.Serialize(rules, false);
        return item;
    }

    private static RegionalRoutingPlan StageBuiltinRouting(
        IReadOnlyList<RoutingItem> existingItems,
        string? legacyRoutingIndexId)
    {
        // Match InitBuiltinRouting's one-time locked-profile removal and migration of
        // RoutingIndexId, while keeping all database writes in the later apply phase.
        var lockedItem = existingItems.FirstOrDefault(item => item.Locked);
        var items = lockedItem is null
            ? existingItems.ToList()
            : existingItems.Where(item => item.Id != lockedItem.Id).ToList();
        var removeIds = lockedItem is null || string.IsNullOrEmpty(lockedItem.Id)
            ? Array.Empty<string>()
            : [lockedItem.Id];
        var clearLegacyIndex = !string.IsNullOrWhiteSpace(legacyRoutingIndexId);

        if (items.Count > 0)
        {
            var migrationTarget = clearLegacyIndex
                ? items.FirstOrDefault(item => item.Id == legacyRoutingIndexId)
                : null;
            return new(
                null,
                [],
                RemoveItemIds: removeIds,
                ExistingDefaultItemId: migrationTarget is { IsActive: false } ? migrationTarget.Id : null,
                ClearLegacyRoutingIndex: clearLegacyIndex,
                IsBuiltinFallback: true);
        }

        var defaults = new[]
        {
            (Name: "绕过大陆(Whitelist)", Resource: "white"),
            (Name: "黑名单(Blacklist)", Resource: "black"),
            (Name: "全局(Global)", Resource: "global"),
        };
        var builtins = defaults.Select((entry, index) =>
        {
            var rulesContent = EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + entry.Resource);
            var rules = JsonUtils.Deserialize<List<RulesItem>>(rulesContent)
                ?? throw new InvalidDataException($"The built-in routing rules '{entry.Resource}' are invalid.");
            return new RoutingItem
            {
                Remarks = $"V4-{entry.Name}",
                Url = string.Empty,
                RuleNum = rules.Count,
                RuleSet = JsonUtils.Serialize(rules, false),
                Enabled = true,
                Sort = index + 1,
            };
        }).ToArray();

        return new(
            null,
            builtins,
            builtins[0].Remarks,
            removeIds,
            ClearLegacyRoutingIndex: clearLegacyIndex,
            IsBuiltinFallback: true);
    }

    private async Task<string?> TryDownloadAsync(string url, IWebProxy? proxy, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadRequiredAsync(url, proxy, cancellationToken);
        }
        catch (Exception exception) when (IsFallbackFailure(exception, cancellationToken))
        {
            return null;
        }
    }

    private async Task<string> DownloadRequiredAsync(string url, IWebProxy? proxy, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            var content = await download(url, proxy, timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidDataException($"The download returned no content: {url}");
            }
            return content;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The download exceeded {_timeout.TotalSeconds:0.###} seconds: {url}");
        }
    }

    private static bool IsFallbackFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is not OperationCanceledException
        && exception is TimeoutException or HttpRequestException or WebException or IOException or InvalidDataException;

    private static DNSItem CloneDnsItem(DNSItem item) =>
        JsonUtils.DeepCopy(item) ?? throw new InvalidOperationException("A DNS profile could not be copied for regional preset staging.");

    private static DNSItem CreateDefaultDns(ECoreType coreType, string remarks) => new()
    {
        Remarks = remarks,
        CoreType = coreType,
        Enabled = false,
    };
}

internal static class RegionalPresetWorkflow
{
    public static async Task<TResult> RunAsync<TStage, TResult>(
        Func<CancellationToken, Task<TStage>> stage,
        Func<TStage, CancellationToken, Task<TResult>> apply,
        CancellationToken cancellationToken)
    {
        TStage staged;
        try
        {
            staged = await stage(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new RegionalPresetStagingException(exception);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return await apply(staged, cancellationToken);
    }
}

internal sealed class RegionalPresetStagingException(Exception innerException)
    : Exception("Regional preset staging failed.", innerException) { }

internal static class RegionalPresetApplyWorkflow
{
    public static async Task RunAsync(
        Func<CancellationToken, Task> applyConfiguration,
        Func<Func<Func<Task>, CancellationToken, Task>, CancellationToken, Task> updateGeoFilesTransaction,
        Func<Func<Task>, CancellationToken, Task> completeCoreApply,
        Func<Task> rollbackConfigurationAndRuntime,
        CancellationToken cancellationToken)
    {
        var configurationApplied = false;
        try
        {
            await applyConfiguration(cancellationToken);
            configurationApplied = true;
            cancellationToken.ThrowIfCancellationRequested();

            await updateGeoFilesTransaction(async (rollbackGeoFiles, transactionToken) =>
            {
                transactionToken.ThrowIfCancellationRequested();
                await completeCoreApply(rollbackGeoFiles, transactionToken);
            }, cancellationToken);
        }
        catch (RegionalPresetCoreApplyFailedException)
        {
            // CoreSettingsApply already restored its snapshot/runtime. The enclosing GeoFiles
            // transaction restores files before this exception is observed by its caller.
            throw;
        }
        catch (Exception applyException)
        {
            if (configurationApplied)
            {
                try
                {
                    await rollbackConfigurationAndRuntime();
                }
                catch (Exception rollbackException)
                {
                    throw new RegionalPresetApplyRollbackException(applyException, rollbackException);
                }
            }
            throw;
        }
    }
}

internal sealed class RegionalPresetCoreApplyFailedException : Exception { }

internal sealed class RegionalPresetApplyRollbackException(Exception applyException, Exception rollbackException)
    : Exception("The regional preset failed and its configuration/runtime rollback also failed.",
        new AggregateException(applyException, rollbackException)) { }

internal sealed record RegionalPresetApplyResult(bool Success, bool RollbackSucceeded, Exception? Error);

internal static class RegionalPresetTransaction
{
    public static async Task<RegionalPresetApplyResult> RunAsync(Func<Task> apply, Func<Task<bool>> rollback)
    {
        try
        {
            await apply();
            return new(true, true, null);
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = false;
            try
            {
                rollbackSucceeded = await rollback();
            }
            catch
            {
                // Rollback failure is represented in the result; preserve the original apply failure.
            }
            return new(false, rollbackSucceeded, exception);
        }
    }
}
