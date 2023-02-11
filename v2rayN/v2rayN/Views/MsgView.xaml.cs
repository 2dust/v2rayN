using ReactiveUI;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Views
{
    public partial class MsgView
    {
        private static Config _config;
        public MsgView()
        {
            InitializeComponent();
            _config = LazyConfig.Instance.GetConfig();
            MessageBus.Current.Listen<string>("MsgView").Subscribe(x => DelegateAppendText(x));
            Global.PresetMsgFilters.ForEach(it =>
            {
                cmbMsgFilter.Items.Add(it);
            });
            if (!_config.uiItem.mainMsgFilter.IsNullOrEmpty())
            {
                cmbMsgFilter.Text = _config.uiItem.mainMsgFilter;
            }
        }

        void DelegateAppendText(string msg)
        {
            Dispatcher.BeginInvoke(new Action<string>(AppendText), DispatcherPriority.Send, msg);
        }

        public void AppendText(string msg)
        {
            if (msg.Equals(Global.CommandClearMsg))
            {
                ClearMsg();
                return;
            }
            if (!togAutoRefresh.IsChecked.Value)
            {
                return;
            }
            var MsgFilter = cmbMsgFilter.Text.TrimEx();
            if (!Utils.IsNullOrEmpty(MsgFilter))
            {
                if (!Regex.IsMatch(msg, MsgFilter))
                {
                    return;
                }
            }

            ShowMsg(msg);
        }

        private void ShowMsg(string msg)
        {
            if (txtMsg.LineCount > 999)
            {
                ClearMsg();
            }
            this.txtMsg.AppendText(msg);
            if (!msg.EndsWith(Environment.NewLine))
            {
                this.txtMsg.AppendText(Environment.NewLine);
            }
            txtMsg.ScrollToEnd();
        }

        public void ClearMsg()
        {
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
            Utils.SetClipboardData(data);
        }

        private void menuMsgViewCopyAll_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var data = txtMsg.Text;
            Utils.SetClipboardData(data);
        }

        private void menuMsgViewClear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ClearMsg();
        }
        private void cmbMsgFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _config.uiItem.mainMsgFilter = cmbMsgFilter.Text.TrimEx();
        }

    }
}