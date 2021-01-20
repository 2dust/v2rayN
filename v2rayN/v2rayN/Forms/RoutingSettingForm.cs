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
            cmbdomainStrategy.Text = config.domainStrategy;

            if (config.rules == null)
            {
                config.rules = new List<RulesItem>();
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
            lvRoutings.Columns.Add(UIRes.I18N("LvAlias"), 100);
            lvRoutings.Columns.Add("outboundTag", 80);
            lvRoutings.Columns.Add("port", 80);
            lvRoutings.Columns.Add("protocol", 100);
            lvRoutings.Columns.Add("domain", 160);
            lvRoutings.Columns.Add("ip", 160);

            lvRoutings.EndUpdate();
        }

        private void RefreshRoutingsView()
        {
            lvRoutings.BeginUpdate();
            lvRoutings.Items.Clear();

            for (int k = 0; k < config.rules.Count; k++)
            {
                var item = config.rules[k];

                ListViewItem lvItem = new ListViewItem("");
                Utils.AddSubItem(lvItem, "remarks", item.remarks);
                Utils.AddSubItem(lvItem, "outboundTag", item.outboundTag);
                Utils.AddSubItem(lvItem, "port", item.port);
                Utils.AddSubItem(lvItem, "protocol", Utils.List2String(item.protocol));
                Utils.AddSubItem(lvItem, "domain", Utils.List2String(item.domain));
                Utils.AddSubItem(lvItem, "ip", Utils.List2String(item.ip));

                if (lvItem != null) lvRoutings.Items.Add(lvItem);
            }
            lvRoutings.EndUpdate();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            config.domainStrategy = cmbdomainStrategy.Text;

            if (ConfigHandler.SaveRoutingRulesItem(ref config) == 0)
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
            var fm = new RoutingSettingDetailsForm();
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

        private void menuMoveTop_Click(object sender, EventArgs e)
        {
            MoveRule(EMove.Top);
        }

        private void menuMoveUp_Click(object sender, EventArgs e)
        {
            MoveRule(EMove.Up);
        }

        private void menuMoveDown_Click(object sender, EventArgs e)
        {
            MoveRule(EMove.Down);
        }

        private void menuMoveBottom_Click(object sender, EventArgs e)
        {
            MoveRule(EMove.Bottom);
        }

        private void MoveRule(EMove eMove)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectRules"));
                return;
            }
            if (ConfigHandler.MoveRoutingRule(ref config, index, eMove) == 0)
            {
                RefreshRoutingsView();
            }
        }
        private void menuSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvRoutings.Items)
            {
                item.Selected = true;
            }
        }

        private void menuAdd_Click(object sender, EventArgs e)
        {
            var fm = new RoutingSettingDetailsForm();
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
                config.rules.RemoveAt(index);
            }
            RefreshRoutingsView();
        }
        private void menuExportSelectedRules_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();
            var lst = new List<RulesItem>();
            foreach (int v in lvSelecteds)
            {
                lst.Add(config.rules[v]);
            }
            if (lst.Count > 0)
            {
                Utils.SetClipboardData(Utils.ToJson(lst));
                UI.Show(UIRes.I18N("OperationSuccess"));
            }

        }

        private void lvRoutings_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A:
                        menuSelectAll_Click(null, null);
                        break;
                    case Keys.C:
                        menuExportSelectedRules_Click(null, null);
                        break;
                }
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Delete:
                        menuRemove_Click(null, null);
                        break;
                    case Keys.T:
                        menuMoveTop_Click(null, null);
                        break;
                    case Keys.B:
                        menuMoveBottom_Click(null, null);
                        break;
                    case Keys.U:
                        menuMoveUp_Click(null, null);
                        break;
                    case Keys.D:
                        menuMoveDown_Click(null, null);
                        break;
                }
            }
        }
        #endregion

        #region preset rules
        private void menuImportRulesFromPreset_Click(object sender, EventArgs e)
        {
            var rules = Utils.GetEmbedText(Global.CustomRoutingFileName + "rules");
            if (ConfigHandler.AddBatchRoutingRules(ref config, rules) == 0)
            {
                RefreshRoutingsView();
                UI.Show(UIRes.I18N("OperationSuccess"));
            }
        }

        private void menuImportRulesFromFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Rules|*.json|All|*.*"
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
            string result = Utils.LoadResource(fileName);
            if (Utils.IsNullOrEmpty(result))
            {
                return;
            }
            if (ConfigHandler.AddBatchRoutingRules(ref config, result) == 0)
            {
                RefreshRoutingsView();
                UI.Show(UIRes.I18N("OperationSuccess"));
            }
        }

        private void menuImportRulesFromClipboard_Click(object sender, EventArgs e)
        {
            string clipboardData = Utils.GetClipboardData();
            if (ConfigHandler.AddBatchRoutingRules(ref config, clipboardData) == 0)
            {
                RefreshRoutingsView();
                UI.Show(UIRes.I18N("OperationSuccess"));
            }
        }
        private void menuImportRulesFromUrl_Click(object sender, EventArgs e)
        {
            var fm = new RoutingSubSettingForm();
            if (fm.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            var url = fm.Url;
            DownloadHandle downloadHandle = new DownloadHandle();
            string clipboardData = downloadHandle.WebDownloadStringSync(url);
            if (ConfigHandler.AddBatchRoutingRules(ref config, clipboardData) == 0)
            {
                RefreshRoutingsView();
                UI.Show(UIRes.I18N("OperationSuccess"));
            }
        }

        #endregion


    }
}
