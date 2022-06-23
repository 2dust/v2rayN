using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class GroupSettingForm : BaseForm
    {
        List<GroupSettingControl> lstControls = new List<GroupSettingControl>();

        public GroupSettingForm()
        {
            InitializeComponent();
        }

        private void GroupSettingForm_Load(object sender, EventArgs e)
        {
            if (config.groupItem == null)
            {
                config.groupItem = new List<GroupItem>();
            }

            RefreshGroupsView();
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshGroupsView()
        {
            panCon.Controls.Clear();
            lstControls.Clear();

            for (int k = config.groupItem.Count - 1; k >= 0; k--)
            {
                GroupItem item = config.groupItem[k];
                if (Utils.IsNullOrEmpty(item.remarks))
                {
                    if (!Utils.IsNullOrEmpty(item.id))
                    {
                        ConfigHandler.RemoveGroupItem(ref config, item.id);
                    }
                    config.groupItem.RemoveAt(k);
                }
            }

            foreach (GroupItem item in config.groupItem)
            {
                GroupSettingControl control = new GroupSettingControl();
                control.OnButtonClicked += Control_OnButtonClicked;
                control.groupItem = item;
                control.Dock = DockStyle.Top;

                panCon.Controls.Add(control);
                panCon.Controls.SetChildIndex(control, 0);

                lstControls.Add(control);
            }
        }

        private void Control_OnButtonClicked(object sender, EventArgs e)
        {
            RefreshGroupsView();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {            
            if (ConfigHandler.SaveGroupItem(ref config) == 0)
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
            AddGroup();

            RefreshGroupsView();
        }


        private void AddGroup()
        {
            GroupItem groupItem = new GroupItem
            {
                id = string.Empty,
                remarks = "Group"
            };
            config.groupItem.Add(groupItem);
        }
    }
}
