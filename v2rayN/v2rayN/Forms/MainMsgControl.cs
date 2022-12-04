using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class MainMsgControl : UserControl
    {
        private string _msgFilter = string.Empty;
        delegate void AppendTextDelegate(string text);

        public MainMsgControl()
        {
            InitializeComponent();
        }

        private void MainMsgControl_Load(object sender, EventArgs e)
        {
            _msgFilter = Utils.RegReadValue(Global.MyRegPath, Utils.MainMsgFilterKey, "");
            if (!Utils.IsNullOrEmpty(_msgFilter))
            {
                gbMsgTitle.Text = string.Format(ResUI.MsgInformationTitle, _msgFilter);
            }
        }

        #region 提示信息

        public void AppendText(string text)
        {
            if (txtMsgBox.InvokeRequired)
            {
                Invoke(new AppendTextDelegate(AppendText), text);
            }
            else
            {
                if (!Utils.IsNullOrEmpty(_msgFilter))
                {
                    if (!Regex.IsMatch(text, _msgFilter))
                    {
                        return;
                    }
                }
                //this.txtMsgBox.AppendText(text);
                ShowMsg(text);
            }
        }

        /// <summary>
        /// 提示信息
        /// </summary>
        /// <param name="msg"></param>
        private void ShowMsg(string msg)
        {
            if (txtMsgBox.Lines.Length > 999)
            {
                ClearMsg();
            }
            txtMsgBox.AppendText(msg);
            if (!msg.EndsWith(Environment.NewLine))
            {
                txtMsgBox.AppendText(Environment.NewLine);
            }
        }

        /// <summary>
        /// 清除信息
        /// </summary>
        public void ClearMsg()
        {
            txtMsgBox.Invoke((Action)delegate
            {
                txtMsgBox.Clear();
            });
        }

        public void DisplayToolStatus(Config config)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{ResUI.LabLocal}:");
            sb.Append($"[{Global.InboundSocks}:{config.GetLocalPort(Global.InboundSocks)}]");
            sb.Append(" | ");
            sb.Append(config.sysProxyType == ESysProxyType.ForcedChange
                ? $"[{Global.InboundHttp}({ResUI.SystemProxy}):{config.GetLocalPort(Global.InboundHttp)}]"
                : $"[{Global.InboundHttp}:{config.GetLocalPort(Global.InboundHttp)}]");

            if (config.inbound[0].allowLANConn)
            {
                sb.Append($"  {ResUI.LabLAN}:");
                sb.Append($"[{Global.InboundSocks}:{config.GetLocalPort(Global.InboundSocks2)}]");
                sb.Append(" | ");
                sb.Append($"[{Global.InboundHttp}:{config.GetLocalPort(Global.InboundHttp2)}]");
            }

            SetToolSslInfo("inbound", sb.ToString());
        }

        public void SetToolSslInfo(string type, string value)
        {
            switch (type)
            {
                case "speed":
                    toolSslServerSpeed.Text = value;
                    break;
                case "inbound":
                    toolSslInboundInfo.Text = value;
                    break;
                case "routing":
                    toolSslRoutingRule.Text = value;
                    break;
            }

        }

        public void ScrollToCaret()
        {
            txtMsgBox.ScrollToCaret();
        }
        #endregion


        #region MsgBoxMenu
        private void menuMsgBoxSelectAll_Click(object sender, EventArgs e)
        {
            txtMsgBox.Focus();
            txtMsgBox.SelectAll();
        }

        private void menuMsgBoxCopy_Click(object sender, EventArgs e)
        {
            var data = txtMsgBox.SelectedText.TrimEx();
            Utils.SetClipboardData(data);
        }

        private void menuMsgBoxCopyAll_Click(object sender, EventArgs e)
        {
            var data = txtMsgBox.Text;
            Utils.SetClipboardData(data);
        }
        private void menuMsgBoxClear_Click(object sender, EventArgs e)
        {
            txtMsgBox.Clear();
        }
        private void menuMsgBoxAddRoutingRule_Click(object sender, EventArgs e)
        {
            menuMsgBoxCopy_Click(null, null);
            var fm = new RoutingSettingForm();
            fm.ShowDialog();

        }

        private void txtMsgBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A:
                        menuMsgBoxSelectAll_Click(null, null);
                        break;
                    case Keys.C:
                        menuMsgBoxCopy_Click(null, null);
                        break;
                    case Keys.V:
                        menuMsgBoxAddRoutingRule_Click(null, null);
                        break;

                }
            }

        }
        private void menuMsgBoxFilter_Click(object sender, EventArgs e)
        {
            var fm = new MsgFilterSetForm();
            fm.MsgFilter = _msgFilter;
            fm.ShowDefFilter = true;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                _msgFilter = fm.MsgFilter;
                gbMsgTitle.Text = string.Format(ResUI.MsgInformationTitle, _msgFilter);
                Utils.RegWriteValue(Global.MyRegPath, Utils.MainMsgFilterKey, _msgFilter);
            }
        }

        private void ssMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!Utils.IsNullOrEmpty(e.ClickedItem.Text))
            {
                Utils.SetClipboardData(e.ClickedItem.Text);
            }
        }
        #endregion


    }
}
