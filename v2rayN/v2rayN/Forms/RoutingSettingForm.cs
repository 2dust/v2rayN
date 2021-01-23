using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class RoutingSettingForm : BaseForm
    {
        private List<int> lvSelecteds = new List<int>();
        public RoutingSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingSettingForm_Load(object sender, EventArgs e)
        {
            ConfigHandler.InitBuiltinRouting(ref config);

            cmbdomainStrategy.Text = config.domainStrategy;

            if (config.routings == null)
            {
                config.routings = new List<RoutingItem>();
            }
            InitRoutingsView();
            RefreshRoutingsView();
        }

        private void InitRoutingsView()
        {
            lvRoutings.BeginUpdate();
            lvRoutings.Items.Clear();

            lvRoutings.GridLines = true;
            lvRoutings.FullRowSelect = true;
            lvRoutings.View = View.Details;
            lvRoutings.MultiSelect = true;
            lvRoutings.HeaderStyle = ColumnHeaderStyle.Clickable;

            lvRoutings.Columns.Add("", 30);
            lvRoutings.Columns.Add(UIRes.I18N("LvAlias"), 200);
            lvRoutings.Columns.Add(UIRes.I18N("LvUrl"), 240);
            lvRoutings.Columns.Add(UIRes.I18N("LvCount"), 60);

            lvRoutings.EndUpdate();
        }

        private void RefreshRoutingsView()
        {
            lvRoutings.BeginUpdate();
            lvRoutings.Items.Clear();

            for (int k = 0; k < config.routings.Count; k++)
            {
                string def = string.Empty;
                if (config.routingIndex.Equals(k))
                {
                    def = "√";
                }

                var item = config.routings[k];

                ListViewItem lvItem = new ListViewItem(def);
                Utils.AddSubItem(lvItem, "remarks", item.remarks);
                Utils.AddSubItem(lvItem, "url", item.url);
                Utils.AddSubItem(lvItem, "count", item.rules.Count.ToString());

                if (lvItem != null) lvRoutings.Items.Add(lvItem);
            }
            lvRoutings.EndUpdate();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.domainStrategy = cmbdomainStrategy.Text;

            if (ConfigHandler.SaveRouting(ref config) == 0)
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

        private void linkLabelRoutingDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.v2fly.org/config/routing.html");
        }

        private void lvRoutings_DoubleClick(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            var fm = new RoutingRuleSettingForm();
            fm.EditIndex = index;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshRoutingsView();
            }
        }

        private int GetLvSelectedIndex()
        {
            int index = -1;
            lvSelecteds.Clear();
            try
            {
                if (lvRoutings.SelectedIndices.Count <= 0)
                {
                    UI.Show(UIRes.I18N("PleaseSelectRules"));
                    return index;
                }

                index = lvRoutings.SelectedIndices[0];
                foreach (int i in lvRoutings.SelectedIndices)
                {
                    lvSelecteds.Add(i);
                }
                return index;
            }
            catch
            {
                return index;
            }
        }

        #region Edit function


        private void menuSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvRoutings.Items)
            {
                item.Selected = true;
            }
        }

        private void menuAdd_Click(object sender, EventArgs e)
        {
            var fm = new RoutingRuleSettingForm();
            fm.EditIndex = -1;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshRoutingsView();
            }
        }

        private void menuRemove_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (UI.ShowYesNo(UIRes.I18N("RemoveRules")) == DialogResult.No)
            {
                return;
            }
            for (int k = lvSelecteds.Count - 1; k >= 0; k--)
            {
                config.routings.RemoveAt(index);
            }
            RefreshRoutingsView();
        }
        private void menuSetDefaultRouting_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            SetDefaultRouting(index);
        }
        private int SetDefaultRouting(int index)
        {
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectServer"));
                return -1;
            }
            if (ConfigHandler.SetDefaultRouting(ref config, index) == 0)
            {
                RefreshRoutingsView();
            }
            return 0;
        }
        #endregion


    }
}
