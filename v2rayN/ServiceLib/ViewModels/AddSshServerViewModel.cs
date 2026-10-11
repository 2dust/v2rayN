namespace ServiceLib.ViewModels;

public partial class AddSshServerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;
    public Interaction<RxVoid, string?> BrowseKeyInteraction { get; } = new();
    public Interaction<string, bool> ConfirmHostKeyInteraction { get; } = new();
    public Interaction<SshKeySetupViewModel, SshSetupSelection?> SetupKeyInteraction { get; } = new();
    public ProfileItem SelectedSource { get; }
    [Reactive] public partial string Remarks { get; set; }
    [Reactive] public partial string Address { get; set; }
    [Reactive] public partial string Port { get; set; }
    [Reactive] public partial string Username { get; set; }
    [Reactive] public partial string Password { get; set; }
    [Reactive] public partial bool PrivateKeyAuth { get; set; }
    [Reactive] public partial string PrivateKeyPath { get; set; }
    [Reactive] public partial string Passphrase { get; set; }
    [Reactive] public partial string HostKey { get; set; }
    [Reactive] public partial string Status { get; set; }
    [Reactive] public partial bool IsBusy { get; set; }
    [Reactive] public partial bool IsTesting { get; set; }
    [Reactive] public partial string TestStatus { get; set; } = "";
    public ReactiveCommand<RxVoid, RxVoid> TestCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> StopTestCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FetchCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> TrustCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> BrowseCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SetupKeyCmd { get; }
    private readonly SshKeyStore _keyStore = new();
    private SshLocalKey? _setupKey;
    private SshKeySetupViewModel? _setupDialog;
    private CancellationTokenSource? _operation;
    private bool _closed;
    private string? _trustedAddress;
    private int? _trustedPort;
    private SshHostKey? _trustedKey;
    private readonly ISshConnectionTestService _testService;
    private long _testRevision;

    public AddSshServerViewModel(ProfileItem item, ISshConnectionTestService? testService = null)
    {
        _testService = testService ?? new SshConnectionTestService();
        _config = AppManager.Instance.Config;
        SelectedSource = item.IndexId.IsNullOrEmpty() ? item : JsonUtils.DeepCopy(item);
        var extra = item.GetProtocolExtra();
        Remarks = item.Remarks;
        Address = item.Address;
        Port = (item.Port == 0 ? 22 : item.Port).ToString();
        Username = item.Username;
        Password = item.Password;
        PrivateKeyAuth = extra.SshPrivateKeyAuth ?? false;
        PrivateKeyPath = extra.SshPrivateKeyPath ?? "";
        Passphrase = extra.SshPrivateKeyPassphrase ?? "";
        HostKey = extra.SshHostKey ?? "";
        Status = SshStrings.Unconfirmed;
        try
        {
            if (extra.SshTrustedAddress == Address && extra.SshTrustedPort == item.Port)
            {
                _trustedKey = SshProfileService.ParseHostKey(HostKey, extra.SshHostKeyAlgorithm);
                _trustedAddress = Address;
                _trustedPort = item.Port;
            }
        }
        catch { }
        this.WhenAnyValue(x => x.Address, x => x.Port, x => x.HostKey)
            .Subscribe(_ => RefreshTrust());
        this.WhenAnyValue(x => x.Address, x => x.Port, x => x.Username).Subscribe(_ => InvalidateTest());
        this.WhenAnyValue(x => x.Password, x => x.PrivateKeyAuth, x => x.PrivateKeyPath).Subscribe(_ => InvalidateTest());
        this.WhenAnyValue(x => x.Passphrase, x => x.HostKey).Subscribe(_ => InvalidateTest());
        TestCmd = ReactiveCommand.CreateFromTask(() => RunAsync(TestConnectionAsync));
        StopTestCmd = ReactiveCommand.Create(() => { if (IsTesting) _operation?.Cancel(); });
        SaveCmd = ReactiveCommand.CreateFromTask(() => RunAsync(SaveAsync));
        FetchCmd = ReactiveCommand.CreateFromTask(() => RunAsync(() => ConfirmAsync(true)));
        TrustCmd = ReactiveCommand.CreateFromTask(() => RunAsync(() => ConfirmAsync(false)));
        SetupKeyCmd = ReactiveCommand.CreateFromTask(() => RunAsync(SetupKeyAsync));
        BrowseCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (IsBusy) return;
            var path = await BrowseKeyInteraction.HandleSafe(RxVoid.Default);
            if (!_closed && !string.IsNullOrEmpty(path)) PrivateKeyPath = path;
        });
    }

    public void CancelOperation()
    {
        _closed = true;
        _operation?.Cancel();
        _setupDialog?.Cancel();
        Password = "";
        Passphrase = "";
    }

    private async Task SetupKeyAsync()
    {
        if (!PrivateKeyAuth) return;
        if (string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(Username)
            || !int.TryParse(Port, out var port) || port is < 1 or > 65535)
        { Status = SshStrings.InvalidEndpoint; return; }
        var address = Address.Trim();
        var username = Username.Trim();
        var profiles = await AppManager.Instance.ProfileItems("") ?? [];
        var paths = new[] { PrivateKeyPath }.Concat(profiles.Where(p => p.ConfigType == EConfigType.SSH)
            .Select(p => p.GetProtocolExtra().SshPrivateKeyPath)).ToArray();
        var candidates = await Task.Run(() => _keyStore.Discover(paths, Utils.GetHomePath()), _operation!.Token);
        if (_trustedKey == null) await ConfirmAsync(true);
        if (_closed || _trustedKey == null || address != Address.Trim() || username != Username.Trim() || port.ToString() != Port) return;
        var trusted = _trustedKey;
        _setupDialog = new(new(address, port, username, trusted), candidates, _keyStore);
        try
        {
            var result = await SetupKeyInteraction.HandleSafe(_setupDialog);
            if (_closed || result == null || _trustedKey != trusted || address != Address.Trim()
                || username != Username.Trim() || port.ToString() != Port) return;
            _setupKey = result.Key;
            PrivateKeyPath = result.Key.Path;
            Passphrase = result.Passphrase;
            PrivateKeyAuth = true;
            Password = "";
            Status = SshStrings.SetupVerified;
        }
        finally { _setupDialog = null; }
    }

    private void RefreshTrust()
    {
        if (_trustedAddress != Address.Trim() || _trustedPort?.ToString() != Port
            || _trustedKey?.PublicKey != HostKey)
        {
            _trustedKey = null;
        }
        Status = _trustedKey == null ? SshStrings.Unconfirmed
            : $"{SshStrings.Pinned}: {_trustedKey.Algorithm}\n{_trustedKey.Fingerprint}";
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy || _closed) return;
        IsBusy = true;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        try { await action(); }
        catch (OperationCanceledException) { Status = SshStrings.Cancelled; }
        catch { Status = SshStrings.FetchFailed; } // Never show exception text containing credentials.
        finally { _operation = null; IsTesting = false; IsBusy = false; }
    }

    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        InvalidateTest();
        if (SshProfileService.Validate(CreateDraftSnapshot(), requireTrust: false) is { } error)
        { Status = error; return; }
        if (_trustedKey == null) await ConfirmAsync(true);
        if (_closed || _trustedKey == null) return;
        var token = _operation!.Token;
        token.ThrowIfCancellationRequested();
        var snapshot = CreateDraftSnapshot();
        var url = _config.SpeedTestItem.SpeedPingTestUrl;
        var revision = _testRevision;
        var progress = new Progress<SshConnectionTestResult>(result =>
        {
            if (!_closed && IsTesting && revision == _testRevision) TestStatus = SshStrings.TestResult(result);
        });
        var result = await Task.Run(() => _testService.TestAsync(snapshot, url, progress, token));
        if (!_closed && revision == _testRevision) TestStatus = SshStrings.TestResult(result);
    }

    private void InvalidateTest()
    {
        _testRevision++;
        TestStatus = "";
    }

    private async Task ConfirmAsync(bool fetch)
    {
        var address = Address.Trim();
        if (address.Length == 0 || !int.TryParse(Port, out var port) || port is < 1 or > 65535)
        {
            Status = SshStrings.InvalidEndpoint;
            return;
        }
        var input = HostKey;
        SshHostKey key;
        if (fetch)
        {
            Status = SshStrings.Fetching;
            key = await SshProfileService.FetchHostKeyAsync(address, port, _operation!.Token);
        }
        else
        {
            try { key = SshProfileService.ParseHostKey(input); }
            catch { Status = SshStrings.InvalidHostKey; return; }
        }
        if (_closed || Address.Trim() != address || Port != port.ToString() || HostKey != input) return;
        var confirmed = await ConfirmHostKeyInteraction.HandleSafe(
            $"{SshStrings.ConfirmKey}\n\n{address}:{port}\n{key.Algorithm}\n{key.Fingerprint}");
        if (!confirmed || _closed || _operation!.IsCancellationRequested || Address.Trim() != address || Port != port.ToString() || HostKey != input)
        {
            RefreshTrust();
            return;
        }
        HostKey = key.PublicKey;
        _trustedKey = key;
        _trustedAddress = address;
        _trustedPort = port;
        RefreshTrust();
    }

    public ProfileItem CreateDraftSnapshot()
    {
        var snapshot = JsonUtils.DeepCopy(SelectedSource);
        ApplyDraft(snapshot);
        return snapshot;
    }

    private void UpdateSource() => ApplyDraft(SelectedSource);

    private void ApplyDraft(ProfileItem source)
    {
        source.ConfigType = EConfigType.SSH;
        source.Remarks = Remarks.Trim();
        source.Address = Address.Trim();
        source.Port = int.TryParse(Port, out var port) ? port : 0;
        source.Username = Username.Trim();
        source.Password = PrivateKeyAuth ? "" : Password;
        source.SetProtocolExtra(source.GetProtocolExtra() with
        {
            SshPrivateKeyAuth = PrivateKeyAuth,
            SshPrivateKeyPath = PrivateKeyAuth ? PrivateKeyPath : null,
            SshPrivateKeyPassphrase = PrivateKeyAuth ? Passphrase : null,
            SshHostKey = _trustedKey?.PublicKey,
            SshHostKeyAlgorithm = _trustedKey?.Algorithm,
            SshTrustedAddress = _trustedKey != null ? _trustedAddress : null,
            SshTrustedPort = _trustedKey != null ? _trustedPort : null,
        });
    }

    private async Task SaveAsync()
    {
        if (PrivateKeyAuth && _setupKey?.Path == PrivateKeyPath)
        {
            try { using var key = _keyStore.Open(_setupKey, Passphrase); }
            catch (SshSetupException e) { Status = SshStrings.SetupError(e.Code); return; }
        }
        UpdateSource();
        if (SshProfileService.Validate(SelectedSource, requireTrust: false) is { } error)
        {
            Status = error;
            return;
        }
        if (_trustedKey == null)
        {
            await ConfirmAsync(true);
            if (_trustedKey == null || _closed) return;
            UpdateSource();
        }
        if (SelectedSource.Remarks.Length == 0)
            SelectedSource.Remarks = $"{Username}@{Address}:{Port}";
        if (await ConfigHandler.AddServer(_config, SelectedSource) == 0 && !_closed)
        {
            if (PrivateKeyAuth && _setupKey?.Path == PrivateKeyPath)
            {
                try { _keyStore.SaveDefault(_setupKey); }
                catch { Status = SshStrings.DefaultKeySaveFailed; return; }
            }
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else Status = ResUI.OperationFailed;
    }
}
