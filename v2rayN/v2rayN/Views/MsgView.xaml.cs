using ReactiveUI;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace v2rayN.Views
{
    public partial class MsgView
    {
        private static Config? _config;

        private string lastMsgFilter = string.Empty;
        private bool lastMsgFilterNotAvailable;

        public MsgView()
        {
            InitializeComponent();
            _config = LazyConfig.Instance.Config;
            MessageBus.Current.Listen<string>(Global.CommandSendMsgView).Subscribe(x => DelegateAppendText(x));
            Global.PresetMsgFilters.ForEach(it =>
            {
                cmbMsgFilter.Items.Add(it);
            });
            if (!_config.uiItem.mainMsgFilter.IsNullOrEmpty())
            {
                cmbMsgFilter.Text = _config.uiItem.mainMsgFilter;
            }
        }

        private void DelegateAppendText(string msg)
        {
            Dispatcher.BeginInvoke(AppendText, DispatcherPriority.ApplicationIdle, msg);
        }

        public void AppendText(string msg)
        {
            if (msg == Global.CommandClearMsg)
            {
                ClearMsg();
                return;
            }
            if (togAutoRefresh.IsChecked == false)
            {
                return;
            }

            var MsgFilter = cmbMsgFilter.Text.TrimEx();
            if (MsgFilter != lastMsgFilter) lastMsgFilterNotAvailable = false;
            if (!Utils.IsNullOrEmpty(MsgFilter) && !lastMsgFilterNotAvailable)
            {
                try
                {
                    if (!Regex.IsMatch(msg, MsgFilter)) // 如果不是正则表达式会异常
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    lastMsgFilterNotAvailable = true;
                }
            }
            lastMsgFilter = MsgFilter;

            ShowMsg(msg);

            if (togScrollToEnd.IsChecked ?? true)
            {
                txtMsg.ScrollToEnd();
            }
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

        private void cmbMsgFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _config.uiItem.mainMsgFilter = cmbMsgFilter.Text.TrimEx();
        }
    }
}