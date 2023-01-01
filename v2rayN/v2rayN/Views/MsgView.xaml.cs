using ReactiveUI;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using v2rayN.Base;

namespace v2rayN.Views
{
    public partial class MsgView
    {
        public MsgView()
        {
            InitializeComponent();
            MessageBus.Current.Listen<string>("MsgView").Subscribe(x => DelegateAppendText(x));
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
            var MsgFilter = txtMsgFilter.Text.TrimEx();
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

    }
}