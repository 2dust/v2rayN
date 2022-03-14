using System;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class GroupSettingControl : UserControl
    {
        public event ChangeEventHandler OnButtonClicked;


        public GroupItem groupItem
        {
            get; set;
        }

        public GroupSettingControl()
        {
            InitializeComponent();
        }

        private void GroupSettingControl_Load(object sender, EventArgs e)
        {
            this.Height = grbMain.Height;
            BindingSub();
        }

        private void BindingSub()
        {
            if (groupItem != null)
            {
                txtRemarks.Text = groupItem.remarks.ToString();
            }
        }
        private void EndBindingSub()
        {
            if (groupItem != null)
            {
                groupItem.remarks = txtRemarks.Text.TrimEx();
            }
        }
        private void txtRemarks_Leave(object sender, EventArgs e)
        {
            EndBindingSub();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (groupItem != null)
            {
                groupItem.remarks = string.Empty;
            }

            OnButtonClicked?.Invoke(sender, e);
        } 
    }
}
