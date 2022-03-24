using NHotkey;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Tool;
using System.Linq;

namespace v2rayN.Forms
{
    public partial class MainForm : BaseForm
    {
        private V2rayHandler v2rayHandler;
        private List<VmessItem> lstSelecteds = new List<VmessItem>();
        private StatisticsHandler statistics = null;
        private string MsgFilter = string.Empty;
        private List<VmessItem> lstVmess = null;
        private string groupId = string.Empty;

        #region Window 事件

        public MainForm()
        {
            InitializeComponent();
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            HideForm();
            this.Text = Utils.GetVersion();
            Global.processJob = new Job();

            Application.ApplicationExit += (sender, args) =>
            {
                MyAppExit(false);
            };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (ConfigHandler.LoadConfig(ref config) != 0)
            {
                UI.ShowWarning($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
                Environment.Exit(0);
                return;
            }

            ConfigHandler.InitBuiltinRouting(ref config);
            MainFormHandler.Instance.BackupGuiNConfig(config, true);
            v2rayHandler = new V2rayHandler();
            v2rayHandler.ProcessEvent += v2rayHandler_ProcessEvent;

            if (config.enableStatistics)
            {
                statistics = new StatisticsHandler(config, UpdateStatisticsHandler);
            }
        }

        private void MainForm_VisibleChanged(object sender, EventArgs e)
        {
            if (statistics == null || !statistics.Enable) return;
            if ((sender as Form).Visible)
            {
                statistics.UpdateUI = true;
            }
            else
            {
                statistics.UpdateUI = false;
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            InitGroupView();
            InitServersView();
            RefreshServers();
            RefreshRoutingsMenu();
            RestoreUI();

            _ = LoadV2ray();

            HideForm();

            MainFormHandler.Instance.UpdateTask(config, UpdateTaskHandler);
            MainFormHandler.Instance.RegisterGlobalHotkey(config, OnHotkeyHandler, UpdateTaskHandler);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            switch (e.CloseReason)
            {
                case CloseReason.UserClosing:
                    StorageUI();
                    e.Cancel = true;
                    HideForm();
                    break;
                case CloseReason.ApplicationExitCall:
                case CloseReason.FormOwnerClosing:
                case CloseReason.TaskManagerClosing:
                    MyAppExit(false);
                    break;
                case CloseReason.WindowsShutDown:
                    MyAppExit(true);
                    break;
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            //if (this.WindowState == FormWindowState.Minimized)
            //{
            //    HideForm();
            //}
            //else
            //{

            //}
        }
        private void MyAppExit(bool blWindowsShutDown)
        {
            try
            {
                v2rayHandler.V2rayStop();

                //HttpProxyHandle.CloseHttpAgent(config);
                if (blWindowsShutDown)
                {
                    SysProxyHandle.ResetIEProxy4WindowsShutDown();
                }
                else
                {
                    SysProxyHandle.UpdateSysProxy(config, true);
                }

                StorageUI();
                ConfigHandler.SaveConfig(ref config);
                statistics?.SaveToFile();
                statistics?.Close();
            }
            catch { }
        }

        private void RestoreUI()
        {
            scMain.Panel2Collapsed = true;

            if (!config.uiItem.mainSize.IsEmpty)
            {
                this.Width = config.uiItem.mainSize.Width;
                this.Height = config.uiItem.mainSize.Height;
            }

            for (int k = 0; k < lvServers.Columns.Count; k++)
            {
                var width = ConfigHandler.GetformMainLvColWidth(ref config, ((EServerColName)k).ToString(), lvServers.Columns[k].Width);
                lvServers.Columns[k].Width = width;
            }
        }

        private void StorageUI()
        {
            config.uiItem.mainSize = new Size(this.Width, this.Height);

            for (int k = 0; k < lvServers.Columns.Count; k++)
            {
                ConfigHandler.AddformMainLvColWidth(ref config, ((EServerColName)k).ToString(), lvServers.Columns[k].Width);
            }
        }

        private void OnHotkeyHandler(object sender, HotkeyEventArgs e)
        {
            switch (Utils.ToInt(e.Name))
            {
                case (int)EGlobalHotkey.ShowForm:
                    if (this.ShowInTaskbar) HideForm(); else ShowForm();
                    break;
                case (int)EGlobalHotkey.SystemProxyClear:
                    SetListenerType(ESysProxyType.ForcedClear);
                    break;
                case (int)EGlobalHotkey.SystemProxySet:
                    SetListenerType(ESysProxyType.ForcedChange);
                    break;
                case (int)EGlobalHotkey.SystemProxyUnchanged:
                    SetListenerType(ESysProxyType.Unchanged);
                    break;
            }
            e.Handled = true;
        }

        #endregion

        #region 显示服务器 listview 和 menu

        /// <summary>
        /// 刷新服务器
        /// </summary>
        private void RefreshServers()
        {
            lstVmess = config.vmess
                .Where(it => Utils.IsNullOrEmpty(groupId) ? true : it.groupId == groupId)
                .OrderBy(it => it.sort)
                .ToList();

            ConfigHandler.SetDefaultServer(config, lstVmess);
            RefreshServersView();
            RefreshServersMenu();
        }

        /// <summary>
        /// 初始化服务器列表
        /// </summary>
        private void InitServersView()
        {
            lvServers.BeginUpdate();
            lvServers.Items.Clear();

            lvServers.GridLines = true;
            lvServers.FullRowSelect = true;
            lvServers.View = View.Details;
            lvServers.Scrollable = true;
            lvServers.MultiSelect = true;
            lvServers.HeaderStyle = ColumnHeaderStyle.Clickable;
            lvServers.RegisterDragEvent(UpdateDragEventHandler);

            lvServers.Columns.Add("", 30);
            lvServers.Columns.Add(UIRes.I18N("LvServiceType"), 80);
            lvServers.Columns.Add(UIRes.I18N("LvAlias"), 100);
            lvServers.Columns.Add(UIRes.I18N("LvAddress"), 120);
            lvServers.Columns.Add(UIRes.I18N("LvPort"), 100);
            lvServers.Columns.Add(UIRes.I18N("LvEncryptionMethod"), 120);
            lvServers.Columns.Add(UIRes.I18N("LvTransportProtocol"), 120);
            lvServers.Columns.Add(UIRes.I18N("LvTLS"), 100);
            lvServers.Columns.Add(UIRes.I18N("LvSubscription"), 100);
            lvServers.Columns.Add(UIRes.I18N("LvTestResults"), 120, HorizontalAlignment.Right);

            if (statistics != null && statistics.Enable)
            {
                lvServers.Columns.Add(UIRes.I18N("LvTodayDownloadDataAmount"), 70);
                lvServers.Columns.Add(UIRes.I18N("LvTodayUploadDataAmount"), 70);
                lvServers.Columns.Add(UIRes.I18N("LvTotalDownloadDataAmount"), 70);
                lvServers.Columns.Add(UIRes.I18N("LvTotalUploadDataAmount"), 70);
            }
            lvServers.EndUpdate();
        }

        private void UpdateDragEventHandler(int index, int targetIndex)
        {
            if (index < 0 || targetIndex < 0)
            {
                return;
            }
            if (ConfigHandler.MoveServer(ref config, ref lstVmess, index, EMove.Position, targetIndex) == 0)
            {
                RefreshServers();
            }
        }

        /// <summary>
        /// 刷新服务器列表
        /// </summary>
        private void RefreshServersView()
        {
            int index = lvServers.SelectedIndices.Count > 0 ? lvServers.SelectedIndices[0] : -1;

            lvServers.BeginUpdate();
            lvServers.Items.Clear();

            for (int k = 0; k < lstVmess.Count; k++)
            {
                string def = string.Empty;
                string totalUp = string.Empty,
                        totalDown = string.Empty,
                        todayUp = string.Empty,
                        todayDown = string.Empty;

                VmessItem item = lstVmess[k];
                if (config.IsActiveNode(item))
                {
                    def = "√";
                }

                bool stats = statistics != null && statistics.Enable;
                if (stats)
                {
                    ServerStatItem sItem = statistics.Statistic.Find(item_ => item_.itemId == item.indexId);
                    if (sItem != null)
                    {
                        totalUp = Utils.HumanFy(sItem.totalUp);
                        totalDown = Utils.HumanFy(sItem.totalDown);
                        todayUp = Utils.HumanFy(sItem.todayUp);
                        todayDown = Utils.HumanFy(sItem.todayDown);
                    }
                }
                ListViewItem lvItem = new ListViewItem(def);
                Utils.AddSubItem(lvItem, EServerColName.configType.ToString(), (item.configType).ToString());
                Utils.AddSubItem(lvItem, EServerColName.remarks.ToString(), item.remarks);
                Utils.AddSubItem(lvItem, EServerColName.address.ToString(), item.address);
                Utils.AddSubItem(lvItem, EServerColName.port.ToString(), item.port.ToString());
                Utils.AddSubItem(lvItem, EServerColName.security.ToString(), item.security);
                Utils.AddSubItem(lvItem, EServerColName.network.ToString(), item.network);
                Utils.AddSubItem(lvItem, EServerColName.streamSecurity.ToString(), item.streamSecurity);
                Utils.AddSubItem(lvItem, EServerColName.subRemarks.ToString(), item.GetSubRemarks(config));
                Utils.AddSubItem(lvItem, EServerColName.testResult.ToString(), item.testResult);
                if (stats)
                {
                    Utils.AddSubItem(lvItem, EServerColName.todayDown.ToString(), todayDown);
                    Utils.AddSubItem(lvItem, EServerColName.todayUp.ToString(), todayUp);
                    Utils.AddSubItem(lvItem, EServerColName.totalDown.ToString(), totalDown);
                    Utils.AddSubItem(lvItem, EServerColName.totalUp.ToString(), totalUp);
                }

                if (k % 2 == 1) // 隔行着色
                {
                    lvItem.BackColor = Color.WhiteSmoke;
                }
                if (config.IsActiveNode(item))
                {
                    //lvItem.Checked = true;
                    lvItem.ForeColor = Color.DodgerBlue;
                    lvItem.Font = new Font(lvItem.Font, FontStyle.Bold);
                }

                if (lvItem != null) lvServers.Items.Add(lvItem);
            }
            lvServers.EndUpdate();

            if (index >= 0 && index < lvServers.Items.Count && lvServers.Items.Count > 0)
            {
                lvServers.Items[index].Selected = true;
                lvServers.EnsureVisible(index); // workaround
            }
        }

        /// <summary>
        /// 刷新托盘服务器菜单
        /// </summary>
        private void RefreshServersMenu()
        {
            menuServers.DropDownItems.Clear();
            menuServers2.SelectedIndexChanged -= MenuServers2_SelectedIndexChanged;
            menuServers2.Items.Clear();
            menuServers.Visible = false;
            menuServers2.Visible = false;

            if (lstVmess.Count > 20)
            {
                for (int k = 0; k < lstVmess.Count; k++)
                {
                    VmessItem item = lstVmess[k];
                    string name = item.GetSummary();

                    if (config.IsActiveNode(item))
                    {
                        name = $"√ {name}";
                    }
                    menuServers2.Items.Add(name);

                }
                menuServers2.SelectedIndex = lstVmess.FindIndex(it => it.indexId == config.indexId);
                menuServers2.SelectedIndexChanged += MenuServers2_SelectedIndexChanged;
                menuServers2.Visible = true;
            }
            else
            {
                List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
                for (int k = 0; k < lstVmess.Count; k++)
                {
                    VmessItem item = lstVmess[k];
                    string name = item.GetSummary();

                    ToolStripMenuItem ts = new ToolStripMenuItem(name)
                    {
                        Tag = k
                    };
                    if (config.IsActiveNode(item))
                    {
                        ts.Checked = true;
                    }
                    ts.Click += new EventHandler(ts_Click);
                    lst.Add(ts);
                }
                menuServers.DropDownItems.AddRange(lst.ToArray());
                menuServers.Visible = true;
            }
        }

        private void MenuServers2_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetDefaultServer(((ToolStripComboBox)sender).SelectedIndex);
        }

        private void ts_Click(object sender, EventArgs e)
        {
            try
            {
                ToolStripItem ts = (ToolStripItem)sender;
                int index = Utils.ToInt(ts.Tag);
                SetDefaultServer(index);
            }
            catch
            {
            }
        }

        private void lvServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = -1;
            try
            {
                if (lvServers.SelectedIndices.Count > 0)
                {
                    index = lvServers.SelectedIndices[0];
                }
            }
            catch
            {
            }
            if (index < 0)
            {
                return;
            }
            //qrCodeControl.showQRCode(index, config);
        }

        private void DisplayToolStatus()
        {
            toolSslInboundInfo.Text = $"{Global.InboundSocks} {Global.Loopback}:{config.inbound[0].localPort} | "
             + $"{ Global.InboundHttp} { Global.Loopback}:{Global.httpPort}";

            notifyMain.Icon = MainFormHandler.Instance.GetNotifyIcon(config, this.Icon);
        }
        private void ssMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!Utils.IsNullOrEmpty(e.ClickedItem.Text))
            {
                Utils.SetClipboardData(e.ClickedItem.Text);
            }
        }

        private void lvServers_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column < 0)
            {
                return;
            }

            try
            {
                if ((EServerColName)e.Column == EServerColName.def)
                {
                    foreach (ColumnHeader it in lvServers.Columns)
                    {
                        it.Width = -2;
                    }
                    return;
                }

                var tag = lvServers.Columns[e.Column].Tag?.ToString();
                bool asc = Utils.IsNullOrEmpty(tag) ? true : !Convert.ToBoolean(tag);
                if (ConfigHandler.SortServers(ref config, ref lstVmess, (EServerColName)e.Column, asc) != 0)
                {
                    return;
                }
                lvServers.Columns[e.Column].Tag = Convert.ToString(asc);
                RefreshServers();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }

            if (e.Column < 0)
            {
                return;
            }

        }

        private void InitGroupView()
        {
            tabGroup.TabPages.Clear();

            string title = $"  {UIRes.I18N("AllGroupServers")}   ";
            var tabPage = new TabPage(title);
            tabPage.Name = "";
            tabGroup.TabPages.Add(tabPage);

            foreach (var item in config.groupItem)
            {
                var tabPage2 = new TabPage($"   {item.remarks}   ");
                tabPage2.Name = item.id;
                tabGroup.TabPages.Add(tabPage2);
            }

            tabGroup.SelectedIndex = 0;

            //menuMoveToGroup
            menuMoveToGroup.DropDownItems.Clear();

            List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
            foreach (var item in config.groupItem)
            {
                string name = item.remarks;

                ToolStripMenuItem ts = new ToolStripMenuItem(name)
                {
                    Tag = item.id,
                };
                ts.Click += new EventHandler(ts_Group_Click);
                lst.Add(ts);
            }
            menuMoveToGroup.DropDownItems.AddRange(lst.ToArray());
        }

        private void tabGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabGroup.SelectedIndex < 0)
            {
                return;
            }
            groupId = string.Empty;
            //groupId = tabGroup.TabPages[tabGroup.SelectedIndex].Name;
            groupId = tabGroup.SelectedTab.Name;

            RefreshServers();

            lvServers.Focus();
        }

        private void ts_Group_Click(object sender, EventArgs e)
        {
            try
            {
                ToolStripItem ts = (ToolStripItem)sender;
                var groupIdSelected = Utils.ToString(ts.Tag);

                int index = GetLvSelectedIndex();
                if (index < 0)
                {
                    return;
                }

                if (ConfigHandler.MoveServerToGroup(config, lstSelecteds, groupIdSelected) == 0)
                {
                    RefreshServers();
                }
            }
            catch
            {
            }
        }
        #endregion

        #region v2ray 操作

        /// <summary>
        /// 载入V2ray
        /// </summary>
        async Task LoadV2ray()
        {
            tsbReload.Enabled = false;

            if (Global.reloadV2ray)
            {
                ClearMsg();
            }
            await Task.Run(() =>
            {
                v2rayHandler.LoadV2ray(config);
            });

            Global.reloadV2ray = false;
            ConfigHandler.SaveConfig(ref config, false);
            statistics?.SaveToFile();

            ChangePACButtonStatus(config.sysProxyType);

            tsbReload.Enabled = true;
        }

        /// <summary>
        /// 关闭V2ray
        /// </summary>
        private void CloseV2ray()
        {
            ConfigHandler.SaveConfig(ref config, false);
            statistics?.SaveToFile();

            ChangePACButtonStatus(ESysProxyType.ForcedClear);

            v2rayHandler.V2rayStop();
        }

        #endregion

        #region 功能按钮

        private void lvServers_Click(object sender, EventArgs e)
        {
            int index = -1;
            try
            {
                if (lvServers.SelectedIndices.Count > 0)
                {
                    index = lvServers.SelectedIndices[0];
                }
            }
            catch
            {
            }
            if (index < 0)
            {
                return;
            }
            qrCodeControl.showQRCode(lstVmess[index]);
        }

        private void lvServers_DoubleClick(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            ShowServerForm(lstVmess[index].configType, index);
        }
        private void ShowServerForm(EConfigType configType, int index)
        {
            BaseServerForm fm;
            if (configType == EConfigType.Custom)
            {
                fm = new AddServer2Form();
            }
            else
            {
                fm = new AddServerForm();
            }
            fm.vmessItem = index >= 0 ? lstVmess[index] : null;
            fm.groupId = groupId;
            fm.eConfigType = configType;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshServers();
                _ = LoadV2ray();
            }
        }


        private void lvServers_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A:
                        menuSelectAll_Click(null, null);
                        break;
                    case Keys.C:
                        menuExport2ShareUrl_Click(null, null);
                        break;
                    case Keys.V:
                        menuAddServers_Click(null, null);
                        break;
                    case Keys.P:
                        menuPingServer_Click(null, null);
                        break;
                    case Keys.O:
                        menuTcpingServer_Click(null, null);
                        break;
                    case Keys.R:
                        menuRealPingServer_Click(null, null);
                        break;
                    case Keys.S:
                        menuScanScreen_Click(null, null);
                        break;
                    case Keys.T:
                        menuSpeedServer_Click(null, null);
                        break;
                }
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Enter:
                        menuSetDefaultServer_Click(null, null);
                        break;
                    case Keys.Delete:
                        menuRemoveServer_Click(null, null);
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

        private void menuAddVmessServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.Vmess, -1);
        }

        private void menuAddVlessServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.VLESS, -1);
        }

        private void menuRemoveServer_Click(object sender, EventArgs e)
        {

            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (UI.ShowYesNo(UIRes.I18N("RemoveServer")) == DialogResult.No)
            {
                return;
            }

            ConfigHandler.RemoveServer(config, lstSelecteds);

            RefreshServers();
            _ = LoadV2ray();
        }

        private void menuRemoveDuplicateServer_Click(object sender, EventArgs e)
        {
            int oldCount = lstVmess.Count;
            int newCount = ConfigHandler.DedupServerList(ref config, ref lstVmess);
            RefreshServers();
            _ = LoadV2ray();
            UI.Show(string.Format(UIRes.I18N("RemoveDuplicateServerResult"), oldCount, newCount));
        }

        private void menuCopyServer_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (ConfigHandler.CopyServer(ref config, lstSelecteds) == 0)
            {
                RefreshServers();
            }
        }

        private void menuSetDefaultServer_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            SetDefaultServer(index);
        }


        private void menuPingServer_Click(object sender, EventArgs e)
        {
            Speedtest(ESpeedActionType.Ping);
        }
        private void menuTcpingServer_Click(object sender, EventArgs e)
        {
            Speedtest(ESpeedActionType.Tcping);
        }

        private void menuRealPingServer_Click(object sender, EventArgs e)
        {
            //if (!config.sysAgentEnabled)
            //{
            //    UI.Show(UIRes.I18N("NeedHttpGlobalProxy"));
            //    return;
            //}

            //UI.Show(UIRes.I18N("SpeedServerTips"));

            Speedtest(ESpeedActionType.Realping);
        }

        private void menuSpeedServer_Click(object sender, EventArgs e)
        {
            //if (!config.sysAgentEnabled)
            //{
            //    UI.Show(UIRes.I18N("NeedHttpGlobalProxy"));
            //    return;
            //}

            //UI.Show(UIRes.I18N("SpeedServerTips"));

            Speedtest(ESpeedActionType.Speedtest);
        }
        private void Speedtest(ESpeedActionType actionType)
        {
            if (GetLvSelectedIndex() < 0) return;
            ClearTestResult();
            SpeedtestHandler statistics = new SpeedtestHandler(ref config, v2rayHandler, lstSelecteds, actionType, UpdateSpeedtestHandler);
        }

        private void tsbTestMe_Click(object sender, EventArgs e)
        {
            SpeedtestHandler statistics = new SpeedtestHandler(ref config);
            string result = statistics.RunAvailabilityCheck() + "ms";
            AppendText(false, string.Format(UIRes.I18N("TestMeOutput"), result));
        }

        private void menuClearStatistic_Click(object sender, EventArgs e)
        {
            if (statistics != null)
            {
                statistics.ClearAllServerStatistics();
            }
        }

        private void menuExport2ClientConfig_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            MainFormHandler.Instance.Export2ClientConfig(lstVmess[index], config);
        }

        private void menuExport2ServerConfig_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            MainFormHandler.Instance.Export2ServerConfig(lstVmess[index], config);
        }

        private void menuExport2ShareUrl_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();

            StringBuilder sb = new StringBuilder();
            foreach (var it in lstSelecteds)
            {
                string url = ShareHandler.GetShareUrl(it);
                if (Utils.IsNullOrEmpty(url))
                {
                    continue;
                }
                sb.Append(url);
                sb.AppendLine();
            }
            if (sb.Length > 0)
            {
                Utils.SetClipboardData(sb.ToString());
                AppendText(false, UIRes.I18N("BatchExportURLSuccessfully"));
                //UI.Show(UIRes.I18N("BatchExportURLSuccessfully"));
            }
        }

        private void menuExport2SubContent_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();

            StringBuilder sb = new StringBuilder();
            foreach (var it in lstSelecteds)
            {
                string url = ShareHandler.GetShareUrl(it);
                if (Utils.IsNullOrEmpty(url))
                {
                    continue;
                }
                sb.Append(url);
                sb.AppendLine();
            }
            if (sb.Length > 0)
            {
                Utils.SetClipboardData(Utils.Base64Encode(sb.ToString()));
                UI.Show(UIRes.I18N("BatchExportSubscriptionSuccessfully"));
            }
        }

        private void tsbOptionSetting_Click(object sender, EventArgs e)
        {
            OptionSettingForm fm = new OptionSettingForm();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshServers();
                _ = LoadV2ray();
            }
        }

        private void tsbRoutingSetting_Click(object sender, EventArgs e)
        {
            var fm = new RoutingSettingForm();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshRoutingsMenu();
                RefreshServers();
                _ = LoadV2ray();
            }
        }

        private void tsbGlobalHotkeySetting_Click(object sender, EventArgs e)
        {
            var fm = new GlobalHotkeySettingForm();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshRoutingsMenu();
                RefreshServers();
                _ = LoadV2ray();
            }

        }

        private void tsbGroupSetting_Click(object sender, EventArgs e)
        {
            var fm = new GroupSettingForm();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                InitGroupView();
                RefreshServers();
            }

        }

        private void tsbReload_Click(object sender, EventArgs e)
        {
            Global.reloadV2ray = true;
            _ = LoadV2ray();
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
            StorageUI();
            HideForm();
            //this.WindowState = FormWindowState.Minimized;
        }

        /// <summary>
        /// 设置活动服务器
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private int SetDefaultServer(int index)
        {
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectServer"));
                return -1;
            }
            if (ConfigHandler.SetDefaultServer(ref config, lstVmess[index]) == 0)
            {
                RefreshServers();
                _ = LoadV2ray();
            }
            return 0;
        }

        /// <summary>
        /// 取得ListView选中的行
        /// </summary>
        /// <returns></returns>
        private int GetLvSelectedIndex()
        {
            int index = -1;
            lstSelecteds.Clear();
            try
            {
                if (lvServers.SelectedIndices.Count <= 0)
                {
                    UI.Show(UIRes.I18N("PleaseSelectServer"));
                    return index;
                }

                index = lvServers.SelectedIndices[0];
                foreach (int i in lvServers.SelectedIndices)
                {
                    lstSelecteds.Add(lstVmess[i]);
                }
                return index;
            }
            catch
            {
                return index;
            }
        }

        private void menuAddCustomServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.Custom, -1);
        }

        private void menuAddShadowsocksServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.Shadowsocks, -1);
            ShowForm();
        }

        private void menuAddSocksServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.Socks, -1);
            ShowForm();
        }

        private void menuAddTrojanServer_Click(object sender, EventArgs e)
        {
            ShowServerForm(EConfigType.Trojan, -1);
            ShowForm();
        }

        private void menuAddServers_Click(object sender, EventArgs e)
        {
            string clipboardData = Utils.GetClipboardData();
            int ret = ConfigHandler.AddBatchServers(ref config, clipboardData, "", groupId);
            if (ret > 0)
            {
                RefreshServers();
                UI.Show(string.Format(UIRes.I18N("SuccessfullyImportedServerViaClipboard"), ret));
            }
        }

        private void menuScanScreen_Click(object sender, EventArgs e)
        {
            _ = ScanScreenTaskAsync();
        }

        public async Task ScanScreenTaskAsync()
        {
            HideForm();

            string result = await Task.Run(() =>
            {
                return Utils.ScanScreen();
            });

            ShowForm();

            if (Utils.IsNullOrEmpty(result))
            {
                UI.ShowWarning(UIRes.I18N("NoValidQRcodeFound"));
            }
            else
            {
                int ret = ConfigHandler.AddBatchServers(ref config, result, "", groupId);
                if (ret > 0)
                {
                    RefreshServers();
                    UI.Show(UIRes.I18N("SuccessfullyImportedServerViaScan"));
                }
            }
        }

        private void menuUpdateSubscriptions_Click(object sender, EventArgs e)
        {
            UpdateSubscriptionProcess(false);
        }
        private void menuUpdateSubViaProxy_Click(object sender, EventArgs e)
        {
            UpdateSubscriptionProcess(true);
        }

        private void tsbBackupGuiNConfig_Click(object sender, EventArgs e)
        {
            MainFormHandler.Instance.BackupGuiNConfig(config);
        }
        #endregion


        #region 提示信息

        /// <summary>
        /// 消息委托
        /// </summary>
        /// <param name="notify"></param>
        /// <param name="msg"></param>
        void v2rayHandler_ProcessEvent(bool notify, string msg)
        {
            AppendText(notify, msg);
        }

        delegate void AppendTextDelegate(string text);
        void AppendText(bool notify, string msg)
        {
            try
            {
                AppendText(msg);
                if (notify)
                {
                    notifyMsg(msg);
                }
            }
            catch
            {
            }
        }

        void AppendText(string text)
        {
            if (this.txtMsgBox.InvokeRequired)
            {
                Invoke(new AppendTextDelegate(AppendText), new object[] { text });
            }
            else
            {
                if (!Utils.IsNullOrEmpty(MsgFilter))
                {
                    if (!Regex.IsMatch(text, MsgFilter))
                    {
                        return;
                    }
                }
                //this.txtMsgBox.AppendText(text);
                ShowMsg(text);
            }
        }

        /// <summary>
        /// 提示信息
        /// </summary>
        /// <param name="msg"></param>
        private void ShowMsg(string msg)
        {
            if (txtMsgBox.Lines.Length > 999)
            {
                ClearMsg();
            }
            this.txtMsgBox.AppendText(msg);
            if (!msg.EndsWith(Environment.NewLine))
            {
                this.txtMsgBox.AppendText(Environment.NewLine);
            }
        }

        /// <summary>
        /// 清除信息
        /// </summary>
        private void ClearMsg()
        {
            txtMsgBox.Invoke((Action)delegate
            {
                txtMsgBox.Clear();
            });
        }

        /// <summary>
        /// 托盘信息
        /// </summary>
        /// <param name="msg"></param>
        private void notifyMsg(string msg)
        {
            notifyMain.Text = msg;
        }

        #endregion


        #region 托盘事件

        private void notifyMain_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowForm();
            }
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            this.Visible = false;
            this.Close();

            Application.Exit();
        }


        private void ShowForm()
        {
            this.Show();
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.ShowInTaskbar = true;
            //this.notifyIcon1.Visible = false;
            this.txtMsgBox.ScrollToCaret();
            //if (config.index >= 0 && config.index < lvServers.Items.Count)
            //{
            //    lvServers.Items[config.index].Selected = true;
            //    lvServers.EnsureVisible(config.index); // workaround
            //}

            SetVisibleCore(true);
        }

        private void HideForm()
        {
            //this.WindowState = FormWindowState.Minimized;
            this.Hide();
            //this.notifyMain.Icon = this.Icon;
            this.notifyMain.Visible = true;
            this.ShowInTaskbar = false;

            SetVisibleCore(false);
        }

        #endregion

        #region 后台测速
        private void SetTestResult(string indexId, string txt)
        {
            int k = lstVmess.FindIndex(it => it.indexId == indexId);
            if (k >= 0 && k < lvServers.Items.Count)
            {
                lstVmess[k].testResult = txt;
                lvServers.Items[k].SubItems["testResult"].Text = txt;
            }
        }
        private void SetTestResult(int k, string txt)
        {
            if (k < lvServers.Items.Count)
            {
                lstVmess[k].testResult = txt;
                lvServers.Items[k].SubItems["testResult"].Text = txt;
            }
        }
        private void ClearTestResult()
        {
            foreach (var it in lstSelecteds)
            {
                SetTestResult(it.indexId, "");
            }
        }
        private void UpdateSpeedtestHandler(string indexId, string msg)
        {
            lvServers.Invoke((MethodInvoker)delegate
            {
                SetTestResult(indexId, msg);
            });
        }

        private void UpdateStatisticsHandler(ulong up, ulong down, List<ServerStatItem> statistics)
        {
            try
            {
                up /= (ulong)(config.statisticsFreshRate / 1000f);
                down /= (ulong)(config.statisticsFreshRate / 1000f);
                toolSslServerSpeed.Text = string.Format("{0}/s↑ | {1}/s↓", Utils.HumanFy(up), Utils.HumanFy(down));

                List<string[]> datas = new List<string[]>();
                for (int i = 0; i < lstVmess.Count; i++)
                {
                    int index = statistics.FindIndex(item_ => item_.itemId == lstVmess[i].indexId);
                    if (index != -1)
                    {
                        lvServers.Invoke((MethodInvoker)delegate
                        {
                            lvServers.BeginUpdate();

                            lvServers.Items[i].SubItems["todayDown"].Text = Utils.HumanFy(statistics[index].todayDown);
                            lvServers.Items[i].SubItems["todayUp"].Text = Utils.HumanFy(statistics[index].todayUp);
                            lvServers.Items[i].SubItems["totalDown"].Text = Utils.HumanFy(statistics[index].totalDown);
                            lvServers.Items[i].SubItems["totalUp"].Text = Utils.HumanFy(statistics[index].totalUp);

                            lvServers.EndUpdate();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void UpdateTaskHandler(bool success, string msg)
        {
            AppendText(false, msg);
            if (success)
            {
                Global.reloadV2ray = true;
                _ = LoadV2ray();
            }
        }
        #endregion

        #region 移动服务器

        private void menuMoveTop_Click(object sender, EventArgs e)
        {
            MoveServer(EMove.Top);
        }

        private void menuMoveUp_Click(object sender, EventArgs e)
        {
            MoveServer(EMove.Up);
        }

        private void menuMoveDown_Click(object sender, EventArgs e)
        {
            MoveServer(EMove.Down);
        }

        private void menuMoveBottom_Click(object sender, EventArgs e)
        {
            MoveServer(EMove.Bottom);
        }

        private void MoveServer(EMove eMove)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectServer"));
                return;
            }
            if (ConfigHandler.MoveServer(ref config, ref lstVmess, index, eMove) == 0)
            {
                //TODO: reload is not good.
                RefreshServers();
                //LoadV2ray();
            }
        }
        private void menuSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvServers.Items)
            {
                item.Selected = true;
            }
        }
        private void menuMoveToGroup_Click(object sender, EventArgs e)
        {
        }
        #endregion

        #region 系统代理相关
        private void menuKeepClear_Click(object sender, EventArgs e)
        {
            SetListenerType(ESysProxyType.ForcedClear);
        }
        private void menuGlobal_Click(object sender, EventArgs e)
        {
            SetListenerType(ESysProxyType.ForcedChange);
        }

        private void menuKeepNothing_Click(object sender, EventArgs e)
        {
            SetListenerType(ESysProxyType.Unchanged);
        }
        private void SetListenerType(ESysProxyType type)
        {
            config.sysProxyType = type;
            ChangePACButtonStatus(type);
        }

        private void ChangePACButtonStatus(ESysProxyType type)
        {
            SysProxyHandle.UpdateSysProxy(config, false);

            for (int k = 0; k < menuSysAgentMode.DropDownItems.Count; k++)
            {
                ToolStripMenuItem item = ((ToolStripMenuItem)menuSysAgentMode.DropDownItems[k]);
                item.Checked = ((int)type == k);
            }

            ConfigHandler.SaveConfig(ref config, false);
            DisplayToolStatus();
        }

        #endregion


        #region CheckUpdate

        private void tsbCheckUpdateN_Click(object sender, EventArgs e)
        {
            void _updateUI(bool success, string msg)
            {
                AppendText(false, msg);
                if (success)
                {
                    menuExit_Click(null, null);
                }
            };
            (new UpdateHandle()).CheckUpdateGuiN(config, _updateUI);
        }

        private void tsbCheckUpdateCore_Click(object sender, EventArgs e)
        {
            CheckUpdateCore("v2fly");
        }

        private void tsbCheckUpdateXrayCore_Click(object sender, EventArgs e)
        {
            CheckUpdateCore("xray");
        }

        private void CheckUpdateCore(string type)
        {
            void _updateUI(bool success, string msg)
            {
                AppendText(false, msg);
                if (success)
                {
                    CloseV2ray();

                    string fileName = Utils.GetPath(Utils.GetDownloadFileName(msg));
                    FileManager.ZipExtractToFile(fileName, config.ignoreGeoUpdateCore ? "geo" : "");

                    AppendText(false, UIRes.I18N("MsgUpdateV2rayCoreSuccessfullyMore"));

                    Global.reloadV2ray = true;
                    _ = LoadV2ray();

                    AppendText(false, UIRes.I18N("MsgUpdateV2rayCoreSuccessfully"));
                }
            };
            (new UpdateHandle()).CheckUpdateCore(type, config, _updateUI);
        }

        private void tsbCheckUpdateGeoSite_Click(object sender, EventArgs e)
        {
            (new UpdateHandle()).UpdateGeoFile("geosite", config, (bool success, string msg) =>
            {
                AppendText(false, msg);
                if (success)
                {
                    Global.reloadV2ray = true;
                    _ = LoadV2ray();
                }
            });
        }

        private void tsbCheckUpdateGeoIP_Click(object sender, EventArgs e)
        {
            (new UpdateHandle()).UpdateGeoFile("geoip", config, (bool success, string msg) =>
            {
                AppendText(false, msg);
                if (success)
                {
                    Global.reloadV2ray = true;
                    _ = LoadV2ray();
                }
            });
        }
        #endregion

        #region Help


        private void tsbAbout_Click(object sender, EventArgs e)
        {
            Process.Start(Global.AboutUrl);
        }

        private void tsbV2rayWebsite_Click(object sender, EventArgs e)
        {
            Process.Start(Global.v2rayWebsiteUrl);
        }

        private void tsbPromotion_Click(object sender, EventArgs e)
        {
            Process.Start($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
        }
        #endregion

        #region 订阅
        private void tsbSubSetting_Click(object sender, EventArgs e)
        {
            SubSettingForm fm = new SubSettingForm();
            if (fm.ShowDialog() == DialogResult.OK)
            {
                RefreshServers();
            }
        }

        private void tsbSubUpdate_Click(object sender, EventArgs e)
        {
            UpdateSubscriptionProcess(false);
        }

        private void tsbSubUpdateViaProxy_Click(object sender, EventArgs e)
        {
            UpdateSubscriptionProcess(true);
        }

        /// <summary>
        /// the subscription update process
        /// </summary>
        private void UpdateSubscriptionProcess(bool blProxy)
        {
            void _updateUI(bool success, string msg)
            {
                AppendText(false, msg);
                if (success)
                {
                    RefreshServers();
                    if (config.uiItem.enableAutoAdjustMainLvColWidth)
                    {
                        foreach (ColumnHeader it in lvServers.Columns)
                        {
                            it.Width = -2;
                        }
                    }
                }
            };

            (new UpdateHandle()).UpdateSubscriptionProcess(config, blProxy, _updateUI);
        }

        private void tsbQRCodeSwitch_CheckedChanged(object sender, EventArgs e)
        {
            bool bShow = tsbQRCodeSwitch.Checked;
            scMain.Panel2Collapsed = !bShow;
        }
        #endregion

        #region Language

        private void tsbLanguageDef_Click(object sender, EventArgs e)
        {
            SetCurrentLanguage("en");
        }

        private void tsbLanguageZhHans_Click(object sender, EventArgs e)
        {
            SetCurrentLanguage("zh-Hans");
        }
        private void SetCurrentLanguage(string value)
        {
            Utils.RegWriteValue(Global.MyRegPath, Global.MyRegKeyLanguage, value);
            //Application.Restart();
        }





        #endregion


        #region RoutingsMenu

        /// <summary>
        ///  
        /// </summary>
        private void RefreshRoutingsMenu()
        {
            menuRoutings.Visible = config.enableRoutingAdvanced;
            if (!config.enableRoutingAdvanced)
            {
                toolSslRoutingRule.Text = string.Empty;
                return;
            }

            menuRoutings.DropDownItems.Clear();

            List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
            for (int k = 0; k < config.routings.Count; k++)
            {
                var item = config.routings[k];
                if (item.locked == true)
                {
                    continue;
                }
                string name = item.remarks;

                ToolStripMenuItem ts = new ToolStripMenuItem(name)
                {
                    Tag = k
                };
                if (config.routingIndex.Equals(k))
                {
                    ts.Checked = true;
                    toolSslRoutingRule.Text = item.remarks;
                }
                ts.Click += new EventHandler(ts_Routing_Click);
                lst.Add(ts);
            }
            menuRoutings.DropDownItems.AddRange(lst.ToArray());
        }

        private void ts_Routing_Click(object sender, EventArgs e)
        {
            try
            {
                ToolStripItem ts = (ToolStripItem)sender;
                int index = Utils.ToInt(ts.Tag);

                if (ConfigHandler.SetDefaultRouting(ref config, index) == 0)
                {
                    RefreshRoutingsMenu();
                    _ = LoadV2ray();
                }
            }
            catch
            {
            }
        }
        #endregion

        #region MsgBoxMenu
        private void menuMsgBoxSelectAll_Click(object sender, EventArgs e)
        {
            this.txtMsgBox.Focus();
            this.txtMsgBox.SelectAll();
        }

        private void menuMsgBoxCopy_Click(object sender, EventArgs e)
        {
            var data = this.txtMsgBox.SelectedText.TrimEx();
            Utils.SetClipboardData(data);
        }

        private void menuMsgBoxCopyAll_Click(object sender, EventArgs e)
        {
            var data = this.txtMsgBox.Text;
            Utils.SetClipboardData(data);
        }
        private void menuMsgBoxClear_Click(object sender, EventArgs e)
        {
            this.txtMsgBox.Clear();
        }
        private void menuMsgBoxAddRoutingRule_Click(object sender, EventArgs e)
        {
            menuMsgBoxCopy_Click(null, null);
            tsbRoutingSetting_Click(null, null);
        }

        private void txtMsgBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A:
                        menuMsgBoxSelectAll_Click(null, null);
                        break;
                    case Keys.C:
                        menuMsgBoxCopy_Click(null, null);
                        break;
                    case Keys.V:
                        menuMsgBoxAddRoutingRule_Click(null, null);
                        break;

                }
            }

        }
        private void menuMsgBoxFilter_Click(object sender, EventArgs e)
        {
            var fm = new MsgFilterSetForm();
            fm.MsgFilter = MsgFilter;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                MsgFilter = fm.MsgFilter;
                gbMsgTitle.Text = string.Format(UIRes.I18N("MsgInformationTitle"), MsgFilter);
            }
        }
        #endregion

    }
}
