using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class MsgView : ReactiveUserControl<MsgViewModel>
{
    private const int MaxLines = 500;

    public MsgView()
    {
        InitializeComponent();

        txtMsg.Options.AllowScrollBelowDocument = false;

        ViewModel = new MsgViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.MsgFilter, v => v.cmbMsgFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.DispatcherShowMsg:
                if (obj is null)
                    return false;

                Dispatcher.UIThread.Post(() =>
                    ShowMsg(obj),
                   DispatcherPriority.ApplicationIdle);
                break;
        }
        return await Task.FromResult(true);
    }

    private void ShowMsg(object msg)
    {
        txtMsg.AppendText(msg.ToString());

        var doc = txtMsg.Document;
        if (doc.LineCount > MaxLines)
        {
            int removeCount = doc.LineCount - MaxLines;
            var line = doc.GetLineByNumber(removeCount);
            int removeUpTo = line.EndOffset;
            doc.Remove(0, removeUpTo);
        }

        if (togScrollToEnd.IsChecked ?? true)
        {
            txtMsg.ScrollToEnd();
        }
    }

    public void ClearMsg()
    {
        ViewModel?.ClearMsg();
        txtMsg.Text = "";
    }

    private void menuMsgViewSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        txtMsg.Focus();
        txtMsg.SelectAll();
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
