using System;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class AddServer6Form : BaseServerForm
    {
        public AddServer6Form()
        {
            InitializeComponent();
        }

        private void AddServer6Form_Load(object sender, EventArgs e)
        {
            cmbFlow.Items.AddRange(Global.xtlsFlows.ToArray());
            transportControl.AllowXtls = true;
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
            cmbFlow.Text = vmessItem.flow;
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
            cmbFlow.Text = "";
            txtRemarks.Text = ""; 

            transportControl.ClearServer(vmessItem);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string address = txtAddress.Text;
            string port = txtPort.Text;
            string id = txtId.Text;
            string flow = cmbFlow.Text;
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

            transportControl.EndBindingServer();

            vmessItem.address = address;
            vmessItem.port = Utils.ToInt(port);
            vmessItem.id = id;
            vmessItem.flow = flow;
            vmessItem.remarks = remarks;

            if (ConfigHandler.AddTrojanServer(ref config, vmessItem, EditIndex) == 0)
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
