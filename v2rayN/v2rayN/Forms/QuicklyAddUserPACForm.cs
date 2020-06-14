using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Base;

namespace v2rayN.Forms
{
    public partial class QuicklyAddUserPACForm : BaseForm
    {
        public QuicklyAddUserPACForm()
        {
            InitializeComponent();            
        }


        private void btnOK_Click(object sender, EventArgs e)
        {
            string userPacRule=this.txtUserPACRule.Text.TrimEx().Replace("\"", "");
            this.txtUserPACRule.Text = "";
            if (!string.IsNullOrEmpty(userPacRule))
            {
                config.userPacRule.Insert(0, userPacRule);                
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.txtUserPACRule.Text = "";
            this.DialogResult = DialogResult.Cancel;
        }

        private void QuicklyAddUserPACForm_VisibleChanged(object sender, EventArgs e)
        {
            //在主屏幕的左下角显示窗口
            if (this.Visible == true)
            {
                int heightLocation = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Size.Height - this.Size.Height - 10;
                this.SetDesktopLocation(12, heightLocation);
                this.Activate();
            }
            this.txtUserPACRule.Focus();
        }

    }
}
