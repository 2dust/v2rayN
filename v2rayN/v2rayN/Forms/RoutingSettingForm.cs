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
        private RoutingItem lockedItem;
        public RoutingSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingSettingForm_Load(object sender, EventArgs e)
        {
            ConfigHandler.InitBuiltinRouting(ref config);

            cmbdomainStrategy.Text = config.domainStrategy;
            chkenableRoutingAdvanced.Checked = config.enableRoutingAdvanced;
            cmbdomainMatcher.Text = config.domainMatcher;

            if (config.routings == null)
            {
                config.routings = new List<RoutingItem>();
            }
            InitRoutingsView();
            RefreshRoutingsView();

            BindingLockedData();
            InitUI();
        }


        private void tabNormal_Selecting(object sender, TabControlCancelEventArgs e)
        {
            //if (tabNormal.SelectedTab == tabPageRuleList)
            //{
            //    MenuItem1.Enabled = true;
            //}
            //else
            //{
            //    MenuItem1.Enabled = false;
            //}
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            config.domainStrategy = cmbdomainStrategy.Text;
            config.enableRoutingAdvanced = chkenableRoutingAdvanced.Checked;
            config.domainMatcher = cmbdomainMatcher.Text;

            EndBindingLockedData();

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
        private void chkenableRoutingAdvanced_CheckedChanged_1(object sender, EventArgs e)
        {
            InitUI();
        }
        private void InitUI()
        {
            if (chkenableRoutingAdvanced.Checked)
            {
                this.tabPageProxy.Parent = null;
                this.tabPageDirect.Parent = null;
                this.tabPageBlock.Parent = null;
                this.tabPageRuleList.Parent = tabNormal;
                MenuItemBasic.Enabled = false;
                MenuItemAdvanced.Enabled = true;

            }
            else
            {
                this.tabPageProxy.Parent = tabNormal;
                this.tabPageDirect.Parent = tabNormal;
                this.tabPageBlock.Parent = tabNormal;
                this.tabPageRuleList.Parent = null;
                MenuItemBasic.Enabled = true;
                MenuItemAdvanced.Enabled = false;
            }

        }


        #region locked
        private void BindingLockedData()
        {
            lockedItem = ConfigHandler.GetLockedRoutingItem(ref config);
            if (lockedItem != null)
            {
                txtProxyDomain.Text = Utils.List2String(lockedItem.rules[0].domain, true);
                txtProxyIp.Text = Utils.List2String(lockedItem.rules[0].ip, true);

                txtDirectDomain.Text = Utils.List2String(lockedItem.rules[1].domain, true);
                txtDirectIp.Text = Utils.List2String(lockedItem.rules[1].ip, true);

                txtBlockDomain.Text = Utils.List2String(lockedItem.rules[2].domain, true);
                txtBlockIp.Text = Utils.List2String(lockedItem.rules[2].ip, true);
            }
        }
        private void EndBindingLockedData()
        {
            if (lockedItem != null)
            {
                lockedItem.rules[0].domain = Utils.String2List(txtProxyDomain.Text.TrimEx());
                lockedItem.rules[0].ip = Utils.String2List(txtProxyIp.Text.TrimEx());

                lockedItem.rules[1].domain = Utils.String2List(txtDirectDomain.Text.TrimEx());
                lockedItem.rules[1].ip = Utils.String2List(txtDirectIp.Text.TrimEx());

                lockedItem.rules[2].domain = Utils.String2List(txtBlockDomain.Text.TrimEx());
                lockedItem.rules[2].ip = Utils.String2List(txtBlockIp.Text.TrimEx());

            }
        }
        #endregion

        #region ListView
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
            lvRoutings.Columns.Add(UIRes.I18N("LvCount"), 60);
            lvRoutings.Columns.Add(UIRes.I18N("LvUrl"), 240);

            lvRoutings.EndUpdate();
        }

        private void RefreshRoutingsView()
        {
            lvRoutings.BeginUpdate();
            lvRoutings.Items.Clear();

            for (int k = 0; k < config.routings.Count; k++)
            {
                var item = config.routings[k];
                if (item.locked == true)
                {
                    continue;
                }

                string def = string.Empty;
                if (config.routingIndex.Equals(k))
                {
                    def = "√";
                }

                ListViewItem lvItem = new ListViewItem(def);
                Utils.AddSubItem(lvItem, "remarks", item.remarks);
                Utils.AddSubItem(lvItem, "count", item.rules.Count.ToString());
                Utils.AddSubItem(lvItem, "url", item.url);

                if (lvItem != null) lvRoutings.Items.Add(lvItem);
            }
            lvRoutings.EndUpdate();
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

        #endregion


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

        private void menuImportBasicRules_Click(object sender, EventArgs e)
        {
            //Extra to bypass the mainland
            txtProxyDomain.Text = "geosite:google";
            txtDirectDomain.Text = "geosite:cn";
            txtDirectIp.Text = "geoip:private,geoip:cn";

            txtBlockDomain.Text = "geosite:category-ads-all";

            UI.Show(UIRes.I18N("OperationSuccess"));
        }

        #endregion

    }
}
