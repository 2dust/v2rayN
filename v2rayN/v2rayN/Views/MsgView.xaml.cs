using System.Reactive.Disposables;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ReactiveUI;

namespace v2rayN.Views;

public partial class MsgView
{
    private const int _maxLines = 320;
    private const int _trimLines = 350;

    public MsgView()
    {
        InitializeComponent();

        ViewModel = new MsgViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.MsgFilter, v => v.cmbMsgFilter.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
        });

        btnCopy.Click += menuMsgViewCopyAll_Click;
        btnClear.Click += menuMsgViewClear_Click;
        menuMsgViewSelectAll.Click += menuMsgViewSelectAll_Click;
        menuMsgViewCopy.Click += menuMsgViewCopy_Click;
        menuMsgViewCopyAll.Click += menuMsgViewCopyAll_Click;
        menuMsgViewClear.Click += menuMsgViewClear_Click;

        cmbMsgFilter.ItemsSource = Global.PresetMsgFilters;
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.DispatcherShowMsg:
                if (obj is null)
                    return false;
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ShowMsg(obj);
                }), DispatcherPriority.ApplicationIdle);
                break;
        }
        return await Task.FromResult(true);
    }

    private void ShowMsg(object msg)
    {
        var incoming = msg?.ToString() ?? string.Empty;
        var old = txtMsg.Text ?? string.Empty;

        txtMsg.BeginChange();

        if (incoming.Length >= old.Length && incoming.AsSpan(0, old.Length).SequenceEqual(old))
        {
            var delta = incoming.AsSpan(old.Length);
            if (!delta.IsEmpty)
                txtMsg.AppendText(delta.ToString());
        }
        else
        {
            // 兼容增量：如果不是全量覆盖场景，直接把 incoming 当作增量追加
            if (old.Length == 0)
            {
                txtMsg.Text = incoming;
            }
            else
            {
                txtMsg.AppendText(incoming);
            }
        }

        // 行数超过阈值才裁剪到 _maxLines
        var lines = txtMsg.Text.Split(Environment.NewLine);
        if (lines.Length > _trimLines)
        {
            var start = lines.Length - _maxLines;
            if (start < 0) start = 0;

            var sb = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Length - 1)
                    sb.Append(Environment.NewLine);
            }
            txtMsg.Text = sb.ToString();
        }

        if (togScrollToEnd.IsChecked ?? true)
        {
            txtMsg.ScrollToEnd();
        }

        txtMsg.EndChange();
    }

    public void ClearMsg()
    {
        ViewModel?.ClearMsg();
        txtMsg.Clear();
    }

    private void menuMsgViewSelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        txtMsg.Focus();
        txtMsg.SelectAll();
    }

    private void menuMsgViewCopy_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var data = txtMsg.SelectedText.TrimEx();
        WindowsUtils.SetClipboardData(data);
    }

    private void menuMsgViewCopyAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var data = txtMsg.Text;
        WindowsUtils.SetClipboardData(data);
    }

    private void menuMsgViewClear_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ClearMsg();
    }
}
