namespace ServiceLib.ViewModels;

public sealed class SshSetupSelection(SshLocalKey key, string passphrase)
{
    public SshLocalKey Key { get; } = key;
    public string Passphrase { get; } = passphrase;
}

public partial class SshKeySetupViewModel : MyReactiveObject
{
    public event EventHandler? RequestClose;
    public Interaction<RxVoid, string?> BrowseInteraction { get; } = new();
    public ObservableCollection<SshLocalKey> Candidates { get; } = [];
    public string TargetDescription { get; }
    public string DiscoveryMessage => Candidates.Count > 0 ? SshStrings.KeysFound : SshStrings.NoKeysFound;
    public SshSetupSelection? Result { get; private set; }
    [Reactive] public partial SshLocalKey? SelectedKey { get; set; }
    [Reactive] public partial bool GenerateNew { get; set; }
    [Reactive] public partial string KeyPassphrase { get; set; } = "";
    [Reactive] public partial string ConfirmPassphrase { get; set; } = "";
    [Reactive] public partial string InstallationPassword { get; set; } = "";
    [Reactive] public partial bool NeedsPassword { get; set; }
    [Reactive] public partial bool IsBusy { get; set; }
    [Reactive] public partial string Status { get; set; } = "";
    [Reactive] public partial string PublicKeyText { get; set; } = "";
    public ReactiveCommand<RxVoid, RxVoid> RunCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> BrowseCmd { get; }
    private readonly SshSetupTarget _target;
    private readonly SshKeyStore _store;
    private CancellationTokenSource? _operation;
    private bool _closing;

    public SshKeySetupViewModel(SshSetupTarget target, IEnumerable<SshLocalKey> candidates, SshKeyStore store)
    {
        _target = target;
        _store = store;
        TargetDescription = $"{target.Username}@{target.Address}:{target.Port}\n{target.HostKey.Fingerprint}";
        foreach (var key in candidates) Candidates.Add(key);
        SelectedKey = Candidates.FirstOrDefault();
        GenerateNew = Candidates.Count == 0;
        this.WhenAnyValue(x => x.GenerateNew, x => x.SelectedKey).Subscribe(_ =>
        {
            NeedsPassword = GenerateNew;
            InstallationPassword = "";
            KeyPassphrase = "";
            ConfirmPassphrase = "";
            PublicKeyText = GenerateNew ? "" : SelectedKey?.PublicKey ?? "";
            Status = "";
        });
        RunCmd = ReactiveCommand.CreateFromTask(RunAsync);
        BrowseCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            if (IsBusy || _closing) return;
            var path = await BrowseInteraction.HandleSafe(RxVoid.Default);
            if (_closing || path == null) return;
            var key = await Task.Run(() => _store.Inspect(path));
            if (_closing) return;
            if (key == null) { Status = SshStrings.SetupError("invalidKey"); return; }
            var existing = Candidates.FirstOrDefault(k => k.Path == key.Path);
            if (existing == null) Candidates.Add(key);
            GenerateNew = false;
            SelectedKey = existing ?? key;
        });
    }

    public void Cancel()
    {
        _closing = true;
        InstallationPassword = "";
        KeyPassphrase = "";
        ConfirmPassphrase = "";
        if (_operation != null) _operation.Cancel();
        else RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync()
    {
        if (IsBusy || _closing) return;
        if (GenerateNew && KeyPassphrase != ConfirmPassphrase) { Status = SshStrings.SetupError("passphraseMismatch"); return; }
        if (!GenerateNew && (SelectedKey == null || SelectedKey.Error != null))
        { Status = SshStrings.SetupError(SelectedKey?.Error ?? "invalidKey"); return; }
        if (GenerateNew && string.IsNullOrEmpty(InstallationPassword))
        { Status = SshStrings.SetupError("passwordRequired"); return; }
        var generate = GenerateNew;
        var selected = SelectedKey;
        var passphrase = KeyPassphrase;
        var password = InstallationPassword;
        InstallationPassword = "";
        IsBusy = true;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        var progress = new Progress<string>(stage => { if (!_closing && IsBusy) Status = SshStrings.SetupStage(stage); });
        try
        {
            var result = await Task.Run(() => new SshKeyInstaller(_store).ConfigureAsync(
                _target, selected, generate, passphrase, password, progress, operation.Token));
            if (_closing) return;
            // Keep generated identity after partial failure, so Retry uses this exact key.
            if (result.Key != null)
            {
                var existing = Candidates.FirstOrDefault(k => k.Path == result.Key.Path);
                if (existing != null) Candidates.Remove(existing);
                Candidates.Insert(0, result.Key);
                GenerateNew = false;
                SelectedKey = result.Key;
                KeyPassphrase = passphrase;
                PublicKeyText = result.Key.PublicKey ?? "";
            }
            if (result.Verified)
            {
                Result = new(result.Key!, passphrase);
                _closing = true;
            }
            else
            {
                NeedsPassword = result.NeedsPassword || generate || result.RemoteMayHaveChanged;
                Status = SshStrings.SetupError(result.Error ?? "connection")
                    + (result.RemoteMayHaveChanged ? "\n" + SshStrings.RemoteMayRemain : "");
            }
        }
        catch (OperationCanceledException) { Status = SshStrings.Cancelled; }
        catch { Status = SshStrings.SetupError("storage"); }
        finally
        {
            _operation = null;
            InstallationPassword = "";
            IsBusy = false;
            if (_closing)
            {
                KeyPassphrase = "";
                ConfirmPassphrase = "";
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
