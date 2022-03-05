using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class AddServer3Form : BaseServerForm
    { 

        public AddServer3Form()
        {
            InitializeComponent();
        }

        private void AddServer3Form_Load(object sender, EventArgs e)
        {
            cmbSecurity.Items.AddRange(config.GetShadowsocksSecuritys().ToArray());
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
            cmbSecurity.Text = vmessItem.security;
            txtRemarks.Text = vmessItem.remarks;
        }


        /// <summary>
        /// 清除设置
        /// </summary>
        private void ClearServer()
        {
            txtAddress.Text = "";
            txtPort.Text = "";
            txtId.Text = "";
            cmbSecurity.Text = Global.DefaultSecurity;
            txtRemarks.Text = "";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string address = txtAddress.Text;
            string port = txtPort.Text;
            string id = txtId.Text;
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
                UI.Show(UIRes.I18N("FillPassword"));
                return;
            }
            if (Utils.IsNullOrEmpty(security))
            {
                UI.Show(UIRes.I18N("PleaseSelectEncryption"));
                return;
            }

            vmessItem.address = address;
            vmessItem.port = Utils.ToInt(port);
            vmessItem.id = id;
            vmessItem.security = security;
            vmessItem.remarks = remarks;

            if (ConfigHandler.AddShadowsocksServer(ref config, vmessItem, EditIndex) == 0)
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
            this.DialogResult = DialogResult.Cancel;
        }       

    }
}
