using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class MsgView : ReactiveUserControl<MsgViewModel>
    {
        public MsgView()
        {
            InitializeComponent();

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
                    if (obj is null) return false;

                    Dispatcher.UIThread.Post(() =>
                        ShowMsg(obj),
                       DispatcherPriority.ApplicationIdle);
                    break;
            }
            return await Task.FromResult(true);
        }

        private void ShowMsg(object msg)
        {
            txtMsg.Text = msg.ToString();
            if (togScrollToEnd.IsChecked ?? true)
            {
                txtMsg.CaretIndex = int.MaxValue;
            }
        }

        public void ClearMsg()
        {
            ViewModel?.ClearMsg();
            txtMsg.Clear();
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
}