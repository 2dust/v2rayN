using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingRuleQuicklyAddForm : BaseForm
    {
        public string domain
        {
            get; set;
        }
        private RulesItem rulesItem;

        public RoutingRuleQuicklyAddForm()
        {
            InitializeComponent();
        }

        private void RoutingRuleQuicklyAddForm_Load(object sender, EventArgs e)
        {
            rulesItem = new RulesItem();
            ClearBind();
        }

        private void EndBindingData()
        {
            if (rulesItem != null)
            {
                rulesItem.outboundTag = cmbOutboundTag.Text;
                rulesItem.domain = Utils.String2List(txtDomain.Text);
                rulesItem.ip = Utils.String2List(txtIP.Text);
            }
        }

        private void ClearBind()
        {
            cmbOutboundTag.Text = Global.agentTag;
            txtDomain.Text = domain;
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
            if (!hasRule)
            {
                return;
            }
            if (ConfigHandler.InsertRoutingRuleItem(ref config, rulesItem) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
