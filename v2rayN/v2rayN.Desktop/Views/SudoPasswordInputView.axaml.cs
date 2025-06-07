using Avalonia.Controls;
using Avalonia.Threading;
using DialogHostAvalonia;

namespace v2rayN.Desktop.Views;

public partial class SudoPasswordInputView : UserControl
{
    public SudoPasswordInputView()
    {
        InitializeComponent();

        this.Loaded += (s, e) => txtPassword.Focus();

        btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                txtPassword.Focus();
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                DialogHost.Close(null, txtPassword.Text);
            });
        };

        btnCancel.Click += (_, _) =>
        {
            DialogHost.Close(null);
        };
    }
}
