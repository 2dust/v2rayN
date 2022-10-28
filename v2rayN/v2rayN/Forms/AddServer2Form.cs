using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

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
            List<string> coreTypes = new List<string>();
            foreach (ECoreType it in Enum.GetValues(typeof(ECoreType)))
            {
                if (it == ECoreType.v2rayN)
                    continue;
                coreTypes.Add(it.ToString());
            }

            cmbCoreType.Items.AddRange(coreTypes.ToArray());
            cmbCoreType.Items.Add(string.Empty);

            txtAddress.ReadOnly = true;
            if (vmessItem != null)
            {
                BindingServer();
            }
            else
            {
                vmessItem = new VmessItem
                {
                    groupId = groupId
                };
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
            txtPreSocksPort.Text = vmessItem.preSocksPort.ToString();

            cmbCoreType.Text = vmessItem.coreType == null ? string.Empty : vmessItem.coreType.ToString();
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
                UI.Show(ResUI.PleaseFillRemarks);
                return;
            }
            if (Utils.IsNullOrEmpty(txtAddress.Text))
            {
                UI.Show(ResUI.FillServerAddressCustom);
                return;
            }
            vmessItem.remarks = remarks;
            vmessItem.preSocksPort = Utils.ToInt(txtPreSocksPort.Text);

            if (Utils.IsNullOrEmpty(cmbCoreType.Text))
            {
                vmessItem.coreType = null;
            }
            else
            {
                vmessItem.coreType = (ECoreType)Enum.Parse(typeof(ECoreType), cmbCoreType.Text);
            }

            if (ConfigHandler.EditCustomServer(ref config, vmessItem) == 0)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = Utils.IsNullOrEmpty(vmessItem.indexId) ? DialogResult.Cancel : DialogResult.OK;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            UI.Show(ResUI.CustomServerTips);

            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Config|*.json|YAML|*.yaml;*.yml|All|*.*"
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

            if (ConfigHandler.AddCustomServer(ref config, vmessItem, false) == 0)
            {
                BindingServer();
                UI.Show(ResUI.SuccessfullyImportedCustomServer);
            }
            else
            {
                UI.ShowWarning(ResUI.FailedImportedCustomServer);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var address = txtAddress.Text;
            if (Utils.IsNullOrEmpty(address))
            {
                UI.Show(ResUI.FillServerAddressCustom);
                return;
            }

            address = Utils.GetConfigPath(address);
            Process.Start(address);
        }
    }
}
