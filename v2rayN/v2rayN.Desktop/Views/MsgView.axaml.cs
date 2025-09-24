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
    private readonly ScrollViewer _scrollViewer;
    private int _lastShownLength = 0;

    public MsgView()
    {
        InitializeComponent();
        _scrollViewer = this.FindControl<ScrollViewer>("msgScrollViewer");

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
        var newText = msg?.ToString() ?? string.Empty;

        if (txtMsg.Text is { } old &&
            newText.Length >= _lastShownLength &&
            newText.AsSpan(0, _lastShownLength).SequenceEqual(old))
        {
            var delta = newText.AsSpan(_lastShownLength);
            if (!delta.IsEmpty)
                txtMsg.Text += delta.ToString();
        }
        else
        {
            txtMsg.Text = newText;
        }

        _lastShownLength = txtMsg.Text.Length;

        if (togScrollToEnd.IsChecked ?? true)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => _scrollViewer?.ScrollToEnd(),
                Avalonia.Threading.DispatcherPriority.Render);
        }
    }

    public void ClearMsg()
    {
        ViewModel?.ClearMsg();
        txtMsg.Text = "";
        _lastShownLength = 0;
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
