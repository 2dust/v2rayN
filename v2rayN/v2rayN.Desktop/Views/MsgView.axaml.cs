using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class MsgView : ReactiveUserControl<MsgViewModel>
{
    //private const int KeepLines = 30;

    public MsgView()
    {
        InitializeComponent();
        txtMsg.TextArea.TextView.Options.EnableHyperlinks = false;
        ViewModel = new MsgViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.MsgFilter, v => v.cmbMsgFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
        });

        TextEditorKeywordHighlighter.Attach(txtMsg, Global.LogLevelColors.ToDictionary(
                kv => kv.Key,
                kv => (IBrush)new SolidColorBrush(Color.Parse(kv.Value))
            ));
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.DispatcherShowMsg:
                if (obj is null)
                {
                    return false;
                }

                Dispatcher.UIThread.Post(() => ShowMsg(obj),
                    DispatcherPriority.ApplicationIdle);
                break;
        }
        return await Task.FromResult(true);
    }

    private void ShowMsg(object msg)
    {
        txtMsg.AppendText(msg.ToString());
        TrimMsg();
        if (togScrollToEnd.IsChecked ?? true)
        {
            txtMsg.ScrollToEnd();
        }
    }

    private void TrimMsg()
    {
        var maxLines = ViewModel?.NumMaxMsg ?? 500;
        if (txtMsg.LineCount <= maxLines || txtMsg.Document == null)
        {
            return;
        }

        var firstKeepLine = txtMsg.LineCount - maxLines + 1;
        var cutLine = txtMsg.Document.GetLineByNumber(firstKeepLine);
        if (cutLine.Offset <= 0)
        {
            return;
        }

        txtMsg.Document.Remove(0, cutLine.Offset);
    }

    public void ClearMsg()
    {
        txtMsg.Clear();
        txtMsg.AppendText("----- Message cleared -----\n");
    }

    private void menuMsgViewSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            txtMsg.TextArea.Focus();
            txtMsg.SelectAll();
        }, DispatcherPriority.Render);
    }

    private async void menuMsgViewCopy_Click(object? sender, RoutedEventArgs e)
    {
        var data = txtMsg.SelectedText.TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private async void menuMsgViewCopyAll_Click(object? sender, RoutedEventArgs e)
    {
        var data = txtMsg.Text.TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private void menuMsgViewClear_Click(object? sender, RoutedEventArgs e)
    {
        ClearMsg();
    }
}
