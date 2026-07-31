namespace v2rayN.Views;

/// <summary>
/// Sign-in gate shown before the main window when there is no stored
/// CubeVPN account token. Mirrors the Android app's OTP login flow against
/// the same @cubevvpn_bot-backed API (requestcode.php / verifycode.php).
/// </summary>
public partial class CubeLoginWindow : Window
{
    private string _identifier = "";
    private readonly DispatcherTimer _cooldownTimer;
    private int _cooldownSeconds;

    public CubeLoginWindow()
    {
        InitializeComponent();

        btnGetCode.Click += BtnGetCode_Click;
        btnVerify.Click += BtnVerify_Click;
        btnResend.Click += BtnGetCode_Click;
        btnChangeIdentifier.Click += BtnChangeIdentifier_Click;

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cooldownTimer.Tick += CooldownTimer_Tick;
    }

    private async void BtnGetCode_Click(object sender, RoutedEventArgs e)
    {
        var identifier = panelCode.Visibility == Visibility.Visible ? _identifier : txtIdentifier.Text.Trim();
        if (identifier.IsNullOrEmpty())
        {
            return;
        }

        SetBusy(true);
        var result = await CubeAuthApi.RequestCodeAsync(identifier);
        SetBusy(false);

        switch (result)
        {
            case CubeAuthResult.RequestCodeOk ok:
                _identifier = identifier;
                ShowError(null);
                panelIdentifier.Visibility = Visibility.Collapsed;
                panelCode.Visibility = Visibility.Visible;
                StartCooldown(ok.CooldownSeconds);
                break;

            case CubeAuthResult.Error err:
                ShowError(err.Message);
                break;
        }
    }

    private async void BtnVerify_Click(object sender, RoutedEventArgs e)
    {
        var code = txtCode.Text.Trim();
        if (code.IsNullOrEmpty())
        {
            return;
        }

        SetBusy(true);
        var result = await CubeAuthApi.VerifyCodeAsync(_identifier, code);
        SetBusy(false);

        switch (result)
        {
            case CubeAuthResult.VerifyOk ok:
                var config = AppManager.Instance.Config;
                config.CubeAuthItem ??= new();
                config.CubeAuthItem.Token = ok.Token;
                config.CubeAuthItem.Identifier = ok.User.Identifier;
                config.CubeAuthItem.DisplayName = ok.User.DisplayName;
                await ConfigHandler.SaveConfig(config);
                DialogResult = true;
                Close();
                break;

            case CubeAuthResult.Error err:
                ShowError(err.Message);
                break;
        }
    }

    private void BtnChangeIdentifier_Click(object sender, RoutedEventArgs e)
    {
        _cooldownTimer.Stop();
        panelCode.Visibility = Visibility.Collapsed;
        panelIdentifier.Visibility = Visibility.Visible;
        ShowError(null);
    }

    private void StartCooldown(int seconds)
    {
        _cooldownSeconds = seconds;
        btnResend.IsEnabled = false;
        btnResend.Content = string.Format(ResUI.CubeLoginResendIn, _cooldownSeconds);
        _cooldownTimer.Start();
    }

    private void CooldownTimer_Tick(object? sender, EventArgs e)
    {
        _cooldownSeconds--;
        if (_cooldownSeconds <= 0)
        {
            _cooldownTimer.Stop();
            btnResend.IsEnabled = true;
            btnResend.Content = ResUI.CubeLoginResend;
        }
        else
        {
            btnResend.Content = string.Format(ResUI.CubeLoginResendIn, _cooldownSeconds);
        }
    }

    private void SetBusy(bool busy)
    {
        progressBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        btnGetCode.IsEnabled = !busy;
        btnVerify.IsEnabled = !busy;
        btnChangeIdentifier.IsEnabled = !busy;
    }

    private void ShowError(string? message)
    {
        txtError.Text = message ?? "";
        txtError.Visibility = message.IsNullOrEmpty() ? Visibility.Collapsed : Visibility.Visible;
    }
}
