using System;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using System.Linq;
using System.Collections.Generic;

namespace v2rayN.Forms
{
    public delegate void ChangeEventHandler(object sender, EventArgs e);
    public partial class SubSettingControl : UserControl
    {
        public event ChangeEventHandler OnButtonClicked;
        private List<GroupItem> groupItem;

        public SubItem subItem
        {
            get; set;
        }

        public SubSettingControl()
        {
            InitializeComponent();
        }

        private void SubSettingControl_Load(object sender, EventArgs e)
        {
            Height = grbMain.Height;

            groupItem = LazyConfig.Instance.GetConfig().groupItem;

            cmbGroup.Items.AddRange(groupItem.Select(t => t.remarks).ToArray());
            cmbGroup.Items.Add(string.Empty);

            BindingSub();
        }

        private void BindingSub()
        {
            if (subItem != null)
            {
                txtRemarks.Text = subItem.remarks.ToString();
                txtUrl.Text = subItem.url.ToString();
                chkEnabled.Checked = subItem.enabled;
                txtUserAgent.Text = subItem.userAgent;

                var index = groupItem.FindIndex(t => t.id == subItem.groupId);
                if (index >= 0)
                {
                    cmbGroup.SelectedIndex = index;
                }
            }
        }
        private void EndBindingSub()
        {
            if (subItem != null)
            {
                subItem.remarks = txtRemarks.Text.TrimEx();
                subItem.url = txtUrl.Text.TrimEx();
                subItem.enabled = chkEnabled.Checked;
                subItem.userAgent = txtUserAgent.Text.TrimEx();

                var index = groupItem.FindIndex(t => t.remarks == cmbGroup.Text);
                subItem.groupId = index >= 0 ? groupItem[index].id : string.Empty;
            }
        }
        private void txtRemarks_Leave(object sender, EventArgs e)
        {
            EndBindingSub();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (subItem != null)
            {
                subItem.remarks = string.Empty;
                subItem.url = string.Empty;
            }

            OnButtonClicked?.Invoke(sender, e);
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            if (Height <= grbMain.Height)
            {
                if (Utils.IsNullOrEmpty(subItem.url))
                {
                    picQRCode.Image = null;
                    return;
                }
                picQRCode.Image = QRCodeHelper.GetQRCode(subItem.url);
                Height = grbMain.Height + 200;
            }
            else
            {
                Height = grbMain.Height;
            }
        }
    }
}
