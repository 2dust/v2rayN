using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class AddServerForm : BaseServerForm
    {

        public AddServerForm()
        {
            InitializeComponent();
        }

        private void AddServerForm_Load(object sender, EventArgs e)
        {
            cmbSecurity.Items.AddRange(Global.vmessSecuritys.ToArray());
            if (EditIndex >= 0)
            {
                vmessItem = config.vmess[EditIndex];
                BindingServer();
            }
            else
            {
                vmessItem = new VmessItem();
                ClearServer();
            }
        }

        /// <summary>
        /// 绑定数据
        /// </summary>
        private void BindingServer()
        {
            txtAddress.Text = vmessItem.address;
            txtPort.Text = vmessItem.port.ToString();
            txtId.Text = vmessItem.id;
            txtAlterId.Text = vmessItem.alterId.ToString();
            cmbSecurity.Text = vmessItem.security;
            txtRemarks.Text = vmessItem.remarks;

            transportControl.BindingServer(vmessItem);
        }


        /// <summary>
        /// 清除设置
        /// </summary>
        private void ClearServer()
        {
            txtAddress.Text = "";
            txtPort.Text = "";
            txtId.Text = "";
            txtAlterId.Text = "0";
            cmbSecurity.Text = Global.DefaultSecurity;
            txtRemarks.Text = "";

            transportControl.ClearServer(vmessItem);
        }
         
        private void btnOK_Click(object sender, EventArgs e)
        {
            string address = txtAddress.Text;
            string port = txtPort.Text;
            string id = txtId.Text;
            string alterId = txtAlterId.Text;
            string security = cmbSecurity.Text;
            string remarks = txtRemarks.Text;

            if (Utils.IsNullOrEmpty(address))
            {
                UI.Show(UIRes.I18N("FillServerAddress"));
                return;
            }
            if (Utils.IsNullOrEmpty(port) || !Utils.IsNumberic(port))
            {
                UI.Show(UIRes.I18N("FillCorrectServerPort"));
                return;
            }
            if (Utils.IsNullOrEmpty(id))
            {
                UI.Show(UIRes.I18N("FillUUID"));
                return;
            }

            transportControl.EndBindingServer();

            vmessItem.address = address;
            vmessItem.port = Utils.ToInt(port);
            vmessItem.id = id;
            vmessItem.alterId = Utils.ToInt(alterId);
            vmessItem.security = security;
            vmessItem.remarks = remarks;

            if (ConfigHandler.AddServer(ref config, vmessItem, EditIndex) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("OperationFailed"));
            }
        }

        private void btnGUID_Click(object sender, EventArgs e)
        {
            txtId.Text = Utils.GetGUID();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        #region 导入客户端/服务端配置

        /// <summary>
        /// 导入客户端
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItemImportClient_Click(object sender, EventArgs e)
        {
            MenuItemImport(1);
        }

        /// <summary>
        /// 导入服务端
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItemImportServer_Click(object sender, EventArgs e)
        {
            MenuItemImport(2);
        }

        private void MenuItemImport(int type)
        {
            ClearServer();

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
            string msg;
            VmessItem vmessItemTemp;
            if (type.Equals(1))
            {
                vmessItemTemp = V2rayConfigHandler.ImportFromClientConfig(fileName, out msg);
            }
            else
            {
                vmessItemTemp = V2rayConfigHandler.ImportFromServerConfig(fileName, out msg);
            }
            if (vmessItemTemp == null)
            {
                UI.ShowWarning(msg);
                return;
            }
            vmessItem = vmessItemTemp;

            txtAddress.Text = vmessItem.address;
            txtPort.Text = vmessItem.port.ToString();
            txtId.Text = vmessItem.id;
            txtAlterId.Text = vmessItem.alterId.ToString();
            txtRemarks.Text = vmessItem.remarks;

            transportControl.BindingServer(vmessItem);
        }

        /// <summary>
        /// 从剪贴板导入URL
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItemImportClipboard_Click(object sender, EventArgs e)
        {
            ClearServer();

            VmessItem vmessItemTemp = ShareHandler.ImportFromClipboardConfig(Utils.GetClipboardData(), out string msg);
            if (vmessItemTemp == null)
            {
                UI.ShowWarning(msg);
                return;
            }
            vmessItem = vmessItemTemp;

            txtAddress.Text = vmessItem.address;
            txtPort.Text = vmessItem.port.ToString();
            txtId.Text = vmessItem.id;
            txtAlterId.Text = vmessItem.alterId.ToString();
            txtRemarks.Text = vmessItem.remarks;

            transportControl.BindingServer(vmessItem);
        }
        #endregion

    }
}
