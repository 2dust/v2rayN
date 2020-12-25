using System;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingSettingControl : UserControl
    {
        public event ChangeEventHandler OnButtonClicked;


        public RoutingItem routingItem
        {
            get; set;
        }

        public RoutingSettingControl()
        {
            InitializeComponent();
        }

        private void RoutingSettingControl_Load(object sender, EventArgs e)
        {
            BindingSub();
        }

        private void BindingSub()
        {
            if (routingItem != null)
            {
                txtRemarks.Text = routingItem.remarks.ToString();
                cmbOutboundTag.Text = routingItem.outboundTag;
                int.TryParse(routingItem.routingMode, out int routingMode);
                cmbroutingMode.SelectedIndex = routingMode;
                txtUserRule.Text = Utils.List2String(routingItem.userRules, true);
            }
        }
        private void EndBindingSub()
        {
            if (routingItem != null)
            {
                routingItem.remarks = txtRemarks.Text.TrimEx();
                routingItem.outboundTag = cmbOutboundTag.Text;
                routingItem.routingMode = cmbroutingMode.SelectedIndex.ToString();
                routingItem.userRules = Utils.String2List(txtUserRule.Text);
            }
        }
        private void txtRemarks_Leave(object sender, EventArgs e)
        {
            EndBindingSub();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (routingItem != null)
            {
                routingItem.remarks = string.Empty;
            }

            OnButtonClicked?.Invoke(sender, e);
        }

        private void btnExpand_Click(object sender, EventArgs e)
        {
            if (this.Height > 200)
            {
                this.Height = 160;
            }
            else
            {
                this.Height = 500;
            }
        }
    }
}
