using System;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public delegate void ChangeEventHandler(object sender, EventArgs e);
    public partial class SubSettingControl : UserControl
    {
        public event ChangeEventHandler OnButtonClicked;


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
            this.Height = grbMain.Height;
            BindingSub();
        }

        private void BindingSub()
        {
            if (subItem != null)
            {
                txtRemarks.Text = subItem.remarks.ToString();
                txtUrl.Text = subItem.url.ToString();
                chkEnabled.Checked = subItem.enabled;
            }
        }
        private void EndBindingSub()
        {
            if (subItem != null)
            {
                subItem.remarks = txtRemarks.Text.TrimEx();
                subItem.url = txtUrl.Text.TrimEx();
                subItem.enabled = chkEnabled.Checked;
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
            if (this.Height <= grbMain.Height)
            {
                if (Utils.IsNullOrEmpty(subItem.url))
                {
                    picQRCode.Image = null;
                    return;
                }
                picQRCode.Image = QRCodeHelper.GetQRCode(subItem.url);
                this.Height = grbMain.Height + 200;
            }
            else
            {
                this.Height = grbMain.Height;
            }
        }
    }
}
