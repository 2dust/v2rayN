using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingSettingDetailsForm : BaseForm
    {
        public int EditIndex
        {
            get; set;
        }
        protected RulesItem routingItem = null;

        public RoutingSettingDetailsForm()
        {
            InitializeComponent();
        }

        private void RoutingSettingDetailsForm_Load(object sender, EventArgs e)
        {
            if (EditIndex >= 0)
            {
                routingItem = config.rules[EditIndex];
                BindingData();
            }
            else
            {
                routingItem = new RulesItem();
                ClearBind();
            }
        }

        private void EndBindingData()
        {
            if (routingItem != null)
            {
                routingItem.remarks = txtRemarks.Text.TrimEx();
                routingItem.port = txtPort.Text.TrimEx();
                routingItem.outboundTag = cmbOutboundTag.Text;
                routingItem.domain = Utils.String2List(txtDomain.Text);
                routingItem.ip = Utils.String2List(txtIP.Text);

                var protocol = new List<string>();
                for (int i = 0; i < clbProtocol.Items.Count; i++)
                {
                    if (clbProtocol.GetItemChecked(i))
                    {
                        protocol.Add(clbProtocol.Items[i].ToString());
                    }
                }
                routingItem.protocol = protocol;
            }
        }
        private void BindingData()
        {
            if (routingItem != null)
            {
                txtRemarks.Text = routingItem.remarks ?? string.Empty;
                txtPort.Text = routingItem.port ?? string.Empty;
                cmbOutboundTag.Text = routingItem.outboundTag;
                txtDomain.Text = Utils.List2String(routingItem.domain, true);
                txtIP.Text = Utils.List2String(routingItem.ip, true);

                if (routingItem.protocol != null)
                {
                    for (int i = 0; i < clbProtocol.Items.Count; i++)
                    {
                        if (routingItem.protocol.Contains(clbProtocol.Items[i].ToString()))
                        {
                            clbProtocol.SetItemChecked(i, true);
                        }
                    }
                }
            }
        }
        private void ClearBind()
        {
            txtRemarks.Text = string.Empty;
            txtPort.Text = string.Empty;
            cmbOutboundTag.Text = Global.agentTag;
            txtDomain.Text = string.Empty;
            txtIP.Text = string.Empty;
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            EndBindingData();
            var hasRule = false;
            if (routingItem.domain != null && routingItem.domain.Count > 0)
            {
                hasRule = true;
            }
            if (routingItem.ip != null && routingItem.ip.Count > 0)
            {
                hasRule = true;
            }
            if (routingItem.protocol != null && routingItem.protocol.Count > 0)
            {
                hasRule = true;
            }
            if (!Utils.IsNullOrEmpty(routingItem.port))
            {
                hasRule = true;
            }
            if (!hasRule)
            {
                UI.ShowWarning(string.Format(UIRes.I18N("RoutingRuleDetailRequiredTips"), "Port/Protocol/Domain/IP"));
                return;
            }

            if (ConfigHandler.AddRoutingRule(ref config, routingItem, EditIndex) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }

            this.DialogResult = DialogResult.OK;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
