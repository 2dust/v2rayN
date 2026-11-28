using System.Collections.ObjectModel;
using v2rayN.Desktop.Common;
using v2rayN.Desktop.Controls;

namespace v2rayN.Desktop.Views;

public partial class MsgView : ReactiveUserControl<MsgViewModel>
{
    private readonly ObservableCollection<LogLineItem> _logLines = [];

    public MsgView()
    {
        InitializeComponent();

        lstMsg.ItemsSource = _logLines;
        lstMsg.GotFocus += LstMsg_GotFocus;
        lstMsg.KeyDown += LstMsg_KeyDown;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.MsgFilter, v => v.cmbMsgFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);

            ViewModel.ShowMsgInteraction.RegisterHandler(interaction =>
            {
                var msg = interaction.Input;
                ShowMsg(msg);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
        });
    }

    private void LstMsg_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_logLines.Count > 0 && lstMsg.SelectedIndex < 0)
        {
            lstMsg.SelectedIndex = (togScrollToEnd.IsChecked ?? true) ? _logLines.Count - 1 : 0;
        }
    }

    private void LstMsg_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            switch (e.Key)
            {
                case Key.A:
                    e.Handled = true;
                    lstMsg.SelectAll();
                    break;

                case Key.C:
                    e.Handled = true;
                    menuMsgViewCopy_Click(sender, e);
                    break;
            }
        }
    }

    private void ShowMsg(object msg)
    {
        var maxLines = ViewModel?.NumMaxMsg ?? 500;
        if (_logLines.Count > maxLines)
        {
            ClearMsg();
        }

        var text = msg.ToString();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            // Ignore only the trailing empty segment caused by a terminating newline.
            if (i == lines.Length - 1 && lines[i].Length == 0)
            {
                continue;
            }

            _logLines.Add(new LogLineItem(lines[i]));
        }

        while (_logLines.Count > maxLines + 50)
        {
            _logLines.RemoveAt(0);
        }

        if ((togScrollToEnd.IsChecked ?? true) && _logLines.Count > 0)
        {
            var lastItem = _logLines[^1];
            Dispatcher.UIThread.Post(() =>
            {
                if ((togScrollToEnd.IsChecked ?? true) && _logLines.Count > 0)
                {
                    lstMsg.ScrollIntoView(lastItem);
                }
            }, DispatcherPriority.Background);
        }
    }

    public void ClearMsg()
    {
        _logLines.Clear();
        _logLines.Add(new LogLineItem("----- Message cleared -----"));
    }

    private void menuMsgViewSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lstMsg.Focus();
            lstMsg.SelectAll();
        }, DispatcherPriority.Render);
    }

    private async void menuMsgViewCopy_Click(object? sender, RoutedEventArgs e)
    {
        var selected = lstMsg.SelectedItems?.OfType<LogLineItem>().Select(x => x.Text).ToList();
        if (selected is null || selected.Count == 0)
        {
            return;
        }

        var data = string.Join(Environment.NewLine, selected).TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private async void menuMsgViewCopyAll_Click(object? sender, RoutedEventArgs e)
    {
        var data = string.Join(Environment.NewLine, _logLines.Select(x => x.Text)).TrimEx();
        await AvaUtils.SetClipboardData(this, data);
    }

    private void menuMsgViewClear_Click(object? sender, RoutedEventArgs e)
    {
        ClearMsg();
    }
}
