using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingSubSettingForm : BaseForm
    {
        public string Url;
        public RoutingSubSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingSubSettingForm_Load(object sender, EventArgs e)
        {
            if (config.ruleSubItem == null)
            {
                config.ruleSubItem = new List<SubItem>();
            }
            if (config.ruleSubItem.Count <= 0)
            {
                config.ruleSubItem.Add(new SubItem
                {
                    remarks = "def",
                    url = Global.CustomRoutingListUrl + "custom_routing_rules"
                });
            }
            txtUrl.Text = config.ruleSubItem[0].url;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            var url = txtUrl.Text.Trim();

            if (Utils.IsNullOrEmpty(url))
            {
                return;
            }
            Url = url;
            config.ruleSubItem[0].url = url;
            ConfigHandler.SaveRuleSubItem(ref config);

            this.DialogResult = DialogResult.OK;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

    }
}
