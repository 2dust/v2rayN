using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class RoutingSettingForm : BaseForm
    {
        private readonly List<int> _lvSelecteds = new List<int>();
        private RoutingItem _lockedItem;
        public RoutingSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingSettingForm_Load(object sender, EventArgs e)
        {
            ConfigHandler.InitBuiltinRouting(ref config);
            cmbdomainMatcher.Items.AddRange(Global.domainMatchers.ToArray());

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
        private void chkenableRoutingAdvanced_CheckedChanged_1(object sender, EventArgs e)
        {
            InitUI();
        }
        private void InitUI()
        {
            if (chkenableRoutingAdvanced.Checked)
            {
                tabPageProxy.Parent = null;
                tabPageDirect.Parent = null;
                tabPageBlock.Parent = null;
                tabPageRuleList.Parent = tabNormal;
                MenuItemBasic.Enabled = false;
                MenuItemAdvanced.Enabled = true;

            }
            else
            {
                tabPageProxy.Parent = tabNormal;
                tabPageDirect.Parent = tabNormal;
                tabPageBlock.Parent = tabNormal;
                tabPageRuleList.Parent = null;
                MenuItemBasic.Enabled = true;
                MenuItemAdvanced.Enabled = false;
            }

        }


        #region locked
        private void BindingLockedData()
        {
            _lockedItem = ConfigHandler.GetLockedRoutingItem(ref config);
            if (_lockedItem != null)
            {
                txtProxyDomain.Text = Utils.List2String(_lockedItem.rules[0].domain, true);
                txtProxyIp.Text = Utils.List2String(_lockedItem.rules[0].ip, true);

                txtDirectDomain.Text = Utils.List2String(_lockedItem.rules[1].domain, true);
                txtDirectIp.Text = Utils.List2String(_lockedItem.rules[1].ip, true);

                txtBlockDomain.Text = Utils.List2String(_lockedItem.rules[2].domain, true);
                txtBlockIp.Text = Utils.List2String(_lockedItem.rules[2].ip, true);
            }
        }
        private void EndBindingLockedData()
        {
            if (_lockedItem != null)
            {
                _lockedItem.rules[0].domain = Utils.String2List(txtProxyDomain.Text.TrimEx());
                _lockedItem.rules[0].ip = Utils.String2List(txtProxyIp.Text.TrimEx());

                _lockedItem.rules[1].domain = Utils.String2List(txtDirectDomain.Text.TrimEx());
                _lockedItem.rules[1].ip = Utils.String2List(txtDirectIp.Text.TrimEx());

                _lockedItem.rules[2].domain = Utils.String2List(txtBlockDomain.Text.TrimEx());
                _lockedItem.rules[2].ip = Utils.String2List(txtBlockIp.Text.TrimEx());

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
            lvRoutings.Columns.Add(ResUI.LvAlias, 200);
            lvRoutings.Columns.Add(ResUI.LvCount, 60);
            lvRoutings.Columns.Add(ResUI.LvUrl, 240);
            lvRoutings.Columns.Add(ResUI.LvCustomIcon, 240);

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
                Utils.AddSubItem(lvItem, "customIcon", item.customIcon);

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
            _lvSelecteds.Clear();
            try
            {
                if (lvRoutings.SelectedIndices.Count <= 0)
                {
                    UI.Show(ResUI.PleaseSelectRules);
                    return index;
                }

                index = lvRoutings.SelectedIndices[0];
                foreach (int i in lvRoutings.SelectedIndices)
                {
                    _lvSelecteds.Add(i);
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
            if (UI.ShowYesNo(ResUI.RemoveRules) == DialogResult.No)
            {
                return;
            }
            for (int k = _lvSelecteds.Count - 1; k >= 0; k--)
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
                UI.Show(ResUI.PleaseSelectServer);
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

            UI.Show(ResUI.OperationSuccess);
        }

        private void menuImportAdvancedRules_Click(object sender, EventArgs e)
        {
            if (ConfigHandler.InitBuiltinRouting(ref config, true) == 0)
            {
                RefreshRoutingsView();
            }
        }

        #endregion

    }
}
