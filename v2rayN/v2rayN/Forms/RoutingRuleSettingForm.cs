using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class RoutingRuleSettingForm : BaseForm
    {
        public int EditIndex
        {
            get; set;
        }
        protected RoutingItem routingItem = null;

        private List<int> lvSelecteds = new List<int>();
        public RoutingRuleSettingForm()
        {
            InitializeComponent();
        }

        private void RoutingRuleSettingForm_Load(object sender, EventArgs e)
        {
            if (EditIndex >= 0)
            {
                routingItem = config.routings[EditIndex];
            }
            else
            {
                routingItem = new RoutingItem();
            }
            if (routingItem.rules == null)
            {
                routingItem.rules = new List<RulesItem>();
            }

            txtRemarks.Text = routingItem.remarks ?? string.Empty;
            txtUrl.Text = routingItem.url ?? string.Empty;
            txtCustomIcon.Text = routingItem.customIcon ?? string.Empty;

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
            lvRoutings.RegisterDragEvent(UpdateDragEventHandler);

            lvRoutings.Columns.Add("", 30);
            lvRoutings.Columns.Add("outboundTag", 80);
            lvRoutings.Columns.Add("port", 80);
            lvRoutings.Columns.Add("protocol", 80);
            lvRoutings.Columns.Add("inboundTag", 80);
            lvRoutings.Columns.Add("domain", 160);
            lvRoutings.Columns.Add("ip", 160);
            lvRoutings.Columns.Add("enable", 60);

            lvRoutings.EndUpdate();
        }
        private void UpdateDragEventHandler(int index, int targetIndex)
        {
            if (index < 0 || targetIndex < 0)
            {
                return;
            }
            if (ConfigHandler.MoveRoutingRule(ref routingItem, index, EMove.Position, targetIndex) == 0)
            {
                RefreshRoutingsView();
            }
        }

        private void RefreshRoutingsView()
        {
            lvRoutings.BeginUpdate();
            lvRoutings.Items.Clear();

            for (int k = 0; k < routingItem.rules.Count; k++)
            {
                var item = routingItem.rules[k];

                ListViewItem lvItem = new ListViewItem("");
                Utils.AddSubItem(lvItem, "outboundTag", item.outboundTag);
                Utils.AddSubItem(lvItem, "port", item.port);
                Utils.AddSubItem(lvItem, "protocol", Utils.List2String(item.protocol));
                Utils.AddSubItem(lvItem, "inboundTag", Utils.List2String(item.inboundTag));
                Utils.AddSubItem(lvItem, "domain", Utils.List2String(item.domain));
                Utils.AddSubItem(lvItem, "ip", Utils.List2String(item.ip));
                Utils.AddSubItem(lvItem, "enable", item.enabled.ToString());

                if (lvItem != null) lvRoutings.Items.Add(lvItem);
            }
            lvRoutings.EndUpdate();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            routingItem.remarks = txtRemarks.Text.Trim();
            routingItem.url = txtUrl.Text.Trim();
            routingItem.customIcon = txtCustomIcon.Text.Trim();

            if (ConfigHandler.AddRoutingItem(ref config, routingItem, EditIndex) == 0)
            {
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "PNG|*.png";
            openFileDialog1.ShowDialog();
            txtCustomIcon.Text = openFileDialog1.FileName;

        }

        private void lvRoutings_DoubleClick(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            var fm = new RoutingRuleSettingDetailsForm();
            fm.rulesItem = routingItem.rules[index];
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
                    UI.Show(ResUI.PleaseSelectRules);
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
                UI.Show(ResUI.PleaseSelectRules);
                return;
            }
            if (ConfigHandler.MoveRoutingRule(ref routingItem, index, eMove) == 0)
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
            var fm = new RoutingRuleSettingDetailsForm();
            fm.rulesItem = new RulesItem();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                routingItem.rules.Insert(0, fm.rulesItem);
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
            for (int k = lvSelecteds.Count - 1; k >= 0; k--)
            {
                routingItem.rules.RemoveAt(index);
            }
            RefreshRoutingsView();
        }
        private void menuExportSelectedRules_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();
            var lst = new List<RulesItem>();
            foreach (int v in lvSelecteds)
            {
                lst.Add(routingItem.rules[v]);
            }
            if (lst.Count > 0)
            {
                Utils.SetClipboardData(Utils.ToJson(lst));
                //UI.Show(ResUI.OperationSuccess"));
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

            if (AddBatchRoutingRules(ref routingItem, result) == 0)
            {
                RefreshRoutingsView();
                UI.Show(ResUI.OperationSuccess);
            }
        }

        private void menuImportRulesFromClipboard_Click(object sender, EventArgs e)
        {
            string clipboardData = Utils.GetClipboardData();
            if (AddBatchRoutingRules(ref routingItem, clipboardData) == 0)
            {
                RefreshRoutingsView();
                UI.Show(ResUI.OperationSuccess);
            }
        }
        private void menuImportRulesFromUrl_Click(object sender, EventArgs e)
        {
            var url = txtUrl.Text.Trim();
            if (Utils.IsNullOrEmpty(url))
            {
                UI.Show(ResUI.MsgNeedUrl);
                return;
            }

            Task.Run(async () =>
            {
                DownloadHandle downloadHandle = new DownloadHandle();
                string result = await downloadHandle.DownloadStringAsync(url, false, "");
                if (AddBatchRoutingRules(ref routingItem, result) == 0)
                {
                    RefreshRoutingsView();
                    UI.Show(ResUI.OperationSuccess);
                }
            });
        }
        private int AddBatchRoutingRules(ref RoutingItem routingItem, string clipboardData)
        {
            bool blReplace = false;
            if (UI.ShowYesNo(ResUI.AddBatchRoutingRulesYesNo) == DialogResult.No)
            {
                blReplace = true;
            }
            return ConfigHandler.AddBatchRoutingRules(ref routingItem, clipboardData, blReplace);
        }

        #endregion

    }
}
