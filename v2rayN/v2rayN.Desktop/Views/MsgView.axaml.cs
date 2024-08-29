using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class MsgView : UserControl
    {
        private static Config? _config;

        private string lastMsgFilter = string.Empty;
        private bool lastMsgFilterNotAvailable;
        private ConcurrentBag<string> _lstMsg = [];

        public MsgView()
        {
            InitializeComponent();
            _config = LazyConfig.Instance.Config;
            MessageBus.Current.Listen<string>(Global.CommandSendMsgView).Subscribe(x => DelegateAppendText(x));
            //Global.PresetMsgFilters.ForEach(it =>
            //{
            //    cmbMsgFilter.Items.Add(it);
            //});
            if (!_config.uiItem.mainMsgFilter.IsNullOrEmpty())
            {
                cmbMsgFilter.Text = _config.uiItem.mainMsgFilter;
            }
            cmbMsgFilter.TextChanged += (s, e) =>
            {
                _config.uiItem.mainMsgFilter = cmbMsgFilter.Text?.ToString();
            };
        }

        private void DelegateAppendText(string msg)
        {
            Dispatcher.UIThread.Post(() => AppendText(msg), DispatcherPriority.ApplicationIdle);
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

            var MsgFilter = cmbMsgFilter.Text?.ToString();
            if (MsgFilter != lastMsgFilter) lastMsgFilterNotAvailable = false;
            if (!Utils.IsNullOrEmpty(MsgFilter) && !lastMsgFilterNotAvailable)
            {
                try
                {
                    if (!Regex.IsMatch(msg, MsgFilter))
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
            }
        }

        private void ShowMsg(string msg)
        {
            if (_lstMsg.Count > 999)
            {
                ClearMsg();
            }
            if (!msg.EndsWith(Environment.NewLine))
            {
                _lstMsg.Add(Environment.NewLine);
            }
            _lstMsg.Add(msg);
            // if (!msg.EndsWith(Environment.NewLine))
            // {
            //     _lstMsg.Add(Environment.NewLine);
            // }
            this.txtMsg.Text = string.Join("", _lstMsg);
        }

        public void ClearMsg()
        {
            _lstMsg.Clear();
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