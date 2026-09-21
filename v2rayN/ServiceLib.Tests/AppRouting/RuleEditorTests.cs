using ReactiveUI.Builder;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;
using ServiceLib.Services.AppRouting;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.AppRouting;

// These headless view-model tests share ReactiveUI scheduler initialization.
[NotInParallel]
public class RuleEditorTests
{
    static RuleEditorTests()
    {
        // Headless tests have no UI dispatcher: run UI notifications synchronously.
        RxAppBuilder.CreateReactiveUIBuilder().WithMainThreadScheduler(ImmediateSequencer.Instance)
            .WithCoreServices().BuildApp();
    }

    [Test]
    public async Task PathsWithSpacesAndQuotesSurviveNormalizationAndSerialization()
    {
        var path = Path.Combine(Path.GetTempPath(), "Applications With Spaces", "My Network App.exe");
        var normalized = AppRouteMatcher.Normalize("  \"" + path + "\"  ", false);
        var rule = JsonUtils.DeepCopy(new AppRouteRule { ExecutablePath = normalized });
        await rule.ExecutablePath.Should().BeEqualTo(path);
        await new AppRouteMatcher([rule]).Find(path, "My Network App.exe").Should().BeEqualTo(rule);
        await rule.MatchByName.Should().BeFalse();
    }

    [Test]
    public async Task NameRuleMatchesAllLocationsButExactPathWinsRegardlessOfOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), "App One", "browser.exe");
        var exact = new AppRouteRule { ExecutablePath = path };
        var byName = new AppRouteRule { ExecutablePath = "BROWSER.EXE", MatchByName = true };
        foreach (var rules in new[] { new[] { exact, byName }, new[] { byName, exact } })
        {
            var matcher = new AppRouteMatcher(rules);
            await matcher.Find(path, "browser.exe").Should().BeEqualTo(exact);
            await matcher.Find(Path.Combine(Path.GetTempPath(), "App Two", "browser.exe"), "browser.exe").Should().BeEqualTo(byName);
            await matcher.Find(null, "browser.exe").Should().BeEqualTo(byName);
            await (matcher.Find(null, "browser-helper.exe") == null).Should().BeTrue();
        }
    }

    [Test]
    public async Task NameRulesNeedNoInstalledFileAndRemainNamesAfterReload()
    {
        var name = "network-fixture-" + Guid.NewGuid().ToString("N") + ".exe";
        var rule = new AppRouteRule { ExecutablePath = name, MatchByName = true, Kind = AppRouteKind.Socks5 };
        AppRoutingManager.Validate([rule]);
        var copy = JsonUtils.DeepCopy(rule);
        await copy.MatchByName.Should().BeTrue();
        await copy.ExecutablePath.Should().BeEqualTo(name);
        await AppRouteMatcher.Normalize(Path.Combine(Path.GetTempPath(), "Program Files", "My App.exe"), true).Should().BeEqualTo("My App.exe");
    }

    [Test]
    public async Task RemovedExecutableDoesNotInvalidateOtherRulesOrBroadenPathMatching()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Removed app.exe");
        var removed = new AppRouteRule { ExecutablePath = path, Kind = AppRouteKind.ActiveProfile };
        var other = new AppRouteRule { ExecutablePath = "Installed app.exe", MatchByName = true, Kind = AppRouteKind.ActiveProfile };
        var reloaded = JsonUtils.DeepCopy(new AppRoutingItem { Enabled = true, Rules = [removed, other] });
        AppRoutingManager.Validate(reloaded.Rules);
        var matcher = new AppRouteMatcher(reloaded.Rules);
        await matcher.Find(null, "Installed app.exe").Should().BeEqualTo(reloaded.Rules[1]);
        await (matcher.Find(null, "Removed app.exe") == null).Should().BeTrue();
        await matcher.Find(path, "Removed app.exe").Should().BeEqualTo(reloaded.Rules[0]);
    }

    [Test]
    public async Task NetworkPickerGroupsAllProtocolsAndFiltersNamePidAndFullPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "Program Files", "Network App.exe");
        var apps = AppRouteProcessCatalog.Build([(10, 6), (10, 6), (10, 17), (20, 17), (30, 6), (40, 6)],
            pid => pid switch { 10 => ("Network App.exe", path), 20 => ("Another.exe", null), 30 => ("xray.exe", path), _ => null });
        await apps.Count.Should().BeEqualTo(2);
        await apps[0].Pid.Should().BeEqualTo(10);
        await apps[0].TcpConnections.Should().BeEqualTo(2);
        await apps[0].UdpEndpoints.Should().BeEqualTo(1);
        await apps[0].MatchesSearch("network app").Should().BeTrue();
        await apps[0].MatchesSearch("Program Files").Should().BeTrue();
        await apps[0].MatchesSearch("10").Should().BeTrue();
        await apps[0].MatchesSearch("not present").Should().BeFalse();
        await (apps[1].Path == null).Should().BeTrue();
    }

    [Test]
    public async Task NetworkPickerSeesOwnedUdpEndpointWithoutDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var apps = AppRouteProcessCatalog.Read();
        var own = apps.Single(p => p.Pid == Environment.ProcessId);
        await (own.UdpEndpoints > 0).Should().BeTrue();
        await Path.IsPathFullyQualified(own.Path!).Should().BeTrue();
    }

    [Test]
    public async Task NetworkPickerHidesSystemDirectoriesWithoutFilteringNamesElsewhere()
    {
        var windows = Path.Combine(Path.GetTempPath(), "Fixture Windows");
        var resolvedSystem = false;
        var apps = AppRouteProcessCatalog.Build(
            [(4, 6), (10, 6), (20, 6), (30, 17), (40, 6), (50, 6), (60, 17), (70, 6), (80, 6), (90, 6)],
            pid =>
            {
                if (pid == 4)
                {
                    resolvedSystem = true;
                }

                return pid switch
                {
                    10 => ("svchost.exe", Path.Combine(windows, "System32", "svchost.exe")),
                    20 => ("services.exe", Path.Combine(windows, "SYSWOW64", "services.exe")),
                    30 => ("wininit.exe", Path.Combine(windows, "System32", "nested", "wininit.exe")),
                    40 => ("System.exe", null),
                    50 => ("svchost.exe", Path.Combine(windows, "System32-tools", "svchost.exe")),
                    60 => ("services.exe", Path.Combine(Path.GetTempPath(), "User apps", "services.exe")),
                    70 => ("Unknown path.exe", null),
                    80 => ("System.exe", Path.Combine(Path.GetTempPath(), "User apps", "System.exe")),
                    90 => ("wininit.exe", Path.Combine(windows, "SysWOW64", "..", "System32", "wininit.exe")),
                    _ => ("System.exe", null)
                };
            }, windows);
        await resolvedSystem.Should().BeFalse();
        await apps.Select(app => app.Pid).Order().ToArray().Should().BeEquivalentTo(new[] { 50, 60, 70, 80 });
    }

    [Test]
    public async Task SwitchTracksSuccessfulStartAndStopWithoutFeedbackCalls()
    {
        var runtime = new FakeRuntime();
        using var vm = CreateViewModel(runtime);
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        await vm.RoutingEnabled.Should().BeTrue();
        await vm.IsBusy.Should().BeFalse();
        await runtime.Starts.Should().BeEqualTo(1);
        await vm.ChangeRoutingCmd.Execute(false).ToTask();
        await vm.RoutingEnabled.Should().BeFalse();
        await runtime.Stops.Should().BeEqualTo(1);
    }

    [Test]
    public async Task BoundSwitchPropertyInvokesRuntimeAndSettlesOnActualState()
    {
        var runtime = new FakeRuntime();
        using var vm = CreateViewModel(runtime);
        vm.RoutingEnabled = true;
        await vm.WhenAnyValue(model => model.IsBusy).Where(busy => !busy && runtime.Starts == 1).FirstAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        await vm.RoutingEnabled.Should().BeTrue();
        vm.RoutingEnabled = false;
        await vm.WhenAnyValue(model => model.IsBusy).Where(busy => !busy && runtime.Stops == 1).FirstAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        await vm.RoutingEnabled.Should().BeFalse();
    }

    [Test]
    public async Task FailedStartResetsSwitchAndDisplaysError()
    {
        var runtime = new FakeRuntime { FailStart = true };
        using var vm = CreateViewModel(runtime);
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        await vm.RoutingEnabled.Should().BeFalse();
        await vm.CanEdit.Should().BeTrue();

    }

    [Test]
    public async Task SavingWhileEnabledAppliesRuleAndDeletingLastRuleStopsRouting()
    {
        var runtime = new FakeRuntime();
        using var vm = CreateViewModel(runtime);
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        vm.MatchByName = true;
        vm.IncludeChildProcesses = true;
        vm.Executable = "My Network App.exe";
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        await runtime.Starts.Should().BeEqualTo(2);
        await runtime.LastConfig!.AppRouting.Rules.Single().ExecutablePath.Should().BeEqualTo("My Network App.exe");
        await runtime.LastConfig.AppRouting.Enabled.Should().BeTrue();
        await runtime.LastConfig.AppRouting.Rules.Single().IncludeChildProcesses.Should().BeTrue();
        await vm.DeleteCmd.Execute().ToTask();
        await runtime.Stops.Should().BeEqualTo(1);
        await vm.RoutingEnabled.Should().BeFalse();
    }

    [Test]
    [Arguments("ChatGPT.exe")]
    [Arguments("codex.exe")]
    public async Task ConvertingSavedPathToNameAppliesAndMatchesAfterReload(string executableName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "App routing " + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, executableName);
        var runtime = new FakeRuntime();
        using var vm = CreateViewModel(runtime);
        vm.Executable = path;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        var originalId = vm.SelectedRule!.Rule.Id;
        await vm.ChangeRoutingCmd.Execute(true).ToTask();

        vm.MatchByName = true;
        await vm.SaveRuleCmd.Execute().ToTask();

        var rule = JsonUtils.DeepCopy(runtime.LastConfig!).AppRouting.Rules.Single();
        await rule.Id.Should().BeEqualTo(originalId);
        await rule.MatchByName.Should().BeTrue();
        await rule.ExecutablePath.Should().BeEqualTo(executableName);
        await runtime.Starts.Should().BeEqualTo(2);
        var matcher = new AppRouteMatcher([rule]);
        await matcher.Find(path, executableName).Should().BeEqualTo(rule);
        await matcher.Find(Path.Combine(directory, "Updated version", executableName), executableName.ToUpperInvariant())
            .Should().BeEqualTo(rule);
    }

    [Test]
    public async Task ChoosingRunningAppPreservesPathOrUsesNameWhenRequested()
    {
        using var vm = CreateViewModel(new FakeRuntime());
        var path = Path.Combine(Path.GetTempPath(), "Program Files", "Network App.exe");
        AppRouteProcess choice = new(10, "Network App.exe", path, 1, 1);
        using var handler = vm.PickProcess.RegisterHandler(interaction => interaction.SetOutput(choice));
        await vm.PickProcessCmd.Execute().ToTask();
        await vm.Executable.Should().BeEqualTo(path);
        vm.MatchByName = true;
        await vm.PickProcessCmd.Execute().ToTask();
        await vm.Executable.Should().BeEqualTo("Network App.exe");
        vm.MatchByName = false;
        choice = new(20, "Restricted.exe", null, 1, 0);
        await vm.PickProcessCmd.Execute().ToTask();
        await vm.Executable.Should().BeEqualTo("Restricted.exe");
        await vm.MatchByName.Should().BeTrue();
    }

    [Test]
    public async Task CancelingPickerKeepsEditorAndClearsPreviousPickerSelection()
    {
        using var vm = CreateViewModel(new FakeRuntime());
        vm.Executable = "Existing.exe";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        vm.SocksPort = 12345;
        vm.ProcessSearch = "previous search";
        vm.SelectedProcess = new(10, "Previous.exe", null, 1, 0);
        using var handler = vm.PickProcess.RegisterHandler(interaction => interaction.SetOutput(null));
        await vm.PickProcessCmd.Execute().ToTask();
        await vm.Executable.Should().BeEqualTo("Existing.exe");
        await vm.MatchByName.Should().BeTrue();
        await vm.SocksPort.Should().BeEqualTo(12345);
        await (vm.SelectedProcess == null).Should().BeTrue();
        await vm.ProcessSearch.Should().BeEqualTo("");
        await vm.CanUseProcess.Should().BeFalse();
    }

    private static void SelectRoute(AppRoutingViewModel vm, AppRouteKind kind) => vm.SelectedDestination = vm.Destinations.Single(d => d.Kind == kind);

    private static AppRoutingViewModel CreateViewModel(FakeRuntime runtime) => new(new Config(), runtime, _ => Task.FromResult(0), true);

    [Test]
    public async Task SavedProfileBlockingOptionDefaultsOffAndSurvivesEditingAndReload()
    {
        var legacy = JsonUtils.Deserialize<AppRouteRule>("{\"Kind\":0,\"ProfileId\":\"saved\"}")!;
        await legacy.ApplyBlockingRules.Should().BeFalse();
        var config = new Config();
        using var vm = new AppRoutingViewModel(config, new FakeRuntime(), _ => Task.FromResult(0), true);
        vm.Profiles.Add(new("saved", "Saved profile"));
        SelectRoute(vm, AppRouteKind.Profile);
        vm.SelectedProfile = vm.Profiles.Single();
        vm.MatchByName = true;
        vm.Executable = "App.exe";
        await vm.ApplyBlockingRules.Should().BeFalse();
        await vm.IsProfile.Should().BeTrue();
        vm.ApplyBlockingRules = true;
        await vm.SaveRuleCmd.Execute().ToTask();
        var saved = JsonUtils.DeepCopy(config.AppRouting.Rules.Single());
        await saved.ApplyBlockingRules.Should().BeTrue();
        await vm.NewCmd.Execute().ToTask();
        await vm.ApplyBlockingRules.Should().BeFalse();
        await vm.IsProfile.Should().BeFalse();
        vm.SelectedRule = vm.Rules.Single();
        await vm.ApplyBlockingRules.Should().BeTrue();
        await vm.IsProfile.Should().BeTrue();
        vm.ApplyBlockingRules = false;
        await vm.SaveRuleCmd.Execute().ToTask();
        await JsonUtils.DeepCopy(config.AppRouting.Rules.Single()).ApplyBlockingRules.Should().BeFalse();
    }

    [Test]
    public async Task NewRulesDefaultToActiveProfileWithoutExtraDestinationFields()
    {
        var config = new Config();
        using var vm = new AppRoutingViewModel(config, new FakeRuntime(), _ => Task.FromResult(0), true);
        await vm.SelectedDestination.Should().BeEqualTo(vm.Destinations[0]);
        await vm.Destinations[0].Label.Should().BeEqualTo(ServiceLib.Resx.ResUI.AppRoutingActiveProfile);
        await vm.IsProfile.Should().BeFalse();
        await vm.IsSocks.Should().BeFalse();
        await vm.IsInterface.Should().BeFalse();
        vm.Executable = "App.exe";
        vm.MatchByName = true;
        await vm.SaveRuleCmd.Execute().ToTask();
        var saved = JsonUtils.DeepCopy(config.AppRouting.Rules.Single());
        await saved.Kind.Should().BeEqualTo(AppRouteKind.ActiveProfile);
        await saved.Enabled.Should().BeTrue();
        await vm.SelectedRule!.Destination.Should().BeEqualTo(vm.Destinations[0].Label);
        await vm.ToggleRuleEnabledCmd.Execute(vm.SelectedRule).ToTask();
        await vm.SaveRuleCmd.Execute().ToTask();
        await config.AppRouting.Rules.Single().Enabled.Should().BeFalse();
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.NewCmd.Execute().ToTask();
        await vm.SelectedDestination.Should().BeEqualTo(vm.Destinations[0]);
        await vm.Enabled.Should().BeTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ExistingDestinationValuesKeepTheirMeaningAfterAddingActiveProfile(int kind)
    {
        var saved = JsonUtils.Deserialize<AppRouteRule>("{\"ExecutablePath\":\"App.exe\",\"MatchByName\":true,\"Kind\":" + kind +
            ",\"ProfileId\":\"profile\",\"InterfaceId\":\"adapter\",\"SocksHost\":\"192.0.2.1\",\"SocksPort\":23456}")!;
        var config = new Config();
        using var vm = new AppRoutingViewModel(config, new FakeRuntime(), _ => Task.FromResult(0), true);
        vm.Profiles.Add(new("profile", "Saved profile"));
        vm.Interfaces.Add(new("adapter", "Adapter"));
        var row = new AppRouteRow(saved, "fixture");
        vm.Rules.Add(row);
        vm.SelectedRule = row;
        await vm.SelectedDestination!.Kind.Should().BeEqualTo((AppRouteKind)kind);
        await vm.IsProfile.Should().BeEqualTo(kind == 0);
        await vm.IsSocks.Should().BeEqualTo(kind == 1);
        await vm.IsInterface.Should().BeEqualTo(kind == 2);
        await vm.SaveRuleCmd.Execute().ToTask();
        var reloaded = JsonUtils.DeepCopy(config.AppRouting.Rules.Single());
        await ((int)reloaded.Kind).Should().BeEqualTo(kind);
        await reloaded.ProfileId.Should().BeEqualTo("profile");
        await reloaded.InterfaceId.Should().BeEqualTo("adapter");
        await reloaded.SocksHost.Should().BeEqualTo("192.0.2.1");
        await reloaded.SocksPort.Should().BeEqualTo(23456);
    }

    [Test]
    public async Task SavedPreferenceAndChildOptionSurviveEditorChanges()
    {
        var config = new Config { AppRouting = new() { Enabled = true } };
        var runtime = new FakeRuntime();
        using var vm = new AppRoutingViewModel(config, runtime, _ => Task.FromResult(0), true);
        await vm.RoutingEnabled.Should().BeTrue();
        vm.Executable = "Parent.exe";
        vm.MatchByName = true;
        vm.IncludeChildProcesses = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        var row = vm.SelectedRule;
        await config.AppRouting.Enabled.Should().BeTrue();
        await runtime.Starts.Should().BeEqualTo(1);
        await vm.NewCmd.Execute().ToTask();
        await vm.IncludeChildProcesses.Should().BeFalse();
        vm.SelectedRule = row;
        await vm.IncludeChildProcesses.Should().BeTrue();
        await vm.ChangeRoutingCmd.Execute(false).ToTask();
        await config.AppRouting.Enabled.Should().BeFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonAdministratorKeepsSwitchStateAndSavesTableEditsWithoutStartingRouting(bool enabled)
    {
        var rule = new AppRouteRule { ExecutablePath = "App.exe", MatchByName = true, Kind = AppRouteKind.Socks5 };
        var config = new Config { AppRouting = new() { Enabled = enabled, Rules = [JsonUtils.DeepCopy(rule)] } };
        var runtime = new FakeRuntime();
        var saves = 0;
        using var vm = new AppRoutingViewModel(config, runtime, _ => { saves++; return Task.FromResult(0); }, false);
        var row = new AppRouteRow(rule, "fixture");
        vm.Rules.Add(row);
        await vm.RoutingEnabled.Should().BeEqualTo(enabled);
        await vm.CanChangeRouting.Should().BeFalse();
        await (await vm.ChangeRoutingCmd.CanExecute.FirstAsync()).Should().BeFalse();
        vm.IsBusy = true;
        vm.IsBusy = false;
        await vm.CanChangeRouting.Should().BeFalse();
        await saves.Should().BeEqualTo(0);
        await vm.ToggleRuleChildrenCmd.Execute(row).ToTask();
        await config.AppRouting.Rules.Single().IncludeChildProcesses.Should().BeTrue();
        await vm.ToggleRuleEnabledCmd.Execute(row).ToTask();
        await config.AppRouting.Rules.Single().Enabled.Should().BeFalse();
        await vm.RoutingEnabled.Should().BeEqualTo(enabled);
        await config.AppRouting.Enabled.Should().BeEqualTo(enabled);
        await saves.Should().BeEqualTo(2);
        await runtime.Starts.Should().BeEqualTo(0);
        await runtime.Stops.Should().BeEqualTo(0);
    }

    [Test]
    public async Task TableFlagsSaveAndApplyWithoutSavingOtherEditorDraftFields()
    {
        var config = new Config();
        var runtime = new FakeRuntime();
        using var vm = new AppRoutingViewModel(config, runtime, _ => Task.FromResult(0), true);
        vm.Executable = "App.exe";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        var row = vm.SelectedRule!;
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        vm.Executable = "Unsaved.exe";
        vm.SocksPort = 12345;
        await vm.ToggleRuleChildrenCmd.Execute(row).ToTask();
        await config.AppRouting.Rules.Single().IncludeChildProcesses.Should().BeTrue();
        await runtime.Starts.Should().BeEqualTo(2);
        await runtime.LastConfig!.AppRouting.Rules.Single().ExecutablePath.Should().BeEqualTo("App.exe");
        await runtime.LastConfig.AppRouting.Rules.Single().SocksPort.Should().BeEqualTo(10808);
        await vm.Executable.Should().BeEqualTo("Unsaved.exe");
        await vm.SocksPort.Should().BeEqualTo(12345);
        await vm.IncludeChildProcesses.Should().BeTrue();
        await vm.ToggleRuleEnabledCmd.Execute(row).ToTask();
        await row.Enabled.Should().BeFalse();
        await vm.Enabled.Should().BeFalse();
        await runtime.Stops.Should().BeEqualTo(1);
        await config.AppRouting.Enabled.Should().BeFalse();
        await vm.CanChangeRouting.Should().BeTrue();
    }

    [Test]
    public async Task TableSaveFailureRestoresRowAndPreservesEditorDraft()
    {
        var config = new Config();
        var failSave = false;
        using var vm = new AppRoutingViewModel(config, new FakeRuntime(), _ => Task.FromResult(failSave ? -1 : 0), true);
        vm.Executable = "App.exe";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        var row = vm.SelectedRule!;
        vm.IncludeChildProcesses = true;
        vm.Executable = "Unsaved.exe";
        failSave = true;
        await vm.ToggleRuleChildrenCmd.Execute(row).ToTask();
        await row.IncludeChildProcesses.Should().BeFalse();
        await config.AppRouting.Rules.Single().IncludeChildProcesses.Should().BeFalse();
        await vm.IncludeChildProcesses.Should().BeTrue();
        await vm.Executable.Should().BeEqualTo("Unsaved.exe");
    }

    [Test]
    public async Task TableRuntimeFailureKeepsSuccessfullySavedChange()
    {
        var config = new Config();
        var runtime = new FakeRuntime();
        using var vm = new AppRoutingViewModel(config, runtime, _ => Task.FromResult(0), true);
        vm.Executable = "App.exe";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        runtime.FailStart = true;
        await vm.ToggleRuleChildrenCmd.Execute(vm.SelectedRule!).ToTask();
        await vm.SelectedRule!.IncludeChildProcesses.Should().BeTrue();
        await config.AppRouting.Rules.Single().IncludeChildProcesses.Should().BeTrue();

    }

    [Test]
    public async Task TableCannotEnableConflictingRule()
    {
        var config = new Config();
        var saves = 0;
        using var vm = new AppRoutingViewModel(config, new FakeRuntime(), _ => { saves++; return Task.FromResult(0); }, true);
        vm.Executable = "App.exe";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        await vm.SaveRuleCmd.Execute().ToTask();
        await vm.NewCmd.Execute().ToTask();
        vm.Executable = "APP.EXE";
        vm.MatchByName = true;
        SelectRoute(vm, AppRouteKind.Socks5);
        vm.Enabled = false;
        await vm.SaveRuleCmd.Execute().ToTask();
        var row = vm.SelectedRule!;
        var savedCount = saves;
        await vm.ToggleRuleEnabledCmd.Execute(row).ToTask();
        await row.Enabled.Should().BeFalse();
        await vm.Enabled.Should().BeFalse();
        await saves.Should().BeEqualTo(savedCount);
        await config.AppRouting.Rules.Count(r => r.Enabled).Should().BeEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SaveAndDeleteFailuresPreserveSavedRowsSelectionAndDraft(bool throws)
    {
        var config = new Config();
        var runtime = new FakeRuntime();
        var fail = false;
        using var vm = new AppRoutingViewModel(config, runtime, _ => fail && throws
            ? Task.FromException<int>(new IOException("Fixture save failure")) : Task.FromResult(fail ? -1 : 0), true);
        vm.Executable = "Saved.exe";
        vm.MatchByName = true;
        await vm.SaveRuleCmd.Execute().ToTask();
        await vm.ChangeRoutingCmd.Execute(true).ToTask();
        var savedRow = vm.SelectedRule;
        vm.Executable = "Unsaved.exe";
        fail = true;
        await vm.SaveRuleCmd.Execute().ToTask();
        await vm.Rules.Single().Should().BeEqualTo(savedRow);
        await vm.SelectedRule.Should().BeEqualTo(savedRow);
        await vm.Executable.Should().BeEqualTo("Unsaved.exe");
        await config.AppRouting.Rules.Single().ExecutablePath.Should().BeEqualTo("Saved.exe");
        await vm.DeleteCmd.Execute().ToTask();
        await vm.Rules.Single().Should().BeEqualTo(savedRow);
        await vm.SelectedRule.Should().BeEqualTo(savedRow);
        await vm.Executable.Should().BeEqualTo("Unsaved.exe");
        await config.AppRouting.Enabled.Should().BeTrue();
        await runtime.Starts.Should().BeEqualTo(1);
        await runtime.Stops.Should().BeEqualTo(0);
        await vm.NewCmd.Execute().ToTask();
        vm.Executable = "New.exe";
        vm.MatchByName = true;
        await vm.SaveRuleCmd.Execute().ToTask();
        await vm.Rules.Single().Should().BeEqualTo(savedRow);
        await (vm.SelectedRule == null).Should().BeTrue();
        await vm.Executable.Should().BeEqualTo("New.exe");
    }

    [Test]
    public async Task EditorsKeepTheirOwnConfigurationAndSavingPreservesRowOrder()
    {
        var first = new Config();
        var second = new Config();
        using var a = new AppRoutingViewModel(first, new FakeRuntime(), _ => Task.FromResult(0), true);
        using var b = new AppRoutingViewModel(second, new FakeRuntime(), _ => Task.FromResult(0), true);
        a.Executable = "First.exe";
        a.MatchByName = true;
        await a.SaveRuleCmd.Execute().ToTask();
        var row = a.SelectedRule;
        await a.NewCmd.Execute().ToTask();
        a.Executable = "Second.exe";
        a.MatchByName = true;
        await a.SaveRuleCmd.Execute().ToTask();
        a.SelectedRule = row;
        a.Executable = "Renamed.exe";
        await a.SaveRuleCmd.Execute().ToTask();
        await first.AppRouting.Rules.Select(r => r.ExecutablePath).SequenceEqual(new[] { "Renamed.exe", "Second.exe" }).Should().BeTrue();
        await second.AppRouting.Rules.Count.Should().BeEqualTo(0);
    }

    private sealed class FakeRuntime : IAppRoutingRuntime
    {
        public bool IsEnabled
        {
            get; private set;
        }

        public int Starts
        {
            get; private set;
        }
        public int Stops
        {
            get; private set;
        }
        public bool FailStart
        {
            get; set;
        }
        public Config? LastConfig
        {
            get; private set;
        }


        public Task StartAsync(Config config)
        {
            Starts++;
            if (FailStart)
            {
                throw new IOException("Fixture start failure");
            }

            LastConfig = JsonUtils.DeepCopy(config);
            IsEnabled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stops++;
            IsEnabled = false;
            return Task.CompletedTask;
        }
    }
}
