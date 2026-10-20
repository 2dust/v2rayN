using DialogHostAvalonia;

namespace v2rayN.Desktop.Views;

public partial class SudoPasswordInputView : UserControl
{
    private readonly string? _dialogIdentifier;

    public bool IsPreserveCorePrivileges => chkPreserveCorePrivileges.IsChecked ?? false;

    public SudoPasswordInputView() : this(false)
    {
    }

    public SudoPasswordInputView(bool preserveCorePrivileges, bool showPreserveOption = true, string? dialogIdentifier = null)
    {
        InitializeComponent();
        _dialogIdentifier = dialogIdentifier;

        chkPreserveCorePrivileges.IsChecked = preserveCorePrivileges;
        chkPreserveCorePrivileges.IsVisible = showPreserveOption;
        txtPreserveTip.IsVisible = showPreserveOption;

        Loaded += (s, e) => txtPassword.Focus();

        btnSave.Click += async (_, _) => await SavePasswordAsync();

        btnCancel.Click += (_, _) =>
        {
            DialogHost.Close(_dialogIdentifier);
        };
    }

    private async Task SavePasswordAsync()
    {
        if (txtPassword.Text.IsNullOrEmpty())
        {
            txtPassword.Focus();
            return;
        }

        var password = txtPassword.Text;
        btnSave.IsEnabled = false;

        try
        {
            // Verify if the password is correct
            if (await CoreAdminManager.CheckLinuxSudoPassword(password))
            {
                // Password verification successful, return password and close dialog
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DialogHost.Close(_dialogIdentifier, password);
                });
            }
            else
            {
                // Password verification failed, display error and let user try again
                NoticeManager.Instance.Enqueue(ResUI.SudoIncorrectPasswordTip);
                txtPassword.Focus();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SudoPassword", ex);
        }
        finally
        {
            btnSave.IsEnabled = true;
        }
    }
}
