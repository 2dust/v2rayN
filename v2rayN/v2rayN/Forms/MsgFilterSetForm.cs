using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace v2rayN.Forms
{
    public partial class MsgFilterSetForm : BaseForm
    {
        public string MsgFilter { get; set; }

        public MsgFilterSetForm()
        {
            InitializeComponent();
        }

        private void MsgFilterSetForm_Load(object sender, EventArgs e)
        {
            txtMsgFilter.Text = MsgFilter;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            MsgFilter = txtMsgFilter.Text;
            this.DialogResult = DialogResult.OK;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnFilderProxy_Click(object sender, EventArgs e)
        {
            txtMsgFilter.Text = "^(?!.*proxy).*$";
        }

        private void btnFilterDirect_Click(object sender, EventArgs e)
        {
            txtMsgFilter.Text = "^(?!.*direct).*$";
        }
    }
}
