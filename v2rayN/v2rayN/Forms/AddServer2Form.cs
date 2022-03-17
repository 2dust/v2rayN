using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class AddServer2Form : BaseServerForm
    {

        public AddServer2Form()
        {
            InitializeComponent();
        }

        private void AddServer2Form_Load(object sender, EventArgs e)
        {
            txtAddress.ReadOnly = true;
            if (vmessItem != null)
            {
                BindingServer();
            }
            else
            {
                vmessItem = new VmessItem();
                vmessItem.groupId = groupId;
                ClearServer();
            }
        }

        /// <summary>
        /// 绑定数据
        /// </summary>
        private void BindingServer()
        {
            txtRemarks.Text = vmessItem.remarks;
            txtAddress.Text = vmessItem.address;
        }


        /// <summary>
        /// 清除设置
        /// </summary>
        private void ClearServer()
        {
            txtRemarks.Text = "";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string remarks = txtRemarks.Text;
            if (Utils.IsNullOrEmpty(remarks))
            {
                UI.Show(UIRes.I18N("PleaseFillRemarks"));
                return;
            }
            vmessItem.remarks = remarks;

            if (ConfigHandler.EditCustomServer(ref config, vmessItem) == 0)
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
            if (Utils.IsNullOrEmpty(vmessItem.indexId))
            {
                this.DialogResult = DialogResult.Cancel;
            }
            else
            {
                this.DialogResult = DialogResult.OK;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            UI.Show(UIRes.I18N("CustomServerTips"));

            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Config|*.json|All|*.*"
            };
            if (fileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string fileName = fileDialog.FileName;
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            vmessItem.address = fileName;
            vmessItem.remarks = txtRemarks.Text;

            if (ConfigHandler.AddCustomServer(ref config, vmessItem) == 0)
            {
                BindingServer();
                UI.Show(UIRes.I18N("SuccessfullyImportedCustomServer"));
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("FailedImportedCustomServer"));
            }
        }
    }
}
