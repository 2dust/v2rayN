using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

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
            if (config.subItem == null)
            {
                config.subItem = new List<SubItem>();
            }

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
                var item = config.subItem[k];
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

            foreach (var item in config.subItem)
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
            if (config.subItem.Count <= 0)
            {
                AddSub();
            }

            if (ConfigHandler.SaveSubItem(ref config) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.Show(UIRes.I18N("OperationFailed"));
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSub();

            RefreshSubsView();
        }


        private void AddSub()
        {
            var subItem = new SubItem
            {
                id = string.Empty,
                remarks = "remarks",
                url = "url"
            };
            config.subItem.Add(subItem);
        }
    }
}
