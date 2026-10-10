using System.Collections.Concurrent;
using System.Net;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

[NotInParallel]
public class RegionalPresetStagingTests
{
    [Test]
    public async Task RegionalPresetStagesAllRemoteDnsAndRoutingContentWithoutChangingUserProfileState()
    {
        var existingDns = CreateDnsRows();
        var responses = CreateRegionalResponses();
        var requestedUrls = new ConcurrentBag<string>();
        var stager = new RegionalPresetStager((url, _, token) =>
        {
            token.ThrowIfCancellationRequested();
            requestedUrls.Add(url);
            return Task.FromResult(responses.GetValueOrDefault(url));
        });

        var staged = await stager.StageAsync(
            EPresetType.Russia,
            existingDns,
            [],
            proxy: null,
            CancellationToken.None);

        await staged.GeoSourceUrl.Should().BeEqualTo(Global.GeoFilesSources[1]);
        await staged.SrsSourceUrl.Should().BeEqualTo(Global.SingboxRulesetSources[1]);
        await staged.RouteRulesTemplateSourceUrl.Should().BeEqualTo(Global.RoutingRulesSources[1]);
        await staged.DnsItems.Count.Should().BeEqualTo(2);
        await (staged.DnsItems[0].Id == "xray-id" && staged.DnsItems[0].Enabled && staged.DnsItems[0].Remarks == "my Xray DNS").Should().BeTrue();
        await (staged.DnsItems[0].NormalDNS == "xray-normal-content" && staged.DnsItems[0].TunDNS == "xray-tun-content").Should().BeTrue();
        await (staged.DnsItems[1].Id == "singbox-id" && !staged.DnsItems[1].Enabled && staged.DnsItems[1].Remarks == "my sing-box DNS").Should().BeTrue();
        await staged.SimpleDnsItem.RemoteDNS.Should().BeEqualTo("https://dns.example.test/remote");
        await staged.Routing.TemplateVersion.Should().BeEqualTo("R1");
        await staged.Routing.Items.Count.Should().BeEqualTo(1);
        await staged.Routing.Items[0].Remarks.Should().BeEqualTo("R1-Region rules");
        await existingDns[0].NormalDNS.Should().BeEqualTo("existing-xray-data");
        await requestedUrls.Count.Should().BeEqualTo(7);
    }

    [Test]
    public async Task DefaultPresetStagesOnlyLocalDefaultsAndDoesNotInvokeDownloader()
    {
        var downloaded = false;
        var stager = new RegionalPresetStager((_, _, _) =>
        {
            downloaded = true;
            return Task.FromResult<string?>(null);
        });

        var staged = await stager.StageAsync(EPresetType.Default, [], [], null, CancellationToken.None);

        await downloaded.Should().BeFalse();
        await staged.DnsItems.Count.Should().BeEqualTo(2);
        await staged.DnsItems.All(item => !item.Enabled).Should().BeTrue();
        await staged.GeoSourceUrl.Should().BeEqualTo(string.Empty);
        await staged.Routing.Items.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task SlowFailingPresetStagesDesktopFallbacksOutsideCoreAndMutationGates()
    {
        var downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coreGate = new SemaphoreSlim(1, 1);
        var mutationGate = new SemaphoreSlim(1, 1);
        var config = SniffingSettingsIntegrationTests.CreateConfig();
        config.ConstItem.GeoSourceUrl = "original-geo-source";
        SniffingSettingsIntegrationTests.BindAppManagerConfig(config);
        var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);
        var dnsRows = new List<string> { "original dns" };
        var routingRows = new List<string> { "original routing" };
        var applyCalled = false;
        RegionalPresetStage? capturedStage = null;
        var stager = new RegionalPresetStager(async (_, _, token) =>
        {
            downloadStarted.TrySetResult();
            await failDownload.Task.WaitAsync(token);
            throw new IOException("simulated download failure");
        }, TimeSpan.FromSeconds(2));
        var workflow = RegionalPresetWorkflow.RunAsync(
            token => stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, token),
            (stage, _) =>
            {
                applyCalled = true;
                capturedStage = stage;
                return Task.FromResult(true);
            },
            CancellationToken.None);

        await downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var settingsRead = await runtime.GetSettingsAsync();
        await settingsRead.App.GeoSourceUrl.Should().BeEqualTo("original-geo-source");
        await coreGate.WaitAsync(TimeSpan.Zero);
        coreGate.Release();
        await mutationGate.WaitAsync(TimeSpan.Zero);
        mutationGate.Release();

        failDownload.TrySetResult();
        await workflow;
        await applyCalled.Should().BeTrue();
        await capturedStage.Should().NotBeNull();
        await capturedStage!.DnsItems[0].NormalDNS.Should().BeEqualTo("existing-xray-data");
        await capturedStage.SimpleDnsItem.RemoteDNS.Should().BeEqualTo(ConfigHandler.InitBuiltinSimpleDNS().RemoteDNS);
        await capturedStage.Routing.IsBuiltinFallback.Should().BeTrue();
        await capturedStage.Routing.Items.Count.Should().BeEqualTo(3);
        await config.ConstItem.GeoSourceUrl.Should().BeEqualTo("original-geo-source");
        await dnsRows.SequenceEqual(new[] { "original dns" }).Should().BeTrue();
        await routingRows.SequenceEqual(new[] { "original routing" }).Should().BeTrue();
    }

    [Test]
    public async Task RequestCancellationCancelsStagingBeforeApply()
    {
        using var cancellation = new CancellationTokenSource();
        var downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var applyCalled = false;
        var stager = new RegionalPresetStager(async (_, _, token) =>
        {
            downloadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return null;
        });
        var workflow = RegionalPresetWorkflow.RunAsync(
            token => stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, token),
            (_, _) =>
            {
                applyCalled = true;
                return Task.FromResult(true);
            },
            cancellation.Token);

        await downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        var cancelled = false;
        try
        {
            await workflow;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await cancelled.Should().BeTrue();
        await applyCalled.Should().BeFalse();
    }

    [Test]
    public async Task FailedApplyRunsRollbackAndCannotTriggerCoreRestart()
    {
        var configValue = "before";
        var dnsRows = new List<string> { "dns-before" };
        var routingRows = new List<string> { "routing-before" };
        var restartCalled = false;

        var result = await RegionalPresetTransaction.RunAsync(
            async () =>
            {
                configValue = "after";
                dnsRows[0] = "dns-after";
                routingRows[0] = "routing-after";
                await Task.Yield();
                throw new IOException("simulated persistence failure");
            },
            () =>
            {
                configValue = "before";
                dnsRows[0] = "dns-before";
                routingRows[0] = "routing-before";
                return Task.FromResult(true);
            });

        if (result.Success) restartCalled = true;
        await result.Success.Should().BeFalse();
        await result.RollbackSucceeded.Should().BeTrue();
        await restartCalled.Should().BeFalse();
        await configValue.Should().BeEqualTo("before");
        await dnsRows.SequenceEqual(new[] { "dns-before" }).Should().BeTrue();
        await routingRows.SequenceEqual(new[] { "routing-before" }).Should().BeTrue();
    }

    [Test]
    public async Task DefaultRussiaAndIranApplySourcesBeforeGeoUpdateAndCoreCompletion()
    {
        foreach (var (preset, sourceIndex) in new[]
                 {
                     (EPresetType.Default, -1),
                     (EPresetType.Russia, 1),
                     (EPresetType.Iran, 2),
                 })
        {
            var responses = sourceIndex < 0 ? new Dictionary<string, string?>() : CreateRegionalResponses(sourceIndex);
            var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));
            var stage = await stager.StageAsync(
                preset,
                sourceIndex < 0 ? [] : CreateDnsRows(),
                [],
                null,
                CancellationToken.None);
            var currentGeoSource = "old-geo-source";
            var currentSrsSource = "old-srs-source";
            var updaterGeoSource = string.Empty;
            var updaterSrsSource = string.Empty;
            var order = new List<string>();

            await RegionalPresetApplyWorkflow.RunAsync(
                applyConfiguration: _ =>
                {
                    currentGeoSource = stage.GeoSourceUrl;
                    currentSrsSource = stage.SrsSourceUrl;
                    order.Add("config/db apply");
                    return Task.CompletedTask;
                },
                updateGeoFilesTransaction: async (beforeCommit, token) =>
                {
                    order.Add("GeoFiles update started");
                    updaterGeoSource = currentGeoSource;
                    updaterSrsSource = currentSrsSource;
                    await Task.Yield();
                    order.Add("GeoFiles update completed");
                    await beforeCommit(() => Task.CompletedTask, token);
                },
                completeCoreApply: (_, _) =>
                {
                    order.Add("Core restart");
                    return Task.CompletedTask;
                },
                rollbackConfigurationAndRuntime: () => Task.CompletedTask,
                CancellationToken.None);

            await updaterGeoSource.Should().BeEqualTo(sourceIndex < 0 ? string.Empty : Global.GeoFilesSources[sourceIndex]);
            await updaterSrsSource.Should().BeEqualTo(sourceIndex < 0 ? string.Empty : Global.SingboxRulesetSources[sourceIndex]);
            await order.SequenceEqual(new[]
            {
                "config/db apply", "GeoFiles update started", "GeoFiles update completed", "Core restart",
            }).Should().BeTrue();
        }
    }

    [Test]
    public async Task RunningCoreRestartsOnceAfterGeoUpdateAndStoppedCoreStaysStopped()
    {
        foreach (var initialState in new[] { CoreRuntimeState.Running, CoreRuntimeState.Stopped })
        {
            var geoUpdates = 0;
            var restarts = 0;
            var action = CoreSettingsApplyAction.SaveOnly;

            await RegionalPresetApplyWorkflow.RunAsync(
                _ => Task.CompletedTask,
                async (beforeCommit, token) =>
                {
                    geoUpdates++;
                    await beforeCommit(() => Task.CompletedTask, token);
                },
                (_, _) =>
                {
                    action = CoreSettingsApplyPolicy.Decide(
                        initialState,
                        hasActiveChild: initialState == CoreRuntimeState.Running,
                        changed: true);
                    if (action == CoreSettingsApplyAction.Restart)
                    {
                        restarts++;
                    }
                    return Task.CompletedTask;
                },
                rollbackConfigurationAndRuntime: () => Task.CompletedTask,
                CancellationToken.None);

            await geoUpdates.Should().BeEqualTo(1);
            await restarts.Should().BeEqualTo(initialState == CoreRuntimeState.Running ? 1 : 0);
            await action.Should().BeEqualTo(initialState == CoreRuntimeState.Running
                ? CoreSettingsApplyAction.Restart
                : CoreSettingsApplyAction.SaveOnly);
        }
    }

    [Test]
    public async Task GeoFilesFailureRollsBackPresetStateAndSkipsCoreCompletion()
    {
        using var directory = new TemporaryDirectory();
        var geoip = Path.Combine(directory.Path, "geoip.dat");
        var geosite = Path.Combine(directory.Path, "geosite.dat");
        var newSrs = Path.Combine(directory.Path, "geosite-region.srs");
        await File.WriteAllTextAsync(geoip, "old-geoip");
        await File.WriteAllTextAsync(geosite, "old-geosite");
        var configSource = "old-source";
        var dns = "old-dns";
        var routing = "old-routing";
        var restartCalled = false;
        var rollbackCalled = false;
        var failed = false;

        try
        {
            await RegionalPresetApplyWorkflow.RunAsync(
                _ =>
                {
                    configSource = "new-source";
                    dns = "new-dns";
                    routing = "new-routing";
                    return Task.CompletedTask;
                },
                (beforeCommit, token) => GeoFilesUpdateTransaction.ApplyAsync(
                    [geoip, geosite, newSrs],
                    [geoip, geosite],
                    async _ =>
                    {
                        await File.WriteAllTextAsync(geoip, "partial-geoip");
                        await File.WriteAllTextAsync(geosite, "partial-geosite");
                        await File.WriteAllTextAsync(newSrs, "partial-srs");
                        throw new IOException("simulated GeoFiles failure");
                    },
                    token,
                    beforeCommit),
                (_, _) =>
                {
                    restartCalled = true;
                    return Task.CompletedTask;
                },
                rollbackConfigurationAndRuntime: () =>
                {
                    configSource = "old-source";
                    dns = "old-dns";
                    routing = "old-routing";
                    rollbackCalled = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        }
        catch (IOException exception) when (exception.Message.Contains("simulated GeoFiles failure", StringComparison.Ordinal))
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await configSource.Should().BeEqualTo("old-source");
        await dns.Should().BeEqualTo("old-dns");
        await routing.Should().BeEqualTo("old-routing");
        await (await File.ReadAllTextAsync(geoip)).Should().BeEqualTo("old-geoip");
        await (await File.ReadAllTextAsync(geosite)).Should().BeEqualTo("old-geosite");
        await File.Exists(newSrs).Should().BeFalse();
        await rollbackCalled.Should().BeTrue();
        await restartCalled.Should().BeFalse();
    }

    [Test]
    public async Task GeoDownloadCancellationRollsBackPresetStateAndFilesWithoutCoreRestart()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        var geoip = Path.Combine(directory.Path, "geoip.dat");
        var geosite = Path.Combine(directory.Path, "geosite.dat");
        await File.WriteAllTextAsync(geoip, "old-geoip");
        await File.WriteAllTextAsync(geosite, "old-geosite");
        var configSource = "old-source";
        var rollbackCalled = false;
        var restartCalled = false;
        var canceled = false;

        try
        {
            await RegionalPresetApplyWorkflow.RunAsync(
                _ =>
                {
                    configSource = "new-source";
                    return Task.CompletedTask;
                },
                (beforeCommit, token) => GeoFilesUpdateTransaction.ApplyAsync(
                    [geoip, geosite],
                    [geoip, geosite],
                    async updateToken =>
                    {
                        await File.WriteAllTextAsync(geoip, "partial-geoip", updateToken);
                        await File.WriteAllTextAsync(geosite, "partial-geosite", updateToken);
                        cancellation.Cancel();
                        updateToken.ThrowIfCancellationRequested();
                    },
                    token,
                    beforeCommit),
                (_, _) =>
                {
                    restartCalled = true;
                    return Task.CompletedTask;
                },
                rollbackConfigurationAndRuntime: () =>
                {
                    configSource = "old-source";
                    rollbackCalled = true;
                    return Task.CompletedTask;
                },
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        await canceled.Should().BeTrue();
        await configSource.Should().BeEqualTo("old-source");
        await (await File.ReadAllTextAsync(geoip)).Should().BeEqualTo("old-geoip");
        await (await File.ReadAllTextAsync(geosite)).Should().BeEqualTo("old-geosite");
        await rollbackCalled.Should().BeTrue();
        await restartCalled.Should().BeFalse();
    }

    [Test]
    public async Task CoreRestartFailureRestoresPresetStateAndGeoFilesBeforeReturning()
    {
        using var directory = new TemporaryDirectory();
        var geoFile = Path.Combine(directory.Path, "geoip.dat");
        await File.WriteAllTextAsync(geoFile, "old-geoip");
        var configSource = "old-source";
        var runtimeState = CoreRuntimeState.Running;
        var restartAttempts = 0;
        var settingsRollbackCalled = false;
        var failed = false;

        try
        {
            await RegionalPresetApplyWorkflow.RunAsync(
                _ =>
                {
                    configSource = "new-source";
                    return Task.CompletedTask;
                },
                (beforeCommit, token) => GeoFilesUpdateTransaction.ApplyAsync(
                    [geoFile],
                    [geoFile],
                    updateToken => File.WriteAllTextAsync(geoFile, "new-geoip", updateToken),
                    token,
                    beforeCommit),
                async (rollbackGeoFiles, _) =>
                {
                    restartAttempts++;
                    await rollbackGeoFiles();
                    configSource = "old-source";
                    runtimeState = CoreRuntimeState.Running;
                    throw new RegionalPresetCoreApplyFailedException();
                },
                rollbackConfigurationAndRuntime: () =>
                {
                    settingsRollbackCalled = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        }
        catch (RegionalPresetCoreApplyFailedException)
        {
            failed = true;
        }

        await failed.Should().BeTrue();
        await configSource.Should().BeEqualTo("old-source");
        await runtimeState.Should().BeEqualTo(CoreRuntimeState.Running);
        await restartAttempts.Should().BeEqualTo(1);
        await settingsRollbackCalled.Should().BeFalse();
        await (await File.ReadAllTextAsync(geoFile)).Should().BeEqualTo("old-geoip");
    }

    private static List<DNSItem> CreateDnsRows() =>
    [
        new() { Id = "xray-id", CoreType = ECoreType.Xray, Remarks = "my Xray DNS", Enabled = true, NormalDNS = "existing-xray-data" },
        new() { Id = "singbox-id", CoreType = ECoreType.sing_box, Remarks = "my sing-box DNS", Enabled = false, NormalDNS = "existing-singbox-data" },
    ];

    private static Dictionary<string, string?> CreateRegionalResponses(int sourceIndex = 1)
    {
        var dnsBase = Global.DNSTemplateSources[sourceIndex];
        var xrayNormalUrl = "https://assets.example.test/xray-normal";
        var xrayTunUrl = "https://assets.example.test/xray-tun";
        var singboxNormalUrl = "https://assets.example.test/singbox-normal";
        var template = new RoutingTemplate
        {
            Version = "R1",
            RoutingItems =
            [
                new RoutingItem
                {
                    Remarks = "Region rules",
                    RuleSet = JsonUtils.Serialize(new List<RulesItem> { new() { Remarks = "sample" } }, false),
                },
            ],
        };

        return new(StringComparer.Ordinal)
        {
            [dnsBase + "v2ray.json"] = JsonUtils.Serialize(new DNSItem { NormalDNS = xrayNormalUrl, TunDNS = xrayTunUrl }, false),
            [dnsBase + "sing_box.json"] = JsonUtils.Serialize(new DNSItem { NormalDNS = singboxNormalUrl }, false),
            [dnsBase + "simple_dns.json"] = JsonUtils.Serialize(new SimpleDNSItem { RemoteDNS = "https://dns.example.test/remote" }, false),
            [Global.RoutingRulesSources[sourceIndex]] = JsonUtils.Serialize(template, false),
            [xrayNormalUrl] = "xray-normal-content",
            [xrayTunUrl] = "xray-tun-content",
            [singboxNormalUrl] = "singbox-normal-content",
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-WebAPI-regional-preset-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    [Test]
    public async Task FailedOrInvalidCoreDnsTemplatePreservesExistingDnsProfile()
    {
        var existingDns = CreateDnsRows();
        var responses = CreateRegionalResponses();
        var dnsBase = Global.DNSTemplateSources[1];
        responses[dnsBase + "v2ray.json"] = null;
        responses[dnsBase + "sing_box.json"] = "not-json";
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, existingDns, [], null, CancellationToken.None);

        await (staged.DnsItems[0].Id == existingDns[0].Id
            && staged.DnsItems[0].Remarks == existingDns[0].Remarks
            && staged.DnsItems[0].Enabled == existingDns[0].Enabled
            && staged.DnsItems[0].NormalDNS == existingDns[0].NormalDNS).Should().BeTrue();
        await (staged.DnsItems[1].Id == existingDns[1].Id
            && staged.DnsItems[1].Enabled == existingDns[1].Enabled
            && staged.DnsItems[1].NormalDNS == existingDns[1].NormalDNS).Should().BeTrue();
        await existingDns[0].NormalDNS.Should().BeEqualTo("existing-xray-data");
    }

    [Test]
    public async Task DownloadedDnsTemplateWithFailedExternalResourceKeepsTemplateAndLeavesFailedFieldEmpty()
    {
        var responses = CreateRegionalResponses();
        responses["https://assets.example.test/xray-normal"] = null;
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, CancellationToken.None);

        await staged.DnsItems[0].NormalDNS.Should().BeEqualTo(string.Empty);
        await staged.DnsItems[0].TunDNS.Should().BeEqualTo("xray-tun-content");
        await staged.DnsItems[0].Enabled.Should().BeTrue();
    }

    [Test]
    public async Task FailedSimpleDnsUsesDesktopBuiltinAndEnablesBothCoreDnsProfiles()
    {
        var responses = CreateRegionalResponses();
        responses[Global.DNSTemplateSources[1] + "simple_dns.json"] = null;
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, CancellationToken.None);

        var builtin = ConfigHandler.InitBuiltinSimpleDNS();
        await staged.SimpleDnsItem.RemoteDNS.Should().BeEqualTo(builtin.RemoteDNS);
        await staged.SimpleDnsItem.DirectDNS.Should().BeEqualTo(builtin.DirectDNS);
        await staged.DnsItems.All(item => item.Enabled).Should().BeTrue();
    }

    [Test]
    public async Task InvalidRoutingTemplateStagesBuiltinFallbackAndMakesFirstBuiltinDefaultWhenNoRoutesExist()
    {
        var responses = CreateRegionalResponses();
        responses[Global.RoutingRulesSources[1]] = "not-json";
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, CancellationToken.None);

        await staged.Routing.IsBuiltinFallback.Should().BeTrue();
        await staged.Routing.Items.Count.Should().BeEqualTo(3);
        await staged.Routing.Items[0].Remarks.Should().BeEqualTo("V4-绕过大陆(Whitelist)");
        await staged.Routing.DefaultRemarks.Should().BeEqualTo(staged.Routing.Items[0].Remarks);
        await staged.Routing.Items.All(item => item.RuleNum > 0).Should().BeTrue();
        await staged.Routing.ExistingDefaultItemId.Should().BeNull();
    }

    [Test]
    public async Task RoutingTemplateFallbackPreservesExistingActiveRouteLikeDesktopBuiltinInitialization()
    {
        var responses = CreateRegionalResponses();
        responses[Global.RoutingRulesSources[1]] = null;
        var active = new RoutingItem { Id = "active", Remarks = "user route", IsActive = true };
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [active], null, CancellationToken.None);

        await staged.Routing.IsBuiltinFallback.Should().BeTrue();
        await staged.Routing.Items.Count.Should().BeEqualTo(0);
        await staged.Routing.DefaultRemarks.Should().BeNull();
        await staged.Routing.ExistingDefaultItemId.Should().BeNull();
    }

    [Test]
    public async Task NewRegionalTemplatePlanActivatesFirstNewRouteEvenWhenAnotherRouteIsActive()
    {
        var responses = CreateRegionalResponses();
        var active = new RoutingItem { Id = "existing", Remarks = "user route", IsActive = true };
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [active], null, CancellationToken.None);

        await staged.Routing.Items.Count.Should().BeEqualTo(1);
        await staged.Routing.DefaultRemarks.Should().BeEqualTo("R1-Region rules");
        await active.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task ImportedRoutingTemplateVersionStagesNoDuplicateAndNoDefaultSwitch()
    {
        var responses = CreateRegionalResponses();
        var active = new RoutingItem { Id = "imported", Remarks = "R1-previous route", IsActive = true };
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [active], null, CancellationToken.None);

        await staged.Routing.TemplateVersion.Should().BeEqualTo("R1");
        await staged.Routing.Items.Count.Should().BeEqualTo(0);
        await staged.Routing.DefaultRemarks.Should().BeNull();
        await active.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task SingleRoutingRuleDownloadFailureSkipsOnlyThatEntryAndActivatesFirstStagedRule()
    {
        var responses = CreateRegionalResponses();
        var template = new RoutingTemplate
        {
            Version = "R2",
            RoutingItems =
            [
                new RoutingItem { Remarks = "unavailable", Url = "https://rules.example.test/missing" },
                new RoutingItem { Remarks = "available", Url = "https://rules.example.test/available" },
            ],
        };
        responses[Global.RoutingRulesSources[1]] = JsonUtils.Serialize(template, false);
        responses["https://rules.example.test/missing"] = null;
        responses["https://rules.example.test/available"] = JsonUtils.Serialize(new List<RulesItem> { new() { Remarks = "ok" } }, false);
        var stager = new RegionalPresetStager((url, _, _) => Task.FromResult(responses.GetValueOrDefault(url)));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, CancellationToken.None);

        await staged.Routing.Items.Count.Should().BeEqualTo(1);
        await staged.Routing.Items[0].Remarks.Should().BeEqualTo("R2-available");
        await staged.Routing.DefaultRemarks.Should().BeEqualTo("R2-available");
    }

    [Test]
    public async Task PerDownloadTimeoutUsesDesktopFallbackInsteadOfFailingPreset()
    {
        var stager = new RegionalPresetStager(async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return null;
        }, TimeSpan.FromMilliseconds(20));

        var staged = await stager.StageAsync(EPresetType.Russia, CreateDnsRows(), [], null, CancellationToken.None);

        await staged.DnsItems[0].NormalDNS.Should().BeEqualTo("existing-xray-data");
        await staged.SimpleDnsItem.RemoteDNS.Should().BeEqualTo(ConfigHandler.InitBuiltinSimpleDNS().RemoteDNS);
        await staged.Routing.IsBuiltinFallback.Should().BeTrue();
        await staged.Routing.Items.Count.Should().BeEqualTo(3);
    }
}
