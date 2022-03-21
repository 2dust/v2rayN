using System;
using System.Collections.Generic;
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
            this.Text = (eConfigType).ToString();
            
            cmbCoreType.Items.AddRange(Global.coreTypes.ToArray());
            cmbCoreType.Items.Add(string.Empty);

            switch (eConfigType)
            {
                case EConfigType.Vmess:
                    panVmess.Dock = DockStyle.Fill;
                    panVmess.Visible = true;

                    cmbSecurity.Items.AddRange(Global.vmessSecuritys.ToArray());
                    break;
                case EConfigType.Shadowsocks:
                    panSs.Dock = DockStyle.Fill;
                    panSs.Visible = true;
                    panTran.Visible = false;
                    this.Height = this.Height - panTran.Height;

                    cmbSecurity3.Items.AddRange(LazyConfig.Instance.GetShadowsocksSecuritys().ToArray());
                    break;
                case EConfigType.Socks:
                    panSocks.Dock = DockStyle.Fill;
                    panSocks.Visible = true;
                    panTran.Visible = false;
                    this.Height = this.Height - panTran.Height;
                    break;
                case EConfigType.VLESS:
                    panVless.Dock = DockStyle.Fill;
                    panVless.Visible = true;
                    transportControl.AllowXtls = true;

                    cmbFlow5.Items.AddRange(Global.xtlsFlows.ToArray());
                    break;
                case EConfigType.Trojan:
                    panTrojan.Dock = DockStyle.Fill;
                    panTrojan.Visible = true;
                    transportControl.AllowXtls = true;

                    cmbFlow6.Items.AddRange(Global.xtlsFlows.ToArray());
                    break;
            }

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
            txtPort.Text = vmessItem.port.ToString();

            switch (eConfigType)
            {
                case EConfigType.Vmess:
                    txtId.Text = vmessItem.id;
                    txtAlterId.Text = vmessItem.alterId.ToString();
                    cmbSecurity.Text = vmessItem.security;
                    break;
                case EConfigType.Shadowsocks:
                    txtId3.Text = vmessItem.id;
                    cmbSecurity3.Text = vmessItem.security;
                    break;
                case EConfigType.Socks:
                    txtId4.Text = vmessItem.id;
                    txtSecurity4.Text = vmessItem.security;
                    break;
                case EConfigType.VLESS:
                    txtId5.Text = vmessItem.id;
                    cmbFlow5.Text = vmessItem.flow;
                    cmbSecurity5.Text = vmessItem.security;
                    break;
                case EConfigType.Trojan:
                    txtId6.Text = vmessItem.id;
                    cmbFlow6.Text = vmessItem.flow;
                    break;
            }

            if (vmessItem.coreType == null)
            {
                cmbCoreType.Text = string.Empty;
            }
            else
            {
                cmbCoreType.Text = vmessItem.coreType.ToString();
            }

            transportControl.BindingServer(vmessItem);
        }

        /// <summary>
        /// 清除设置
        /// </summary>
        private void ClearServer()
        {
            txtRemarks.Text = "";
            txtAddress.Text = "";
            txtPort.Text = "";

            switch (eConfigType)
            {
                case EConfigType.Vmess:
                    txtId.Text = "";
                    txtAlterId.Text = "0";
                    cmbSecurity.Text = Global.DefaultSecurity;
                    break;
                case EConfigType.Shadowsocks:
                    txtId3.Text = "";
                    cmbSecurity3.Text = Global.DefaultSecurity;
                    break;
                case EConfigType.Socks:
                    txtId4.Text = "";
                    txtSecurity4.Text = "";
                    break;
                case EConfigType.VLESS:
                    txtId5.Text = "";
                    cmbFlow5.Text = "";
                    cmbSecurity5.Text = Global.None;
                    break;
                case EConfigType.Trojan:
                    txtId6.Text = "";
                    cmbFlow6.Text = "";
                    break;
            }

            transportControl.ClearServer(vmessItem);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string remarks = txtRemarks.Text;
            string address = txtAddress.Text;
            string port = txtPort.Text;

            string id = string.Empty;
            string alterId = string.Empty;
            string security = string.Empty;
            string flow = string.Empty;

            switch (eConfigType)
            {
                case EConfigType.Vmess:
                    id = txtId.Text;
                    alterId = txtAlterId.Text;
                    security = cmbSecurity.Text;
                    break;
                case EConfigType.Shadowsocks:
                    id = txtId3.Text;
                    security = cmbSecurity3.Text;
                    break;
                case EConfigType.Socks:
                    id = txtId4.Text;
                    security = txtSecurity4.Text;
                    break;
                case EConfigType.VLESS:
                    id = txtId5.Text;
                    flow = cmbFlow5.Text;
                    security = cmbSecurity5.Text;
                    break;
                case EConfigType.Trojan:
                    id = txtId6.Text;
                    flow = cmbFlow6.Text;
                    break;
            }

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
            if (eConfigType == EConfigType.Shadowsocks)
            {
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
            }
            if (eConfigType != EConfigType.Socks)
            {
                if (Utils.IsNullOrEmpty(id))
                {
                    UI.Show(UIRes.I18N("FillUUID"));
                    return;
                }
            }

            transportControl.EndBindingServer();

            vmessItem.remarks = remarks;
            vmessItem.address = address;
            vmessItem.port = Utils.ToInt(port);
            vmessItem.id = id;
            vmessItem.alterId = Utils.ToInt(alterId);
            vmessItem.security = security;

            if (Utils.IsNullOrEmpty(cmbCoreType.Text))
            {
                vmessItem.coreType = null;
            }
            else
            {
                vmessItem.coreType = (ECoreType)Enum.Parse(typeof(ECoreType), cmbCoreType.Text);
            }

            int ret = -1;
            switch (eConfigType)
            {
                case EConfigType.Vmess:
                    ret = ConfigHandler.AddServer(ref config, vmessItem);
                    break;
                case EConfigType.Shadowsocks:
                    ret = ConfigHandler.AddShadowsocksServer(ref config, vmessItem);
                    break;
                case EConfigType.Socks:
                    ret = ConfigHandler.AddSocksServer(ref config, vmessItem);
                    break;
                case EConfigType.VLESS:
                    vmessItem.flow = flow;
                    ret = ConfigHandler.AddVlessServer(ref config, vmessItem);
                    break;
                case EConfigType.Trojan:
                    vmessItem.flow = flow;
                    ret = ConfigHandler.AddTrojanServer(ref config, vmessItem);
                    break;
            }

            if (ret == 0)
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
            txtId.Text =
            txtId5.Text = Utils.GetGUID();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
