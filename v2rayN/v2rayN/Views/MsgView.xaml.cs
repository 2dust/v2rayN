using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Threading;

namespace v2rayN.Views
{
    public partial class MsgView
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

            btnCopy.Click += menuMsgViewCopyAll_Click;
            btnClear.Click += menuMsgViewClear_Click;
            menuMsgViewSelectAll.Click += menuMsgViewSelectAll_Click;
            menuMsgViewCopy.Click += menuMsgViewCopy_Click;
            menuMsgViewCopyAll.Click += menuMsgViewCopyAll_Click;
            menuMsgViewClear.Click += menuMsgViewClear_Click;

            Global.PresetMsgFilters.ForEach(it =>
            {
                cmbMsgFilter.Items.Add(it);
            });
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.DispatcherShowMsg:
                    if (obj is null) return false;
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
            txtMsg.BeginChange();
            txtMsg.Text = msg.ToString();
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
}