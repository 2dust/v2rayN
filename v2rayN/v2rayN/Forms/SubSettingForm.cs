using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class SubSettingForm : BaseForm
    {
        List<SubSettingControl> lstControls = new List<SubSettingControl>();

        public SubSettingForm()
        {
            InitializeComponent();
        }

        private void SubSettingForm_Load(object sender, EventArgs e)
        {
            config.subItem ??= new List<SubItem>();

            RefreshSubsView();
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshSubsView()
        {
            panCon.Controls.Clear();
            lstControls.Clear();

            for (int k = config.subItem.Count - 1; k >= 0; k--)
            {
                SubItem item = config.subItem[k];
                if (Utils.IsNullOrEmpty(item.remarks)
                    && Utils.IsNullOrEmpty(item.url))
                {
                    if (!Utils.IsNullOrEmpty(item.id))
                    {
                        ConfigHandler.RemoveServerViaSubid(ref config, item.id);
                    }
                    config.subItem.RemoveAt(k);
                }
            }

            foreach (SubItem item in config.subItem)
            {
                SubSettingControl control = new SubSettingControl();
                control.OnButtonClicked += Control_OnButtonClicked;
                control.subItem = item;
                control.Dock = DockStyle.Top;

                panCon.Controls.Add(control);
                panCon.Controls.SetChildIndex(control, 0);

                lstControls.Add(control);
            }
        }

        private void Control_OnButtonClicked(object sender, EventArgs e)
        {
            RefreshSubsView();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (ConfigHandler.SaveSubItem(ref config) == 0)
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
            DialogResult = DialogResult.Cancel;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSub();

            RefreshSubsView();
        }


        private void AddSub()
        {
            SubItem subItem = new SubItem
            {
                id = string.Empty,
                remarks = "remarks",
                url = "url"
            };
            config.subItem.Add(subItem);
        }
    }
}
