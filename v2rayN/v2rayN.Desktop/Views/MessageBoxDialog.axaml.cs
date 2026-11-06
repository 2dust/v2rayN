using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class MessageBoxDialog : Window
{
    public MessageBoxDialog()
        : this(string.Empty, string.Empty)
    {
    }

    public MessageBoxDialog(string caption, string message)
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            caption = "Design Caption";
            message = "Design Message";
        }

        Title = caption;
        txtMessage.Text = message;

        btnYes.Click += BtnYes_Click;
        btnNo.Click += BtnNo_Click;

        Opened += MessageBoxDialog_Opened;

        CanMinimize = false;
    }

    private void MessageBoxDialog_Opened(object? sender, EventArgs e)
    {
        btnYes.Focus();
    }

    private void BtnYes_Click(object? sender, RoutedEventArgs e)
    {
        Close(ButtonResult.Yes);
    }

    private void BtnNo_Click(object? sender, RoutedEventArgs e)
    {
        Close(ButtonResult.No);
    }
}
