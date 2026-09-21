using ServiceLib.Services.AppRouting;

namespace ServiceLib.ViewModels;

public sealed record AppRouteChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record AppRouteDestination(AppRouteKind Kind, string Label)
{
    public override string ToString() => Label;
}

public sealed class AppRouteRow(AppRouteRule rule, string destination) : ReactiveObject
{
    public AppRouteRule Rule { get; private set; } = rule;
    public string Destination { get; } = destination;
    public string Executable => Rule.ExecutablePath;
    public string Match => Rule.MatchByName ? ResUI.AppRoutingNameOnly : ResUI.AppRoutingFullPath;
    public bool Enabled => Rule.Enabled;
    public bool IncludeChildProcesses => Rule.IncludeChildProcesses;

    internal void UpdateRule(AppRouteRule value)
    {
        Rule = value;
        NotifyFlags();
    }

    internal void NotifyFlags()
    {
        this.RaisePropertyChanged(nameof(Enabled));
        this.RaisePropertyChanged(nameof(IncludeChildProcesses));
    }
}

public partial class AppRoutingViewModel : MyReactiveObject, IDisposable
{
    public ObservableCollection<AppRouteRow> Rules { get; } = [];
    public ObservableCollection<AppRouteChoice> Profiles { get; } = [];
    public ObservableCollection<AppRouteChoice> Interfaces { get; } = [];
    public ObservableCollection<AppRouteProcess> NetworkApps { get; } = [];
    public IReadOnlyList<AppRouteDestination> Destinations
    {
        get;
    } =
    [
        new(AppRouteKind.ActiveProfile, ResUI.AppRoutingActiveProfile),
        new(AppRouteKind.Profile, ResUI.AppRoutingProfile),
        new(AppRouteKind.Socks5, "SOCKS5"),
        new(AppRouteKind.Interface, ResUI.AppRoutingInterface)
    ];
    [Reactive]
    public partial AppRouteRow? SelectedRule
    {
        get; set;
    }
    [Reactive] public partial string Executable { get; set; } = "";
    [Reactive]
    public partial bool MatchByName
    {
        get; set;
    }
    [Reactive]
    public partial bool IncludeChildProcesses
    {
        get; set;
    }
    [Reactive] public partial bool Enabled { get; set; } = true;
    [Reactive]
    public partial AppRouteDestination? SelectedDestination
    {
        get; set;
    }
    [Reactive]
    public partial AppRouteChoice? SelectedProfile
    {
        get; set;
    }
    [Reactive]
    public partial bool ApplyBlockingRules { get; set; }
    [Reactive]
    public partial AppRouteChoice? SelectedInterface
    {
        get; set;
    }
    [Reactive] public partial string SocksHost { get; set; } = "127.0.0.1";
    [Reactive] public partial int SocksPort { get; set; } = 10808;
    [Reactive] public partial string SocksUsername { get; set; } = "";
    [Reactive] public partial string SocksPassword { get; set; } = "";
    [Reactive]
    public partial bool IsProfile
    {
        get; set;
    }
    [Reactive]
    public partial bool IsSocks
    {
        get; set;
    }
    [Reactive]
    public partial bool IsInterface
    {
        get; set;
    }
    [Reactive]
    public partial bool RoutingEnabled
    {
        get; set;
    }
    [Reactive]
    public partial bool IsBusy
    {
        get; set;
    }
    [Reactive] public partial bool CanEdit { get; set; } = true;
    [Reactive]
    public partial bool CanChangeRouting
    {
        get; set;
    }
    public bool IsAdministrator
    {
        get;
    }
    [Reactive]
    public partial bool IsLoadingApps
    {
        get; set;
    }
    [Reactive]
    public partial bool CanUseProcess
    {
        get; set;
    }
    [Reactive] public partial string ProcessSearch { get; set; } = "";
    [Reactive] public partial string ProcessStatus { get; set; } = "";
    [Reactive]
    public partial AppRouteProcess? SelectedProcess
    {
        get; set;
    }
    public Interaction<RxVoid, string?> BrowseExecutable { get; } = new();
    public Interaction<RxVoid, AppRouteProcess?> PickProcess { get; } = new();
    public ReactiveCommand<RxVoid, RxVoid> BrowseCmd
    {
        get;
    }
    public ReactiveCommand<RxVoid, RxVoid> NewCmd
    {
        get;
    }
    public ReactiveCommand<RxVoid, RxVoid> SaveRuleCmd
    {
        get;
    }
    public ReactiveCommand<RxVoid, RxVoid> DeleteCmd
    {
        get;
    }
    public ReactiveCommand<bool, RxVoid> ChangeRoutingCmd
    {
        get;
    }
    public ReactiveCommand<RxVoid, RxVoid> RefreshProcessesCmd
    {
        get;
    }
    public ReactiveCommand<RxVoid, RxVoid> PickProcessCmd
    {
        get;
    }
    public ReactiveCommand<AppRouteRow, RxVoid> ToggleRuleEnabledCmd
    {
        get;
    }
    public ReactiveCommand<AppRouteRow, RxVoid> ToggleRuleChildrenCmd
    {
        get;
    }
    private readonly List<IDisposable> _subscriptions = [];
    private readonly IAppRoutingRuntime _runtime;
    private readonly Config _routingConfig;
    private readonly Func<Config, Task<int>> _saveConfiguration;
    private List<AppRouteProcess> _networkApps = [];
    private bool _syncRuntime;
    private bool _disposed;

    public AppRoutingViewModel() : this(AppManager.Instance.Config, AppRoutingManager.Instance, ConfigHandler.SaveConfig, Utils.IsAdministrator()) { }

    internal AppRoutingViewModel(Config config, IAppRoutingRuntime runtime, Func<Config, Task<int>> saveConfiguration, bool isAdministrator)
    {
        _routingConfig = config;
        _runtime = runtime;
        _saveConfiguration = saveConfiguration;
        IsAdministrator = isAdministrator;
        var canEdit = this.WhenAnyValue(vm => vm.IsBusy).Select(busy => !busy);
        BrowseCmd = ReactiveCommand.CreateFromTask(() => Guard(async () =>
        {
            var path = await BrowseExecutable.HandleSafe(RxVoid.Default);
            if (path != null)
            {
                Executable = AppRouteMatcher.Normalize(path, MatchByName);
            }
        }), canEdit);
        NewCmd = ReactiveCommand.Create(() => { SelectedRule = null; Load(null); }, canEdit);
        SaveRuleCmd = ReactiveCommand.CreateFromTask(() => RunBusy(SaveRule), canEdit);
        DeleteCmd = ReactiveCommand.CreateFromTask(() => RunBusy(DeleteRule), canEdit);
        ChangeRoutingCmd = ReactiveCommand.CreateFromTask<bool>(value => RunBusy(async () =>
        {
            if (!IsAdministrator)
            {
                return;
            }

            await AppRoutingLifecycle.SetEnabledAsync(_routingConfig, _runtime, _saveConfiguration, value);
        }), canEdit.Select(editable => editable && IsAdministrator));
        ToggleRuleEnabledCmd = ReactiveCommand.CreateFromTask<AppRouteRow>(row => RunBusy(() => ToggleRuleFlag(row, false)), canEdit);
        ToggleRuleChildrenCmd = ReactiveCommand.CreateFromTask<AppRouteRow>(row => RunBusy(() => ToggleRuleFlag(row, true)), canEdit);
        RefreshProcessesCmd = ReactiveCommand.CreateFromTask(RefreshProcesses,
            this.WhenAnyValue(vm => vm.IsLoadingApps).Select(loading => !loading));
        PickProcessCmd = ReactiveCommand.CreateFromTask(() => Guard(async () =>
        {
            ProcessSearch = "";
            SelectedProcess = null;
            var process = await PickProcess.HandleSafe(RxVoid.Default);
            if (process == null)
            {
                return;
            }

            if (process.Path == null)
            {
                MatchByName = true;
            }

            Executable = AppRouteMatcher.Normalize(process.Path ?? process.Name, MatchByName);
        }), canEdit);
        _subscriptions.Add(this.WhenAnyValue(vm => vm.SelectedRule).Subscribe(row => Load(row?.Rule)));
        _subscriptions.Add(this.WhenAnyValue(vm => vm.SelectedDestination).Subscribe(destination =>
        {
            IsProfile = destination?.Kind == AppRouteKind.Profile;
            IsSocks = destination?.Kind == AppRouteKind.Socks5;
            IsInterface = destination?.Kind == AppRouteKind.Interface;
        }));
        _subscriptions.Add(this.WhenAnyValue(vm => vm.IsBusy).Subscribe(busy =>
        {
            CanEdit = !busy;
            CanChangeRouting = !busy && IsAdministrator;
        }));
        _subscriptions.Add(this.WhenAnyValue(vm => vm.ProcessSearch).Subscribe(_ => FilterProcesses()));
        _subscriptions.Add(this.WhenAnyValue(vm => vm.SelectedProcess, vm => vm.IsLoadingApps,
            (process, loading) => process != null && !loading).Subscribe(value => CanUseProcess = value));
        _subscriptions.Add(this.WhenAnyValue(vm => vm.RoutingEnabled).Skip(1).Where(_ => !_syncRuntime).InvokeCommand(ChangeRoutingCmd));
        SyncSwitch();
    }

    public async Task Initialize()
    {
        await Guard(async () =>
        {
            await Refresh();
            foreach (var rule in JsonUtils.DeepCopy(_routingConfig.AppRouting.Rules))
            {
                Rules.Add(ToRow(rule));
            }
        });
    }

    private async Task Refresh()
    {
        var profileId = SelectedProfile?.Id;
        var interfaceId = SelectedInterface?.Id;
        Profiles.Clear();
        Interfaces.Clear();
        foreach (var profile in (await AppManager.Instance.ProfileItems("") ?? []).Where(p => p.ConfigType != EConfigType.Custom))
        {
            Profiles.Add(new(profile.IndexId, profile.GetSummary()));
        }

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback))
        {
            Interfaces.Add(new(adapter.Id, $"{adapter.Name} — {adapter.OperationalStatus}"));
        }

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profileId);
        SelectedInterface = Interfaces.FirstOrDefault(p => p.Id == interfaceId);
    }

    private void Load(AppRouteRule? rule)
    {
        rule ??= new()
        {
            Kind = AppRouteKind.ActiveProfile
        };
        Executable = rule.ExecutablePath;
        MatchByName = rule.MatchByName;
        IncludeChildProcesses = rule.IncludeChildProcesses;
        Enabled = rule.Enabled;
        SelectedDestination = Destinations.FirstOrDefault(d => d.Kind == rule.Kind);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == rule.ProfileId);
        ApplyBlockingRules = rule.ApplyBlockingRules;
        SelectedInterface = Interfaces.FirstOrDefault(p => p.Id == rule.InterfaceId);
        SocksHost = rule.SocksHost;
        SocksPort = rule.SocksPort;
        SocksUsername = rule.SocksUsername;
        SocksPassword = rule.SocksPassword;
    }

    private async Task SaveRule()
    {
        if (SelectedDestination == null)
        {
            throw new ArgumentException("Select an application route type.");
        }

        var rule = new AppRouteRule
        {
            Id = SelectedRule?.Rule.Id ?? Guid.NewGuid().ToString("N"),
            ExecutablePath = AppRouteMatcher.Normalize(Executable, MatchByName),
            MatchByName = MatchByName,
            IncludeChildProcesses = IncludeChildProcesses,
            Enabled = Enabled,
            Kind = SelectedDestination.Kind,
            ProfileId = SelectedProfile?.Id ?? "",
            ApplyBlockingRules = ApplyBlockingRules,
            InterfaceId = SelectedInterface?.Id ?? "",
            SocksHost = SocksHost.Trim(),
            SocksPort = SocksPort,
            SocksUsername = SocksUsername,
            SocksPassword = SocksPassword
        };
        var row = ToRow(rule);
        var previous = SelectedRule;
        var updated = previous == null ? Rules.Select(r => r.Rule).Append(rule) : Rules.Select(r => r == previous ? rule : r.Rule);
        await SaveAndApply(updated, () =>
        {
            if (previous == null)
            {
                Rules.Add(row);
            }
            else
            {
                Rules[Rules.IndexOf(previous)] = row;
            }

            SelectedRule = row;
        });
    }

    private async Task DeleteRule()
    {
        if (SelectedRule is not { } row)
        {
            return;
        }

        await SaveAndApply(Rules.Where(r => r != row).Select(r => r.Rule), () =>
        {
            Rules.Remove(row);
            SelectedRule = null;
            Load(null);
        });
    }

    private async Task ToggleRuleFlag(AppRouteRow row, bool children)
    {
        if (!Rules.Contains(row))
        {
            return;
        }

        try
        {
            var rule = JsonUtils.DeepCopy(row.Rule);
            if (children)
            {
                rule.IncludeChildProcesses = !rule.IncludeChildProcesses;
            }
            else
            {
                rule.Enabled = !rule.Enabled;
            }

            await SaveAndApply(Rules.Select(r => r == row ? rule : r.Rule), () =>
            {
                row.UpdateRule(rule);
                if (SelectedRule == row)
                {
                    if (children)
                    {
                        IncludeChildProcesses = rule.IncludeChildProcesses;
                    }
                    else
                    {
                        Enabled = rule.Enabled;
                    }
                }
            });
        }
        finally { row.NotifyFlags(); }
    }

    private async Task SaveAndApply(IEnumerable<AppRouteRule> rules, Action updateEditor)
    {
        var updated = rules.ToList();
        AppRoutingManager.Validate(updated);
        var previous = _routingConfig.AppRouting;
        _routingConfig.AppRouting = new()
        {
            Enabled = previous.Enabled && (!IsAdministrator || updated.Any(r => r.Enabled)),
            Rules = JsonUtils.DeepCopy(updated)
        };
        try
        {
            if (await _saveConfiguration(_routingConfig) != 0)
            {
                throw new IOException(ResUI.OperationFailed);
            }
        }
        catch
        {
            _routingConfig.AppRouting = previous;
            throw;
        }
        // Commit the visible rows only after persistence succeeds. A runtime failure
        // must keep the saved edits, whereas a save failure leaves the draft intact.
        updateEditor();
        if (previous.Enabled && IsAdministrator)
        {
            if (_routingConfig.AppRouting.Enabled)
            {
                await _runtime.StartAsync(_routingConfig);
            }
            else
            {
                await _runtime.StopAsync();
            }
        }
    }

    private AppRouteRow ToRow(AppRouteRule rule) => new(rule, rule.Kind switch
    {
        AppRouteKind.ActiveProfile => ResUI.AppRoutingActiveProfile,
        AppRouteKind.Profile => Profiles.FirstOrDefault(p => p.Id == rule.ProfileId)?.Label ?? rule.ProfileId,
        AppRouteKind.Interface => Interfaces.FirstOrDefault(p => p.Id == rule.InterfaceId)?.Label ?? rule.InterfaceId,
        _ => $"{rule.SocksHost}:{rule.SocksPort}"
    });

    private async Task RefreshProcesses()
    {
        IsLoadingApps = true;
        ProcessStatus = ResUI.AppRoutingLoadingApps;
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException();
            }

            var apps = await Task.Run(AppRouteProcessCatalog.Read);
            if (_disposed)
            {
                return;
            }

            _networkApps = apps;
            FilterProcesses();
            ProcessStatus = apps.Count == 0 ? ResUI.AppRoutingNoNetworkApps : ResUI.AppRoutingNetworkAppsHelp;
        }
        catch (Exception ex) { ProcessStatus = ex.Message; }
        finally { IsLoadingApps = false; }
    }

    private void FilterProcesses()
    {
        var pid = SelectedProcess?.Pid;
        NetworkApps.Clear();
        foreach (var process in _networkApps.Where(p => p.MatchesSearch(ProcessSearch.Trim())))
        {
            NetworkApps.Add(process);
        }

        SelectedProcess = NetworkApps.FirstOrDefault(p => p.Pid == pid);
    }

    private async Task RunBusy(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Guard(action);
        }
        finally { IsBusy = false; SyncSwitch(); }
    }

    private async Task Guard(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) { Logging.SaveLog("AppRouting", ex); NoticeManager.Instance.Enqueue(ex.Message); }
    }

    private void SyncSwitch()
    {
        _syncRuntime = true;
        try
        {
            RoutingEnabled = _routingConfig.AppRouting.Enabled;
        }
        finally { _syncRuntime = false; }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        BrowseCmd.Dispose();
        NewCmd.Dispose();
        SaveRuleCmd.Dispose();
        DeleteCmd.Dispose();
        RefreshProcessesCmd.Dispose();
        PickProcessCmd.Dispose();
        ChangeRoutingCmd.Dispose();
        ToggleRuleEnabledCmd.Dispose();
        ToggleRuleChildrenCmd.Dispose();
    }
}
