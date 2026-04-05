using ReactiveUI.Fody.Helpers;

namespace v2rayN.ViewModels;

public class LiteMainWindowViewModel : ReactiveObject
{
    [Reactive]
    public string VlessLink { get; set; } = string.Empty;

    [Reactive]
    public string StatusText { get; set; } = "Не подключено";

    [Reactive]
    public string StatusColor { get; set; } = "#0066cc";

    [Reactive]
    public bool IsConnected { get; set; } = false;

    [Reactive]
    public bool IsConnecting { get; set; } = false;

    [Reactive]
    public bool VlessInputEnabled { get; set; } = true;

    [Reactive]
    public bool AutostartEnabled { get; set; } = false;

    [Reactive]
    public bool AutoconnectEnabled { get; set; } = false;

    [Reactive]
    public bool TrayMinimizeEnabled { get; set; } = false;

    [Reactive]
    public string ConnectButtonText { get; set; } = "ПОДКЛЮЧИТЬ";

    [Reactive]
    public bool ConnectingProgressVisible { get; set; } = false;

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLinkCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }

    private readonly LiteConnectionBridge _connectionBridge;
    private CancellationTokenSource? _cancellationTokenSource;

    public LiteMainWindowViewModel()
    {
        _connectionBridge = new LiteConnectionBridge();

        // Load saved settings
        LoadSettings();

        // Connect command
        ConnectCommand = ReactiveCommand.CreateFromTask(
            async _ => await ConnectVpn(),
            this.WhenAnyValue(x => x.IsConnected, x => x.IsConnecting, x => x.VlessLink,
                (connected, connecting, link) => !connected && !connecting && !string.IsNullOrWhiteSpace(link)));

        // Disconnect command
        DisconnectCommand = ReactiveCommand.CreateFromTask(
            async _ => await DisconnectVpn(),
            this.WhenAnyValue(x => x.IsConnected, x => x.IsConnecting,
                (connected, connecting) => connected && !connecting));

        // Clear link command
        ClearLinkCommand = ReactiveCommand.Create(() =>
        {
            VlessLink = string.Empty;
            SaveSettings();
        });

        // Open logs command
        OpenLogsCommand = ReactiveCommand.Create(() =>
        {
            try
            {
                var logsPath = Path.Combine(Utils.GetAppDataPath(), "logs");
                if (Directory.Exists(logsPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = logsPath
                    });
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(LiteMainWindowViewModel), ex);
            }
        });

        // Update UI based on connection state
        this.WhenAnyValue(x => x.IsConnected, x => x.IsConnecting)
            .Subscribe(x =>
            {
                UpdateButtonState();
            });
    }

    private void UpdateButtonState()
    {
        if (IsConnecting)
        {
            ConnectButtonText = "ПОДКЛЮЧЕНИЕ...";
            ConnectingProgressVisible = true;
            VlessInputEnabled = false;
            StatusColor = "#ffaa00";
        }
        else if (IsConnected)
        {
            ConnectButtonText = "ОТКЛЮЧИТЬ";
            ConnectingProgressVisible = false;
            VlessInputEnabled = false;
            StatusColor = "#00cc00";
        }
        else
        {
            ConnectButtonText = "ПОДКЛЮЧИТЬ";
            ConnectingProgressVisible = false;
            VlessInputEnabled = true;
            StatusColor = "#0066cc";
        }
    }

    private async Task ConnectVpn()
    {
        if (string.IsNullOrWhiteSpace(VlessLink))
        {
            StatusText = "Ошибка: введите VLESS-ссылку";
            StatusColor = "#ff0000";
            return;
        }

        if (!VlessLink.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Ошибка: только VLESS + REALITY";
            StatusColor = "#ff0000";
            return;
        }

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            IsConnecting = true;
            StatusText = "Подключение...";
            StatusColor = "#ffaa00";

            bool result = await _connectionBridge.ConnectAsync(VlessLink, _cancellationTokenSource.Token);

            if (result)
            {
                IsConnected = true;
                StatusText = "Подключено ✓";
                StatusColor = "#00cc00";
                SaveSettings();
            }
            else
            {
                StatusText = "Ошибка подключения";
                StatusColor = "#ff0000";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Подключение отменено";
            StatusColor = "#ffaa00";
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteMainWindowViewModel), ex);
            StatusText = "Ошибка: " + ex.Message.Substring(0, Math.Min(30, ex.Message.Length));
            StatusColor = "#ff0000";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task DisconnectVpn()
    {
        try
        {
            IsConnecting = true;
            StatusText = "Отключение...";
            StatusColor = "#ffaa00";

            bool result = await _connectionBridge.DisconnectAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

            if (result)
            {
                IsConnected = false;
                StatusText = "Не подключено";
                StatusColor = "#0066cc";
            }
            else
            {
                StatusText = "Ошибка отключения";
                StatusColor = "#ff0000";
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteMainWindowViewModel), ex);
            StatusText = "Ошибка отключения";
            StatusColor = "#ff0000";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var config = AppManager.Instance?.GetAppConfig();
            if (config != null)
            {
                config.LiteLastVlessLink = VlessLink;
                config.LiteAutostartEnabled = AutostartEnabled;
                config.LiteAutoconnectEnabled = AutoconnectEnabled;
                config.LiteTrayMinimizeEnabled = TrayMinimizeEnabled;
                ConfigHandler.SaveConfig(config);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteMainWindowViewModel), ex);
        }
    }

    private void LoadSettings()
    {
        try
        {
            var config = AppManager.Instance?.GetAppConfig();
            if (config != null)
            {
                VlessLink = config.LiteLastVlessLink ?? string.Empty;
                AutostartEnabled = config.LiteAutostartEnabled;
                AutoconnectEnabled = config.LiteAutoconnectEnabled;
                TrayMinimizeEnabled = config.LiteTrayMinimizeEnabled;

                // Auto-connect if enabled
                if (AutoconnectEnabled && !string.IsNullOrWhiteSpace(VlessLink))
                {
                    _ = Task.Delay(1000).ContinueWith(async _ => await ConnectVpn());
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(LiteMainWindowViewModel), ex);
        }
    }

    public void Cleanup()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}