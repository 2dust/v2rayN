using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using ReactiveUI.Builder;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;
using ServiceLib.Tests.CoreConfig;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.ViewModels;

[NotInParallel]
public class EditorViewModelTests
{
    static EditorViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }

    [Test]
    public async Task NewProfile_SavePreservesExtrasNormalizationAndAssignedIdentity()
    {
        using var fixture = new EditorFixture();
        var input = new ProfileItem { ConfigType = EConfigType.VMess, Remarks = fixture.Name, Address = " original ", Port = 443, Password = "original" };
        var vm = new AddServerViewModel(input);
        vm.SelectedSource.Address = " edited.example ";
        vm.SelectedSource.Password = " edited-password ";
        await input.Address.Should().BeEqualTo(" original ");
        vm.Network = "invalid-network";
        vm.CoreType = nameof(ECoreType.Xray);
        vm.AllowInsecure = true;
        vm.MuxEnabled = true;
        vm.Cert = "edited-certificate";
        vm.CertSha = "edited-sha";
        vm.AlterId = 7;
        vm.Host = "edited-host";
        vm.Path = "/edited-path";
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;

        await Invoke(vm, "SaveServerAsync");
        var first = (await fixture.Profiles()).Single();
        await first.GetProtocolExtra().AlterId.Should().BeEqualTo("7");
        await first.GetTransportExtra().Host.Should().BeEqualTo("edited-host");
        await first.GetTransportExtra().Path.Should().BeEqualTo("/edited-path");
        await first.CoreType.Should().BeEqualTo(ECoreType.Xray);
        await first.AllowInsecure.Should().BeEqualTo(Global.StringTrue);
        await first.MuxEnabled.Should().BeTrue();
        await first.Cert.Should().BeEqualTo("edited-certificate");
        await first.CertSha.Should().BeEqualTo("edited-sha");
        await first.Network.Should().BeEqualTo(Global.DefaultNetwork);
        await vm.SelectedSource.Address.Should().BeEqualTo("edited.example");
        await vm.SelectedSource.Password.Should().BeEqualTo("edited-password");
        vm.SelectedSource.Port = 8443;
        await Invoke(vm, "SaveServerAsync");
        var second = (await fixture.Profiles()).Single();
        await second.IndexId.Should().BeEqualTo(first.IndexId);
        await second.Port.Should().BeEqualTo(8443);
        await closed.Should().BeEqualTo(2);
    }

    [Test]
    public async Task TransportFields_FollowCurrentNetworkSelection()
    {
        using var fixture = new EditorFixture();
        var vm = new AddServerViewModel(new ProfileItem { Remarks = fixture.Name, Network = "raw" });
        vm.Network = nameof(ETransport.grpc);
        vm.TransportHeaderType = "multi";
        vm.TransportHost = "grpc-authority";
        vm.TransportPath = "grpc-service";
        await vm.GrpcMode.Should().BeEqualTo("multi");
        await vm.GrpcAuthority.Should().BeEqualTo("grpc-authority");
        await vm.GrpcServiceName.Should().BeEqualTo("grpc-service");
        vm.Network = nameof(ETransport.xhttp);
        vm.TransportExtraText = "{\"extra\":true}";
        await vm.XhttpExtra.Should().BeEqualTo("{\"extra\":true}");
    }

    [Test]
    public async Task InvalidProfileHeaders_DoNotSaveOrClose()
    {
        using var fixture = new EditorFixture();
        var input = new ProfileItem { ConfigType = EConfigType.HTTP, Remarks = fixture.Name, Address = "original", Port = 80 };
        var vm = new AddServerViewModel(input) { HttpHeadersJson = "invalid-json" };
        vm.SelectedSource.Address = "edited";
        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        await Invoke(vm, "SaveServerAsync");

        await input.Address.Should().BeEqualTo("original");
        await (await fixture.Profiles()).Count.Should().BeEqualTo(0);
        await closed.Should().BeFalse();
    }

    [Test]
    public async Task AliasOnlySubscription_SavePreservesSelectionsSortAndAssignedIdentity()
    {
        using var fixture = new EditorFixture();
        var input = new SubItem { Remarks = fixture.Name, Url = "", UpdateTime = 123 };
        var vm = new SubEditViewModel(input) { CustomCoreType = nameof(ECoreType.sing_box), PrevProfile = "previous", NextProfile = "next" };
        vm.SelectedSource.Memo = "edited-memo";
        await input.Memo.Should().BeEqualTo(null);

        await Invoke(vm, "SaveSubAsync");
        var first = (await fixture.Subscriptions()).Single();
        await first.CustomCoreType.Should().BeEqualTo(ECoreType.sing_box);
        await first.PrevProfile.Should().BeEqualTo("previous");
        await first.NextProfile.Should().BeEqualTo("next");
        await first.UpdateTime.Should().BeEqualTo(123L);
        await (first.Sort > 0).Should().BeTrue();
        await vm.SelectedSource.Sort.Should().BeEqualTo(first.Sort);
        vm.SelectedSource.Memo = "second-save";
        await Invoke(vm, "SaveSubAsync");
        var second = (await fixture.Subscriptions()).Single();
        await second.Id.Should().BeEqualTo(first.Id);
        await second.Memo.Should().BeEqualTo("second-save");
    }

    [Test]
    [Arguments("https://example.com/sub", "not-json")]
    [Arguments("invalid-url", "{}")]
    public async Task InvalidSubscription_DoNotSaveOrClose(string url, string headers)
    {
        using var fixture = new EditorFixture();
        var input = new SubItem { Remarks = fixture.Name, Url = "original", RequestHeaders = "{}" };
        var vm = new SubEditViewModel(input);
        vm.SelectedSource.Url = url;
        vm.SelectedSource.RequestHeaders = headers;
        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        await Invoke(vm, "SaveSubAsync");

        await input.Url.Should().BeEqualTo("original");
        await (await fixture.Subscriptions()).Count.Should().BeEqualTo(0);
        await closed.Should().BeFalse();
    }

    [Test]
    public async Task PublicHttpSubscription_WarningStillAllowsSave()
    {
        using var fixture = new EditorFixture();
        var vm = new SubEditViewModel(new SubItem { Remarks = fixture.Name, Url = "http://example.com/sub" });
        await Invoke(vm, "SaveSubAsync");
        await (await fixture.Subscriptions()).Single().Url.Should().BeEqualTo("http://example.com/sub");
    }

    [Test]
    public async Task Browse_ReplacesEditorAndSaveUsesImportedIdentityAndCurrentValues()
    {
        using var fixture = new EditorFixture();
        var input = new ProfileItem { ConfigType = EConfigType.Custom, Remarks = fixture.Name };
        var vm = new AddServer2ViewModel(input);
        var originalEditor = vm.SelectedSource;
        var addresses = new List<string>();
        using var observation = vm.WhenAnyValue(x => x.SelectedSource.Address).Subscribe(value => addresses.Add(value));
        var sourceFile = Utils.GetTempPath($"{fixture.Name}.json");
        await File.WriteAllTextAsync(sourceFile, "{}");
        try
        {
            await vm.BrowseServer(sourceFile);
            var imported = (await fixture.Profiles()).Single();
            await ReferenceEquals(originalEditor, vm.SelectedSource).Should().BeFalse();
            await input.Address.Should().BeEqualTo(string.Empty);
            await vm.SelectedSource.Address.Should().BeEqualTo(imported.Address);
            vm.SelectedSource.DisplayLog = true;
            vm.SelectedSource.PreSocksPort = 1081;
            vm.IsSingboxEndpoint = true;
            vm.SelectedSource.Address = imported.Address;
            await Invoke(vm, "SaveServerAsync");
            await Invoke(vm, "SaveServerAsync");
            var saved = (await fixture.Profiles()).Single();
            await saved.IndexId.Should().BeEqualTo(imported.IndexId);
            await saved.DisplayLog.Should().BeTrue();
            await saved.PreSocksPort.Should().BeEqualTo(1081);
            await saved.GetProtocolExtra().IsSingboxEndpoint.Should().BeEqualTo(true);
            vm.SelectedSource.Address = "replacement-edit";
            await addresses.Last().Should().BeEqualTo("replacement-edit");
            File.Delete(Utils.GetConfigPath(imported.Address));
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    [Test]
    public async Task Group_SaveUsesEditedValuesAndKeepsAssignedIdentity()
    {
        using var fixture = new EditorFixture();
        var input = new ProfileItem { ConfigType = EConfigType.ProxyChain, Remarks = fixture.Name };
        var child = new ProfileItem { IndexId = $"child-{Guid.NewGuid():N}", Remarks = fixture.Name };
        await SQLiteHelper.Instance.InsertAsync(child);
        input.SetProtocolExtra(new ProtocolExtraItem { ChildItems = child.IndexId });
        var vm = new AddGroupServerViewModel(input) { CoreType = nameof(ECoreType.sing_box) };
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.ChildItemsObs.CollectionChanged += (_, _) => initialized.TrySetResult();
        if (vm.ChildItemsObs.Count == 0) await initialized.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.Filter = "edited-filter";
        vm.SelectedSource.Address = "edited";
        await input.Address.Should().BeEqualTo(string.Empty);

        await Invoke(vm, "SaveServerAsync");
        var first = (await fixture.Profiles()).Single(x => x.ConfigType == EConfigType.ProxyChain);
        await first.GetProtocolExtra().ChildItems.Should().BeEqualTo(child.IndexId);
        await first.GetProtocolExtra().Filter.Should().BeEqualTo("edited-filter");
        await first.CoreType.Should().BeEqualTo(ECoreType.sing_box);
        await Invoke(vm, "SaveServerAsync");
        await (await fixture.Profiles()).Single(x => x.ConfigType == EConfigType.ProxyChain).IndexId.Should().BeEqualTo(first.IndexId);
    }

    [Test]
    public async Task Routing_ImportsEditedUrlAndSaveKeepsRulesAndAssignedIdentity()
    {
        using var fixture = new EditorFixture();
        await using var server = new HttpServer("[{\"outboundTag\":\"proxy\",\"domain\":[\"example.com\"]}]");
        var input = new RoutingItem { Remarks = fixture.Name, Url = "http://127.0.0.1:1/stale", Locked = true };
        var vm = new RoutingRuleSettingViewModel(input);
        vm.SelectedRouting.Url = server.Url + "current-rules";
        using var confirm = vm.ShowYesNoInteraction.RegisterHandler(context => context.SetOutput(true));

        await Invoke(vm, "ImportRulesFromUrl");
        await server.Requests.Single().Should().BeEqualTo("GET /current-rules HTTP/1.1");
        await vm.RulesItems.Count.Should().BeEqualTo(1);
        await input.Url.Should().BeEqualTo("http://127.0.0.1:1/stale");
        await Invoke(vm, "SaveRoutingAsync");
        var first = (await fixture.Routings()).Single();
        await first.Locked.Should().BeTrue();
        await first.RuleNum.Should().BeEqualTo(1);
        var rule = JsonUtils.Deserialize<List<RulesItem>>(first.RuleSet)!.Single();
        await string.IsNullOrEmpty(rule.Id).Should().BeFalse();
        await rule.Domain!.Single().Should().BeEqualTo("example.com");
        vm.SelectedRouting.DomainStrategy = "IPIfNonMatch";
        await Invoke(vm, "SaveRoutingAsync");
        var second = (await fixture.Routings()).Single();
        await second.Id.Should().BeEqualTo(first.Id);
        await second.DomainStrategy.Should().BeEqualTo("IPIfNonMatch");
    }

    [Test]
    [Arguments("FetchCert", false)]
    [Arguments("FetchCertChain", false)]
    [Arguments("FetchCert", true)]
    [Arguments("FetchCertChain", true)]
    public async Task CertificateAction_ConnectsUsingCurrentAddressPortAndSni(string method, bool useTransportHost)
    {
        using var fixture = new EditorFixture();
        await using var server = new TlsServer();
        var input = new ProfileItem { Remarks = fixture.Name, Address = "127.0.0.2", Port = 1, StreamSecurity = "", Sni = "stale.example" };
        var vm = new AddServerViewModel(input);
        vm.SelectedSource.StreamSecurity = Global.StreamSecurity;
        vm.SelectedSource.Address = "127.0.0.1";
        vm.SelectedSource.Port = server.Port;
        vm.SelectedSource.Sni = useTransportHost ? "" : "current.example";
        vm.Network = nameof(ETransport.grpc);
        vm.GrpcAuthority = "current.example";

        await Invoke(vm, method);

        await (await server.ServerName.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeEqualTo("current.example");
        await input.Port.Should().BeEqualTo(1);
    }

    [Test]
    public async Task WebDavCheck_CommitsPlainSettingsBeforeConnectionAndKeepsFutureEditsDetached()
    {
        using var fixture = new EditorFixture();
        var configPath = Utils.GetConfigPath("guiNConfig.json");
        var committedBeforeRequest = false;
        await using var server = new HttpServer("", () =>
        {
            var saved = JsonUtils.Deserialize<Config>(File.ReadAllText(configPath));
            committedBeforeRequest = saved?.WebDavItem.DirName == "edited-directory" && fixture.Config.WebDavItem.DirName == "edited-directory";
        });
        var original = fixture.Config.WebDavItem;
        var vm = new BackupAndRestoreViewModel();
        vm.SelectedSource.Url = server.Url;
        vm.SelectedSource.UserName = "test-user";
        vm.SelectedSource.Password = "test-password";
        vm.SelectedSource.DirName = "edited-directory";
        await original.DirName.Should().BeEqualTo("original-directory");

        await Invoke(vm, "WebDavCheck");

        await committedBeforeRequest.Should().BeTrue();
        await fixture.Config.WebDavItem.GetType().Should().BeEqualTo(typeof(WebDavItem));
        await server.Requests.Any(x => x.StartsWith("PUT /edited-directory/readme_test ")).Should().BeTrue();
        vm.SelectedSource.DirName = "unsaved";
        await fixture.Config.WebDavItem.DirName.Should().BeEqualTo("edited-directory");
    }

    [Test]
    [Arguments(EConfigType.VMess, true)]
    [Arguments(EConfigType.Custom, true)]
    [Arguments(EConfigType.ProxyChain, true)]
    [Arguments(EConfigType.VMess, false)]
    [Arguments(EConfigType.Custom, false)]
    [Arguments(EConfigType.ProxyChain, false)]
    public async Task AddServerDialog_UsesSavedIdentityForReloadDecision(EConfigType configType, bool makeSavedProfileActive)
    {
        using var fixture = new EditorFixture();
        using var main = new MainWindowFixture(fixture.Config);
        fixture.Config.SubIndexId = fixture.Name;
        var active = new ProfileItem { IndexId = $"active-{fixture.Name}", Remarks = fixture.Name, Subid = fixture.Name, Address = "active.example", Port = 443 };
        await SQLiteHelper.Instance.InsertAsync(active);
        fixture.Config.IndexId = active.IndexId;
        string? savedId = null;
        var previousDialog = AppManager.Instance.WindowDialog;
        AppManager.Instance.WindowDialog = new DialogStub(async dialog =>
        {
            switch (dialog)
            {
                case AddServerViewModel profile:
                    profile.SelectedSource.Remarks = fixture.Name;
                    profile.SelectedSource.Address = "edited.example";
                    profile.SelectedSource.Port = 443;
                    profile.SelectedSource.Password = "edited-password";
                    await Invoke(profile, "SaveServerAsync");
                    savedId = profile.SelectedSource.ToEntity().IndexId;
                    break;
                case AddServer2ViewModel custom:
                    custom.SelectedSource.Remarks = fixture.Name;
                    var file = Utils.GetTempPath($"{fixture.Name}-dialog.json");
                    await File.WriteAllTextAsync(file, "{}");
                    try
                    {
                        await custom.BrowseServer(file);
                        await Invoke(custom, "SaveServerAsync");
                        savedId = custom.SelectedSource.ToEntity().IndexId;
                        File.Delete(Utils.GetConfigPath(custom.SelectedSource.Address));
                    }
                    finally { File.Delete(file); }
                    break;
                case AddGroupServerViewModel group:
                    group.SelectedSource.Remarks = fixture.Name;
                    group.ChildItemsObs.Add(active);
                    await Invoke(group, "SaveServerAsync");
                    savedId = group.SelectedSource.ToEntity().IndexId;
                    break;
                default:
                    throw new InvalidOperationException("Unexpected dialog.");
            }
            if (makeSavedProfileActive) await ConfigHandler.SetDefaultServerIndex(fixture.Config, savedId);
            return true;
        });
        try
        {
            await main.ViewModel.AddServerAsync(configType).WaitAsync(TimeSpan.FromSeconds(20));

            await string.IsNullOrEmpty(savedId).Should().BeFalse();
            await (await fixture.Profiles()).Count.Should().BeEqualTo(2);
            await main.ViewModel.ProfilesViewModel.ProfileItems.Any(item => item.IndexId == savedId).Should().BeTrue();
            await main.RefreshCount.Should().BeEqualTo(1);
            await main.ReloadQueued.Should().BeEqualTo(makeSavedProfileActive);
        }
        finally { AppManager.Instance.WindowDialog = previousDialog; }
    }

    [Test]
    [Arguments(EConfigType.VMess)]
    [Arguments(EConfigType.Custom)]
    [Arguments(EConfigType.ProxyChain)]
    public async Task AddServerDialog_CancelDoesNotPersistRefreshOrReload(EConfigType configType)
    {
        using var fixture = new EditorFixture();
        using var main = new MainWindowFixture(fixture.Config);
        var previousDialog = AppManager.Instance.WindowDialog;
        AppManager.Instance.WindowDialog = new DialogStub(dialog =>
        {
            switch (dialog)
            {
                case AddServerViewModel profile: profile.SelectedSource.Remarks = fixture.Name; break;
                case AddServer2ViewModel custom: custom.SelectedSource.Remarks = fixture.Name; break;
                case AddGroupServerViewModel group: group.SelectedSource.Remarks = fixture.Name; break;
            }
            return Task.FromResult(false);
        });
        try
        {
            await main.ViewModel.AddServerAsync(configType).WaitAsync(TimeSpan.FromSeconds(20));
            await (await fixture.Profiles()).Count.Should().BeEqualTo(0);
            await fixture.Config.IndexId.Should().BeEqualTo(string.Empty);
            await main.RefreshCount.Should().BeEqualTo(0);
            await main.ReloadQueued.Should().BeFalse();
        }
        finally { AppManager.Instance.WindowDialog = previousDialog; }
    }

    [Test]
    [Arguments("RemoteRestore")]
    [Arguments("RemoteBackup")]
    public async Task RemoteOperation_UsesCommittedConfigWithoutApplyingUnsavedEditor(string method)
    {
        using var fixture = new EditorFixture();
        await using var server = new HttpServer("");
        var vm = new BackupAndRestoreViewModel();
        vm.SelectedSource.Url = server.Url;
        vm.SelectedSource.UserName = "unsaved-user";
        vm.SelectedSource.Password = "unsaved-password";
        await Invoke(vm, method);
        await server.Requests.Count.Should().BeEqualTo(0);
        await fixture.Config.WebDavItem.Password.Should().BeEqualTo(null);
    }

    private static async Task Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(T viewModel, string method)
    {
        var task = (Task)typeof(T).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewModel, null)!;
        await task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private sealed class DialogStub(Func<object, Task<bool>> show) : IWindowDialog
    {
        public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm) where TViewModel : class => show(vm);
    }

    private sealed class MainWindowFixture : IDisposable
    {
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private readonly SemaphoreSlim _reloadGate = new(0, 1);
        private readonly IDisposable _refreshHandler;
        public MainWindowViewModel ViewModel { get; }
        public int RefreshCount { get; private set; }
        public bool ReloadQueued => (bool)typeof(MainWindowViewModel).GetField("_hasNextReloadJob", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ViewModel)!;

        public MainWindowFixture(Config config)
        {
            // Constructor startup registers jobs and touches core/proxy services. This fixture
            // initializes only fields used by the real AddServerAsync and refresh paths.
            ViewModel = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
            var profiles = (ProfilesViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ProfilesViewModel));
            var status = (StatusBarViewModel)RuntimeHelpers.GetUninitializedObject(typeof(StatusBarViewModel));
            typeof(MyReactiveObject).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, config);
            typeof(ProfilesViewModel).GetField("<ProfileItems>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(profiles, new BulkObservableCollection<ProfileItemModel>());
            var refresh = new Interaction<RxVoid, RxVoid>();
            typeof(ProfilesViewModel).GetField("<DispatcherRefreshServersBizInteraction>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(profiles, refresh);
            typeof(StatusBarViewModel).GetField("<Servers>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(status, new BulkObservableCollection<ComboItem>());
            typeof(MainWindowViewModel).GetField("<ProfilesViewModel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ViewModel, profiles);
            typeof(MainWindowViewModel).GetField("<StatusBarViewModel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ViewModel, status);
            typeof(MainWindowViewModel).GetField("_refreshServersSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ViewModel, _refreshGate);
            // Reload's existing overlapping-work guard records the request and returns
            // before any core startup or system-proxy operation can execute.
            typeof(MainWindowViewModel).GetField("_reloadSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ViewModel, _reloadGate);
            _refreshHandler = refresh.RegisterHandler(context => { RefreshCount++; context.SetOutput(RxVoid.Default); });
        }

        public void Dispose()
        {
            _refreshHandler.Dispose();
            _refreshGate.Dispose();
            _reloadGate.Dispose();
        }
    }

    private sealed class EditorFixture : IDisposable
    {
        private readonly FieldInfo _configField = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _viewConfigField = typeof(MyReactiveObject).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly FieldInfo _webDavConfigField = typeof(WebDavManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _certConfigField = typeof(CertPemManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly object? _oldConfig;
        private readonly object? _oldViewConfig;
        private readonly object? _oldWebDavConfig;
        private readonly object? _oldCertConfig;
        private readonly Dictionary<FieldInfo, object?> _oldWebDavState;
        private readonly Dictionary<FieldInfo, object?> _oldProfileExState;
        private readonly HashSet<string> _oldBackups;
        private readonly byte[]? _oldConfigFile;
        private readonly string _configPath;
        public string Name { get; } = $"editor-test-{Guid.NewGuid():N}";
        public Config Config { get; } = CoreConfigTestFactory.CreateConfig();

        public EditorFixture()
        {
            if (Environment.GetEnvironmentVariable(Global.LocalAppData) == "1")
                throw new InvalidOperationException("Editor tests require isolated application-output configuration.");
            _oldConfig = _configField.GetValue(AppManager.Instance);
            _oldViewConfig = _viewConfigField.GetValue(null);
            CoreConfigTestFactory.BindAppManagerConfig(Config);
            Config.WebDavItem = new WebDavItem { DirName = "original-directory" };
            _oldWebDavConfig = _webDavConfigField.GetValue(WebDavManager.Instance);
            _oldWebDavState = new[] { "_client", "_lastDescription", "_webDir" }
                .Select(name => typeof(WebDavManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!)
                .ToDictionary(field => field, field => field.GetValue(WebDavManager.Instance));
            typeof(WebDavManager).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(WebDavManager.Instance, null);
            _webDavConfigField.SetValue(WebDavManager.Instance, Config);
            _oldCertConfig = _certConfigField.GetValue(CertPemManager.Instance);
            _certConfigField.SetValue(CertPemManager.Instance, Config);
            _configPath = Utils.GetConfigPath("guiNConfig.json");
            _oldConfigFile = File.Exists(_configPath) ? File.ReadAllBytes(_configPath) : null;
            _oldBackups = Directory.GetFiles(Utils.GetBackupPath(""), "*.zip").ToHashSet();
            _oldProfileExState = new[] { "_lstProfileEx", "_queIndexIds" }
                .Select(name => typeof(ProfileExManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!)
                .ToDictionary(field => field, field => field.GetValue(ProfileExManager.Instance));
            typeof(ProfileExManager).GetField("_lstProfileEx", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ProfileExManager.Instance, new ConcurrentBag<ProfileExItem>());
            typeof(ProfileExManager).GetField("_queIndexIds", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ProfileExManager.Instance, new ConcurrentQueue<string>());
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            SQLiteHelper.Instance.CreateTable<SubItem>();
            SQLiteHelper.Instance.CreateTable<RoutingItem>();
        }

        public Task<List<ProfileItem>> Profiles() => SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(x => x.Remarks == Name).ToListAsync();
        public Task<List<SubItem>> Subscriptions() => SQLiteHelper.Instance.TableAsync<SubItem>().Where(x => x.Remarks == Name).ToListAsync();
        public Task<List<RoutingItem>> Routings() => SQLiteHelper.Instance.TableAsync<RoutingItem>().Where(x => x.Remarks == Name).ToListAsync();

        public void Dispose()
        {
            foreach (var item in Profiles().GetAwaiter().GetResult()) SQLiteHelper.Instance.DeleteAsync(item).GetAwaiter().GetResult();
            foreach (var item in Subscriptions().GetAwaiter().GetResult()) SQLiteHelper.Instance.DeleteAsync(item).GetAwaiter().GetResult();
            foreach (var item in Routings().GetAwaiter().GetResult()) SQLiteHelper.Instance.DeleteAsync(item).GetAwaiter().GetResult();
            if (_oldConfigFile == null) File.Delete(_configPath);
            else File.WriteAllBytes(_configPath, _oldConfigFile);
            foreach (var file in Directory.GetFiles(Utils.GetBackupPath(""), "*.zip").Except(_oldBackups)) File.Delete(file);
            var clientField = typeof(WebDavManager).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;
            (clientField.GetValue(WebDavManager.Instance) as IDisposable)?.Dispose();
            foreach (var (field, value) in _oldWebDavState) field.SetValue(WebDavManager.Instance, value);
            foreach (var (field, value) in _oldProfileExState) field.SetValue(ProfileExManager.Instance, value);
            _webDavConfigField.SetValue(WebDavManager.Instance, _oldWebDavConfig);
            _certConfigField.SetValue(CertPemManager.Instance, _oldCertConfig);
            _configField.SetValue(AppManager.Instance, _oldConfig);
            _viewConfigField.SetValue(null, _oldViewConfig);
        }
    }

    private sealed class HttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        public ConcurrentQueue<string> Requests { get; } = new();
        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

        public HttpServer(string body, Action? onRequest = null)
        {
            _listener.Start();
            _serverTask = Serve(body, onRequest);
        }

        private async Task Serve(string body, Action? onRequest)
        {
            var token = _cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(token);
                var length = 0;
                while (await reader.ReadLineAsync(token) is { Length: > 0 } line)
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line[15..]);
                if (length > 0)
                {
                    var buffer = new char[length];
                    var offset = 0;
                    while (offset < length)
                    {
                        var read = await reader.ReadAsync(buffer.AsMemory(offset), token);
                        if (read == 0) throw new EndOfStreamException();
                        offset += read;
                    }
                }
                Requests.Enqueue(request!);
                onRequest?.Invoke();
                var status = request!.StartsWith("GET ") ? "200 OK" : "201 Created";
                var response = $"HTTP/1.1 {status}\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response), token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            try { await _serverTask; }
            catch (OperationCanceledException) { }
            finally { _listener.Stop(); _cancellation.Dispose(); }
        }
    }

    private sealed class TlsServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly X509Certificate2 _certificate;
        private readonly Task _serverTask;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public TaskCompletionSource<string?> ServerName { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TlsServer()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=current.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            _listener.Start();
            _serverTask = Serve();
        }

        private async Task Serve()
        {
            using var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
            await using var stream = new SslStream(client.GetStream());
            try
            {
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificateSelectionCallback = (_, name) => { ServerName.TrySetResult(name); return _certificate; },
                }, _cancellation.Token);
            }
            catch (AuthenticationException) { /* The self-signed certificate is intentionally untrusted. */ }
            catch (IOException) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            try { await _serverTask; }
            catch (OperationCanceledException) { }
            finally { _listener.Stop(); _certificate.Dispose(); _cancellation.Dispose(); }
        }
    }
}
