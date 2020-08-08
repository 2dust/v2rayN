using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class AddServerForm : BaseForm
    {
        public int EditIndex { get; set; }
        VmessItem vmessItem = null;

        public AddServerForm()
        {
            InitializeComponent();
        }

        private void AddServerForm_Load(object sender, EventArgs e)
        {
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
            cmbNetwork.Text = vmessItem.network;
            txtRemarks.Text = vmessItem.remarks;

            cmbHeaderType.Text = vmessItem.headerType;
            txtRequestHost.Text = vmessItem.requestHost;
            txtPath.Text = vmessItem.path;
            cmbStreamSecurity.Text = vmessItem.streamSecurity;
            cmbAllowInsecure.Text = vmessItem.allowInsecure;
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
            cmbNetwork.Text = Global.DefaultNetwork;
            txtRemarks.Text = "";

            cmbHeaderType.Text = Global.None;
            txtRequestHost.Text = "";
            cmbStreamSecurity.Text = "";
            cmbAllowInsecure.Text = "";
            txtPath.Text = "";
        }


        private void cmbNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetHeaderType();
        }

        /// <summary>
        /// 设置伪装选项
        /// </summary>
        private void SetHeaderType()
        {
            cmbHeaderType.Items.Clear();

            string network = cmbNetwork.Text;
            if (Utils.IsNullOrEmpty(network))
            {
                cmbHeaderType.Items.Add(Global.None);
                return;
            }

            cmbHeaderType.Items.Add(Global.None);
            if (network.Equals(Global.DefaultNetwork))
            {
                cmbHeaderType.Items.Add(Global.TcpHeaderHttp);
            }
            else if (network.Equals("kcp") || network.Equals("quic"))
            {
                cmbHeaderType.Items.Add("srtp");
                cmbHeaderType.Items.Add("utp");
                cmbHeaderType.Items.Add("wechat-video");
                cmbHeaderType.Items.Add("dtls");
                cmbHeaderType.Items.Add("wireguard");
            }
            else
            {
            }
            cmbHeaderType.Text = Global.None;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string address = txtAddress.Text;
            string port = txtPort.Text;
            string id = txtId.Text;
            string alterId = txtAlterId.Text;
            string security = cmbSecurity.Text;
            string network = cmbNetwork.Text;
            string remarks = txtRemarks.Text;

            string headerType = cmbHeaderType.Text;
            string requestHost = txtRequestHost.Text;
            string path = txtPath.Text;
            string streamSecurity = cmbStreamSecurity.Text;
            string allowInsecure = cmbAllowInsecure.Text;

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
            if (Utils.IsNullOrEmpty(alterId) || !Utils.IsNumberic(alterId))
            {
                UI.Show(UIRes.I18N("FillCorrectAlterId"));
                return;
            }

            vmessItem.address = address;
            vmessItem.port = Utils.ToInt(port);
            vmessItem.id = id;
            vmessItem.alterId = Utils.ToInt(alterId);
            vmessItem.security = security;
            vmessItem.network = network;
            vmessItem.remarks = remarks;

            vmessItem.headerType = headerType;
            vmessItem.requestHost = requestHost.Replace(" ", "");
            vmessItem.path = path.Replace(" ", "");
            vmessItem.streamSecurity = streamSecurity;
            vmessItem.allowInsecure = allowInsecure;

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

        private void cmbStreamSecurity_SelectedIndexChanged(object sender, EventArgs e)
        {
            string security = cmbStreamSecurity.Text;
            if (Utils.IsNullOrEmpty(security))
            {
                panTlsMore.Hide();
            }
            else
            {
                panTlsMore.Show();
            }
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
            VmessItem vmessItem;
            if (type.Equals(1))
            {
                vmessItem = V2rayConfigHandler.ImportFromClientConfig(fileName, out msg);
            }
            else
            {
                vmessItem = V2rayConfigHandler.ImportFromServerConfig(fileName, out msg);
            }
            if (vmessItem == null)
            {
                UI.ShowWarning(msg);
                return;
            }

            txtAddress.Text = vmessItem.address;
            txtPort.Text = vmessItem.port.ToString();
            txtId.Text = vmessItem.id;
            txtAlterId.Text = vmessItem.alterId.ToString();
            txtRemarks.Text = vmessItem.remarks;
            cmbNetwork.Text = vmessItem.network;
            cmbHeaderType.Text = vmessItem.headerType;
            txtRequestHost.Text = vmessItem.requestHost;
            txtPath.Text = vmessItem.path;
            cmbStreamSecurity.Text = vmessItem.streamSecurity;
        }

        /// <summary>
        /// 从剪贴板导入URL
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItemImportClipboard_Click(object sender, EventArgs e)
        {
            ClearServer();

            VmessItem vmessItem = V2rayConfigHandler.ImportFromClipboardConfig(Utils.GetClipboardData(), out string msg);
            if (vmessItem == null)
            {
                UI.ShowWarning(msg);
                return;
            }

            txtAddress.Text = vmessItem.address;
            txtPort.Text = vmessItem.port.ToString();
            txtId.Text = vmessItem.id;
            txtAlterId.Text = vmessItem.alterId.ToString();
            txtRemarks.Text = vmessItem.remarks;
            cmbNetwork.Text = vmessItem.network;
            cmbHeaderType.Text = vmessItem.headerType;
            txtRequestHost.Text = vmessItem.requestHost;
            txtPath.Text = vmessItem.path;
            cmbStreamSecurity.Text = vmessItem.streamSecurity;
        }
        #endregion

    }
}
