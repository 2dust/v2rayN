using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingRuleSettingDetailsForm : BaseForm
    {
        public RulesItem rulesItem
        {
            get; set;
        }

        public RoutingRuleSettingDetailsForm()
        {
            InitializeComponent();
        }

        private void RoutingRuleSettingDetailsForm_Load(object sender, EventArgs e)
        {
            if (Utils.IsNullOrEmpty(rulesItem.outboundTag))
            {
                ClearBind();
            }
            else
            {
                BindingData();
            }
        }

        private void EndBindingData()
        {
            if (rulesItem != null)
            {
                rulesItem.port = txtPort.Text.TrimEx();
                rulesItem.outboundTag = cmbOutboundTag.Text;
                rulesItem.domain = Utils.String2List(txtDomain.Text);
                rulesItem.ip = Utils.String2List(txtIP.Text);

                var protocol = new List<string>();
                for (int i = 0; i < clbProtocol.Items.Count; i++)
                {
                    if (clbProtocol.GetItemChecked(i))
                    {
                        protocol.Add(clbProtocol.Items[i].ToString());
                    }
                }
                rulesItem.protocol = protocol;
            }
        }
        private void BindingData()
        {
            if (rulesItem != null)
            {
                txtPort.Text = rulesItem.port ?? string.Empty;
                cmbOutboundTag.Text = rulesItem.outboundTag;
                txtDomain.Text = Utils.List2String(rulesItem.domain, true);
                txtIP.Text = Utils.List2String(rulesItem.ip, true);

                if (rulesItem.protocol != null)
                {
                    for (int i = 0; i < clbProtocol.Items.Count; i++)
                    {
                        if (rulesItem.protocol.Contains(clbProtocol.Items[i].ToString()))
                        {
                            clbProtocol.SetItemChecked(i, true);
                        }
                    }
                }
            }
        }
        private void ClearBind()
        {
            txtPort.Text = string.Empty;
            cmbOutboundTag.Text = Global.agentTag;
            txtDomain.Text = string.Empty;
            txtIP.Text = string.Empty;
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            EndBindingData();
            var hasRule = false;
            if (rulesItem.domain != null && rulesItem.domain.Count > 0)
            {
                hasRule = true;
            }
            if (rulesItem.ip != null && rulesItem.ip.Count > 0)
            {
                hasRule = true;
            }
            if (rulesItem.protocol != null && rulesItem.protocol.Count > 0)
            {
                hasRule = true;
            }
            if (!Utils.IsNullOrEmpty(rulesItem.port))
            {
                hasRule = true;
            }
            if (!hasRule)
            {
                UI.ShowWarning(string.Format(UIRes.I18N("RoutingRuleDetailRequiredTips"), "Port/Protocol/Domain/IP"));
                return;
            }
            this.DialogResult = DialogResult.OK;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
