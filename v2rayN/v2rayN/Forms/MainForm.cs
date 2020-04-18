using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.HttpProxyHandler;
using v2rayN.Mode;
using v2rayN.Base;
using v2rayN.Tool;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

namespace v2rayN.Forms
{
    public partial class MainForm : BaseForm
    {
        private V2rayHandler v2rayHandler;
        private List<int> lvSelecteds = new List<int>(); // 使用前需用 GetServerListSelectedConfigIndex 更新
        private StatisticsHandler statistics = null;

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
                Closes();
            };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ConfigHandler.LoadConfig(ref config);
            v2rayHandler = new V2rayHandler();
            v2rayHandler.ProcessEvent += v2rayHandler_ProcessEvent;

            if (config.enableStatistics)
            {
                statistics = new StatisticsHandler(config, UpdateStatisticsHandler);
            }

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += new EventHandler(SystemEvents_DisplaySettingsChanged);
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
            InitServersView();
            RefreshServers();
            RestoreUI();

            LoadV2ray();

            HideForm();

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideForm();
                return;
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


        //private const int WM_QUERYENDSESSION = 0x0011;
        //protected override void WndProc(ref Message m)
        //{
        //    switch (m.Msg)
        //    {
        //        case WM_QUERYENDSESSION:
        //            Utils.SaveLog("Windows shutdown UnsetProxy");

        //            ConfigHandler.ToJsonFile(config);
        //            statistics?.SaveToFile();
        //            ProxySetting.UnsetProxy();
        //            m.Result = (IntPtr)1;
        //            break;
        //        default:
        //            base.WndProc(ref m);
        //            break;
        //    }
        //}
        #endregion

        #region 窗口大小和列宽等取/存
        private void RestoreUI()
        {
            scMain.Panel2Collapsed = true;

            if (!config.uiItem.mainSize.IsEmpty)
            {
                this.Width = config.uiItem.mainSize.Width;
                this.Height = config.uiItem.mainSize.Height;
            }

            foreach (ColumnHeader c in lvServers.Columns)
            {
                var width = ConfigHandler.GetformMainLvColWidth(ref config, c.Name, c.Width);
                c.Width = width;
            }
        }

        // Deprecated.
        private void StorageUI()
        {
            config.uiItem.mainSize = new Size(this.Width, this.Height);

            foreach (ColumnHeader c in lvServers.Columns)
            {
                ConfigHandler.AddformMainLvColWidth(ref config, c.Name, c.Width);
            }
        }

        #endregion

        #region 显示服务器 listview 和 menu

        /// <summary>
        /// 刷新服务器
        /// </summary>
        private void RefreshServers()
        {
            RefreshServersView();
            //lvServers.AutoResizeColumns();
            RefreshServersMenu();
        }

        /// <summary>
        /// 初始化服务器列表
        /// </summary>
        private void InitServersView()
        {
            lvServers.ListViewItemSorter = new Sorter();

            lvServers.BeginUpdate();
            lvServers.Items.Clear();

            lvServers.Columns.Add(EServerColName.def.ToString(), "", 30);
            lvServers.Columns.Add(EServerColName.type.ToString(), UIRes.I18N("LvServiceType"), 80);
            lvServers.Columns.Add(EServerColName.remarks.ToString(), UIRes.I18N("LvAlias"), 100);
            lvServers.Columns.Add(EServerColName.address.ToString(), UIRes.I18N("LvAddress"), 120);
            lvServers.Columns.Add(EServerColName.port.ToString(), UIRes.I18N("LvPort"), 50);
            lvServers.Columns.Add(EServerColName.security.ToString(), UIRes.I18N("LvEncryptionMethod"), 90);
            lvServers.Columns.Add(EServerColName.network.ToString(), UIRes.I18N("LvTransportProtocol"), 70);
            lvServers.Columns.Add(EServerColName.subRemarks.ToString(), UIRes.I18N("LvSubscription"), 50);
            lvServers.Columns.Add(EServerColName.testResult.ToString(), UIRes.I18N("LvTestResults"), 70);

            lvServers.Columns[EServerColName.port.ToString()].Tag = Global.sortMode.Numeric.ToString();
            if (statistics != null && statistics.Enable)
            {
                lvServers.Columns.Add(EServerColName.todayDown.ToString(), UIRes.I18N("LvTodayDownloadDataAmount"), 70);
                lvServers.Columns.Add(EServerColName.todayUp.ToString(), UIRes.I18N("LvTodayUploadDataAmount"), 70);
                lvServers.Columns.Add(EServerColName.totalDown.ToString(), UIRes.I18N("LvTotalDownloadDataAmount"), 70);
                lvServers.Columns.Add(EServerColName.totalUp.ToString(), UIRes.I18N("LvTotalUploadDataAmount"), 70);

                lvServers.Columns[EServerColName.todayDown.ToString()].Tag = Global.sortMode.Numeric.ToString();
                lvServers.Columns[EServerColName.todayUp.ToString()].Tag = Global.sortMode.Numeric.ToString();
                lvServers.Columns[EServerColName.totalDown.ToString()].Tag = Global.sortMode.Numeric.ToString();
                lvServers.Columns[EServerColName.totalUp.ToString()].Tag = Global.sortMode.Numeric.ToString();
            }

            foreach (ColumnHeader c in lvServers.Columns)
            {
                if (config.uiItem.mainLvColLayout == null) break;
                int i = config.uiItem.mainLvColLayout.IndexOf(c.Name);
                if (i >= 0) c.DisplayIndex = i;
            }
            lvServers.EndUpdate();
        }

        /// <summary>
        /// 刷新服务器列表
        /// </summary>
        private void RefreshServersView()
        {
            lvServers.Items.Clear();

            List<ListViewItem> lst = new List<ListViewItem>();
            for (int k = 0; k < config.vmess.Count; k++)
            {
                string def = string.Empty;
                string totalUp = string.Empty,
                        totalDown = string.Empty,
                        todayUp = string.Empty,
                        todayDown = string.Empty;
                if (config.index.Equals(k))
                {
                    def = "√";
                }

                VmessItem item = config.vmess[k];

                void _addSubItem(ListViewItem i, string name, string text, object tag = null)
                {
                    var n = new ListViewItem.ListViewSubItem() { Text = text };
                    n.Name = name; // new don't accept it.
                    n.Tag = tag; // cell's data store.
                    i.SubItems.Add(n);
                }
                ListViewItem lvItem = new ListViewItem(def);
                lvItem.Tag = k; // the Tag of line is config's index.
                _addSubItem(lvItem, EServerColName.type.ToString(), ((EConfigType)item.configType).ToString());
                _addSubItem(lvItem, EServerColName.remarks.ToString(), item.remarks);
                _addSubItem(lvItem, EServerColName.address.ToString(), item.address);
                _addSubItem(lvItem, EServerColName.port.ToString(), item.port.ToString(), item.port);
                _addSubItem(lvItem, EServerColName.security.ToString(), item.security);
                _addSubItem(lvItem, EServerColName.network.ToString(), item.network);
                _addSubItem(lvItem, EServerColName.subRemarks.ToString(), item.getSubRemarks(config));
                _addSubItem(lvItem, EServerColName.testResult.ToString(), item.testResult);
                bool stats = statistics != null && statistics.Enable;
                if (stats)
                {
                    ServerStatItem sItem = statistics.Statistic.Find(item_ => item_.itemId == item.getItemId());
                    if (sItem != null)
                    {
                        totalUp = Utils.HumanFy(sItem.totalUp);
                        totalDown = Utils.HumanFy(sItem.totalDown);
                        todayUp = Utils.HumanFy(sItem.todayUp);
                        todayDown = Utils.HumanFy(sItem.todayDown);
                    }
                    _addSubItem(lvItem, EServerColName.todayDown.ToString(), todayDown, sItem?.todayDown);
                    _addSubItem(lvItem, EServerColName.todayUp.ToString(), todayUp, sItem?.todayUp);
                    _addSubItem(lvItem, EServerColName.totalDown.ToString(), totalDown, sItem?.totalDown);
                    _addSubItem(lvItem, EServerColName.totalUp.ToString(), totalUp, sItem?.totalUp);
                }

                if (config.interlaceColoring && k % 2 == 1) // 隔行着色
                {
                    lvItem.BackColor = SystemColors.Control;
                }
                if (config.index.Equals(k))
                {
                    //lvItem.Checked = true;
                    lvItem.ForeColor = SystemColors.MenuHighlight;
                    lvItem.Font = new Font(lvItem.Font, FontStyle.Bold);
                }

                if (lvItem != null) lst.Add(lvItem);
            }
            lvServers.Items.AddRange(lst.ToArray());

            //if (lvServers.Items.Count > 0)
            //{
            //    if (lvServers.Items.Count <= testConfigIndex)
            //    {
            //        testConfigIndex = lvServers.Items.Count - 1;
            //    }
            //    lvServers.Items[testConfigIndex].Selected = true;
            //    lvServers.Select();
            //}
        }

        /// <summary>
        /// 刷新托盘服务器菜单
        /// </summary>
        private void RefreshServersMenu()
        {
            menuServers.DropDownItems.Clear();

            List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
            for (int k = 0; k < config.vmess.Count; k++)
            {
                VmessItem item = config.vmess[k];
                string name = item.getSummary();

                ToolStripMenuItem ts = new ToolStripMenuItem(name)
                {
                    Tag = k
                };
                if (config.index.Equals(k))
                {
                    ts.Checked = true;
                }
                ts.Click += new EventHandler(ts_Click);
                lst.Add(ts);
            }
            menuServers.DropDownItems.AddRange(lst.ToArray());
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
            RefreshQRCodePanel();
        }

        private void RefreshQRCodePanel()
        {
            if (scMain.Panel2Collapsed) return; // saving cpu.

            int index = GetConfigIndexFromServerListSelected();
            if (index < 0) return;
            qrCodeControl.showQRCode(index, config);
        }
        private void RefreshTaryIcon()
        {
            notifyMain.Icon = MainFormHandler.Instance.GetNotifyIcon(config, this.Icon);
        }
        private void DisplayToolStatus()
        {
            ssMain.SuspendLayout();
            toolSslSocksPort.Text =
            toolSslHttpPort.Text =
            toolSslPacPort.Text = "OFF";

            toolSslSocksPort.Text = $"{Global.Loopback}:{config.inbound[0].localPort}";

            if (config.listenerType != (int)ListenerType.noHttpProxy)
            {
                toolSslHttpPort.Text = $"{Global.Loopback}:{Global.httpPort}";
                if (config.listenerType == ListenerType.GlobalPac ||
                    config.listenerType == ListenerType.PacOpenAndClear ||
                    config.listenerType == ListenerType.PacOpenOnly)
                {
                    if (PACServerHandle.IsRunning)
                    {
                        toolSslPacPort.Text = $"{HttpProxyHandle.GetPacUrl()}";
                    }
                    else
                    {
                        toolSslPacPort.Text = UIRes.I18N("StartPacFailed");
                    }
                }
            }

            string routingStatus = "";
            switch (config.routingMode)
            {
                case 0:
                    routingStatus = UIRes.I18N("RoutingModeGlobal");
                    break;
                case 1:
                    routingStatus = UIRes.I18N("RoutingModeBypassLAN");
                    break;
                case 2:
                    routingStatus = UIRes.I18N("RoutingModeBypassCN");
                    break;
                case 3:
                    routingStatus = UIRes.I18N("RoutingModeBypassLANCN");
                    break;
            }
            toolSslRouting.Text = routingStatus;
            ssMain.ResumeLayout();

            RefreshTaryIcon();
        }
        private void ssMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!Utils.IsNullOrEmpty(e.ClickedItem.Text))
            {
                Utils.SetClipboardData(e.ClickedItem.Text);
            }
        }

        #endregion

        public static Task autoLatencyRefreshTask;
        private void autoLatencyRefresh()
        {
            if (config.listenerType != ListenerType.noHttpProxy)
            {
                if (autoLatencyRefreshTask == null || autoLatencyRefreshTask.IsCompleted)
                {
                    autoLatencyRefreshTask = Task.Run(async delegate
                    {
                        await Task.Delay(2000);
                        this.Invoke((MethodInvoker)(delegate
                        {
                            toolSslServerLatencyRefresh();
                        }));
                    });
                }
            }
        }

        #region v2ray 操作
        /// <summary>
        /// 载入V2ray
        /// </summary>
        private async void LoadV2ray()
        {
            this.Invoke((MethodInvoker)(delegate
            {
                tsbReload.Enabled = false;

                if (Global.reloadV2ray)
                {
                    ClearMsg();
                }
            }));
            await v2rayHandler.LoadV2ray(config);
            Global.reloadV2ray = false;
            ChangePACButtonStatus(config.listenerType);
            //ConfigHandler.SaveConfigToFile(ref config, false); // ChangePACButtonStatus does it.
            statistics?.SaveToFile();

            this.Invoke((MethodInvoker)(delegate
            {
                tsbReload.Enabled = true;

                autoLatencyRefresh();
            }));
        }

        /// <summary>
        /// 关闭相关组件
        /// </summary>
        private void Closes()
        {
            List<Task> tasks = new List<Task>
            {
                Task.Run(() => ConfigHandler.SaveConfigToFile(ref config)),
                Task.Run(() => HttpProxyHandle.CloseHttpAgent(config)),
                Task.Run(() => v2rayHandler.V2rayStop()),
                Task.Run(() => PACServerHandle.Stop()),
                Task.Run(() =>
                {
                    statistics?.SaveToFile();
                    statistics?.Close();
                })
            };
            Task.WaitAll(tasks.ToArray());
        }

        #endregion

        #region 功能按钮

        private void lvServers_DoubleClick(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            if (index < 0) return;

            if (config.vmess[index].configType == (int)EConfigType.Vmess)
            {
                AddServerForm fm = new AddServerForm
                {
                    EditIndex = index
                };
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    //刷新
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else if (config.vmess[index].configType == (int)EConfigType.Shadowsocks)
            {
                AddServer3Form fm = new AddServer3Form
                {
                    EditIndex = index
                };
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else if (config.vmess[index].configType == (int)EConfigType.Socks)
            {
                AddServer4Form fm = new AddServer4Form
                {
                    EditIndex = index
                };
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else
            {
                AddServer2Form fm2 = new AddServer2Form
                {
                    EditIndex = index
                };
                if (fm2.ShowDialog() == DialogResult.OK)
                {
                    //刷新
                    RefreshServers();
                    LoadV2ray();
                }
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
            AddServerForm fm = new AddServerForm
            {
                EditIndex = -1
            };
            if (fm.ShowDialog() == DialogResult.OK)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
            }
        }

        private void menuRemoveServer_Click(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            if (index < 0) return;

            if (UI.ShowYesNo(UIRes.I18N("RemoveServer")) == DialogResult.No)
            {
                return;
            }
            for (int k = lvSelecteds.Count - 1; k >= 0; k--)
            {
                ConfigHandler.RemoveServer(ref config, lvSelecteds[k]);
            }
            //刷新
            RefreshServers();
            LoadV2ray();

        }

        private void menuRemoveDuplicateServer_Click(object sender, EventArgs e)
        {
            Utils.DedupServerList(config.vmess, out List<VmessItem> servers, config.keepOlderDedupl);
            int oldCount = config.vmess.Count;
            int newCount = servers.Count;
            if (servers != null)
            {
                config.vmess = servers;
            }
            //刷新
            RefreshServers();
            LoadV2ray();
            UI.Show(string.Format(UIRes.I18N("RemoveDuplicateServerResult"), oldCount, newCount));
        }

        private void menuCopyServer_Click(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            if (index < 0) return;

            if (ConfigHandler.CopyServer(ref config, index) == 0)
            {
                //刷新
                RefreshServers();
            }
        }

        private void menuSetDefaultServer_Click(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            if (index < 0) return;

            SetDefaultServer(index);
        }


        private void menuPingServer_Click(object sender, EventArgs e)
        {
            Speedtest("ping");
        }
        private void menuTcpingServer_Click(object sender, EventArgs e)
        {
            Speedtest("tcping");
        }

        private void menuRealPingServer_Click(object sender, EventArgs e)
        {
            //if (!config.sysAgentEnabled)
            //{
            //    UI.Show(UIRes.I18N("NeedHttpGlobalProxy"));
            //    return;
            //}

            //UI.Show(UIRes.I18N("SpeedServerTips"));

            Speedtest("realping");
        }

        private void menuSpeedServer_Click(object sender, EventArgs e)
        {
            //if (!config.sysAgentEnabled)
            //{
            //    UI.Show(UIRes.I18N("NeedHttpGlobalProxy"));
            //    return;
            //}

            //UI.Show(UIRes.I18N("SpeedServerTips"));

            Speedtest("speedtest");
        }
        private void Speedtest(string actionType)
        {
            if (GetConfigIndexFromServerListSelected() < 0) return;
            ClearTestResult();
            SpeedtestHandler statistics = new SpeedtestHandler(ref config, ref v2rayHandler, lvSelecteds, actionType, UpdateSpeedtestHandler);
        }

        private async void menuTestMe_Click(object sender, EventArgs e)
        {
            string result = await httpProxyTest() + "ms";
            AppendText(false, string.Format(UIRes.I18N("TestMeOutput"), result));
        }
        private async Task<int> httpProxyTest()
        {
            SpeedtestHandler statistics = new SpeedtestHandler(ref config, ref v2rayHandler, lvSelecteds, "", UpdateSpeedtestHandler);
            return await Task.Run(() => statistics.RunAvailabilityCheck());
        }

        private void menuExport2ClientConfig_Click(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            MainFormHandler.Instance.Export2ClientConfig(index, config);
        }

        private void menuExport2ServerConfig_Click(object sender, EventArgs e)
        {
            int index = GetConfigIndexFromServerListSelected();
            MainFormHandler.Instance.Export2ServerConfig(index, config);
        }

        private void menuExport2ShareUrl_Click(object sender, EventArgs e)
        {
            GetConfigIndexFromServerListSelected();

            StringBuilder sb = new StringBuilder();
            foreach (int v in lvSelecteds)
            {
                string url = ConfigHandler.GetVmessQRCode(config, v);
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
            GetConfigIndexFromServerListSelected();

            StringBuilder sb = new StringBuilder();
            foreach (int v in lvSelecteds)
            {
                string url = ConfigHandler.GetVmessQRCode(config, v);
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
            string tab = "";
            if (sender == toolSslRouting) tab = "tabPreDefinedRules";

            OptionSettingForm fm = new OptionSettingForm(tab);
            if (fm.ShowDialog() == DialogResult.OK)
            {
                //刷新
                RefreshServers();
                //Application.DoEvents();
                Task.Run(() =>
                {
                    LoadV2ray();
                    HttpProxyHandle.RestartHttpAgent(config, true);
                });
            }
        }

        private void tsbReload_Click(object sender, EventArgs e)
        {
            Global.reloadV2ray = true;
            LoadV2ray();
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
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
            if (ConfigHandler.SetDefaultServer(ref config, index) == 0)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
                toolSslServerLatencySet();
            }
            return 0;
        }

        /// <summary>
        /// 获取服务器列表选中行的配置项（config）索引（index）
        /// 
        /// 返回值对应首个选中项，无选中或出错时返回-1
        /// 访问多选请在调用此函数后访问 lvSelecteds
        /// </summary>
        /// <returns></returns>
        private int GetConfigIndexFromServerListSelected()
        {
            int index = -1;
            lvSelecteds.Clear();
            try
            {
                if (lvServers.SelectedIndices.Count <= 0)
                {
                    UI.Show(UIRes.I18N("PleaseSelectServer"));
                    return index;
                }

                index = Convert.ToInt32(lvServers.SelectedItems[0].Tag);
                
                foreach (int item in lvServers.SelectedIndices)
                {
                    int i = Convert.ToInt32(lvServers.Items[item].Tag);
                    lvSelecteds.Add(i);
                }
                return index;
            }
            catch
            {
                return index;
            }
        }
        /// <summary>
        /// 获取服务器列表表示指定配置项的所有行的索引
        /// 基于列表项的Tag属性
        /// 
        /// 出错时返回空。
        /// </summary>
        /// <returns></returns>
        private List<int> GetServerListItemsByConfigIndex(int configIndex)
        {
            var l = new List<int>();
            foreach (ListViewItem item in lvServers.Items)
            {
                string tagStr = item.Tag?.ToString();
                if (int.TryParse(tagStr, out int tagInt)
                    && tagInt == configIndex)
                {
                    l.Add(item.Index);
                }
            }
            return l;
        }

        private void menuAddCustomServer_Click(object sender, EventArgs e)
        {
            UI.Show(UIRes.I18N("CustomServerTips"));

            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Config|*.json|All|*.*"
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

            if (ConfigHandler.AddCustomServer(ref config, fileName) == 0)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
                UI.Show(UIRes.I18N("SuccessfullyImportedCustomServer"));
            }
            else
            {
                UI.ShowWarning(UIRes.I18N("FailedImportedCustomServer"));
            }
        }

        private void menuAddShadowsocksServer_Click(object sender, EventArgs e)
        {
            AddServer3Form fm = new AddServer3Form
            {
                EditIndex = -1
            };
            if (fm.ShowDialog() == DialogResult.OK)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
            }
            ShowForm();
        }

        private void menuAddSocksServer_Click(object sender, EventArgs e)
        {
            AddServer4Form fm = new AddServer4Form
            {
                EditIndex = -1
            };
            if (fm.ShowDialog() == DialogResult.OK)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
            }
            ShowForm();
        }

        private void menuAddServers_Click(object sender, EventArgs e)
        {
            string clipboardData = Utils.GetClipboardData();
            int result = AddBatchServers(clipboardData);
            if (result > 0)
            {
                UI.Show(string.Format(UIRes.I18N("SuccessfullyImportedServerViaClipboard"), result));
            }
        }

        private void menuScanScreen_Click(object sender, EventArgs e)
        {
            HideForm();
            bgwScan.RunWorkerAsync();
        }

        private int AddBatchServers(string clipboardData, string subid = "")
        {
            int counter;
            int _Add()
            {
                return ConfigHandler.AddBatchServers(ref config, clipboardData, subid);
            }
            counter = _Add();
            if (counter < 1)
            {
                clipboardData = Utils.Base64Decode(clipboardData);
                counter = _Add();
            }
            RefreshServers();
            return counter;
        }

        private void menuUpdateSubscriptions_Click(object sender, EventArgs e)
        {
            UpdateSubscriptionProcess();
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
            this.txtMsgBox.Clear();
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
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.ShowInTaskbar = true;
            //this.notifyIcon1.Visible = false;
            this.txtMsgBox.ScrollToCaret();
            if (config.index >= 0 && config.index < lvServers.Items.Count)
            {
                lvServers.EnsureVisible(config.index); // workaround
            }

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

        private void SetTestResult(int configIndex, string txt)
        {
            var l = GetServerListItemsByConfigIndex(configIndex);
            if (l.Count > 0)
            {
                config.vmess[configIndex].testResult = txt;
                l.ForEach((listIndex) => lvServers.Items[listIndex].SubItems["testResult"].Text = txt);
            }
        }
        private void ClearTestResult()
        {
            foreach (int s in lvSelecteds)
            {
                SetTestResult(s, "");
            }
        }
        private void UpdateSpeedtestHandler(int index, string msg)
        {
            lvServers.Invoke((MethodInvoker)delegate
            {
                SetTestResult(index, msg);
            });
        }

        private void UpdateStatisticsHandler(ulong up, ulong down, List<ServerStatItem> statistics)
        {
            try
            {
                up /= (ulong)(config.statisticsFreshRate / 1000f);
                down /= (ulong)(config.statisticsFreshRate / 1000f);
                this.Invoke((MethodInvoker)(delegate
                {
                    toolSslServerSpeed.Text = string.Format("{0}/s↑ | {1}/s↓", Utils.HumanFy(up), Utils.HumanFy(down));
                }));
                List<string[]> datas = new List<string[]>();
                for (int i = 0; i < config.vmess.Count; i++)
                {
                    int index = statistics.FindIndex(item_ => item_.itemId == config.vmess[i].getItemId());
                    if (index != -1)
                    {
                        if (lvServers == null) return; // The app is exiting.
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
            int index = GetConfigIndexFromServerListSelected();
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectServer"));
                return;
            }
            if (ConfigHandler.MoveServer(ref config, index, eMove) == 0)
            {
                //TODO: reload is not good.
                RefreshServers();
                LoadV2ray();
            }
        }
        private void menuSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvServers.Items)
            {
                item.Selected = true;
            }
        }

        #endregion

        #region 系统代理相关

        private void menuCopyPACUrl_Click(object sender, EventArgs e)
        {
            Utils.SetClipboardData(HttpProxyHandle.GetPacUrl());
        }

        private void menuNotEnabledHttp_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.noHttpProxy);
        }
        private void menuGlobal_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.GlobalHttp);
        }
        private void menuGlobalPAC_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.GlobalPac);
        }
        private void menuKeep_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.HttpOpenAndClear);
        }
        private void menuKeepPAC_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.PacOpenAndClear);
        }
        private void menuKeepNothing_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.HttpOpenOnly);
        }
        private void menuKeepPACNothing_Click(object sender, EventArgs e)
        {
            SetListenerType(ListenerType.PacOpenOnly);
        }
        private void SetListenerType(ListenerType type)
        {
            config.listenerType = type;
            ChangePACButtonStatus(type);
        }

        private void ChangePACButtonStatus(ListenerType type)
        {
            if (type != ListenerType.noHttpProxy)
            {
                HttpProxyHandle.RestartHttpAgent(config, false);
            }
            else
            {
                HttpProxyHandle.CloseHttpAgent(config);
            }

            for (int k = 0; k < menuSysAgentMode.DropDownItems.Count; k++)
            {
                ToolStripMenuItem item = ((ToolStripMenuItem)menuSysAgentMode.DropDownItems[k]);
                item.Checked = ((int)type == k);
            }

            Global.reloadV2ray = false;
            ConfigHandler.SaveConfigToFile(ref config);

            this.Invoke((MethodInvoker)(delegate
            {
                DisplayToolStatus();
            }));
        }

        #endregion


        #region CheckUpdate

        private async void askToDownload(DownloadHandle downloadHandle, string url)
        {
            if (UI.ShowYesNo(string.Format(UIRes.I18N("DownloadYesNo"), url)) == DialogResult.Yes)
            {
                if (await httpProxyTest() > 0)
                {
                    int httpPort = config.GetLocalPort(Global.InboundHttp);
                    WebProxy webProxy = new WebProxy(Global.Loopback, httpPort);
                    downloadHandle.DownloadFileAsync(url, webProxy, 60);
                }
                else
                {
                    downloadHandle.DownloadFileAsync(url, null, 60);
                }
            }
        }
        private void tsbCheckUpdateN_Click(object sender, EventArgs e)
        {
            //System.Diagnostics.Process.Start(Global.UpdateUrl);
            DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();
                downloadHandle.AbsoluteCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        AppendText(false, string.Format(UIRes.I18N("MsgParsingSuccessfully"), "v2rayN"));

                        string url = args.Msg;
                        this.Invoke((MethodInvoker)(delegate
                        {
                            askToDownload(downloadHandle, url);
                        }));
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        AppendText(false, UIRes.I18N("MsgDownloadV2rayCoreSuccessfully"));

                        try
                        {
                            string fileName = Utils.GetPath(downloadHandle.DownloadFileName);
                            Process process = Process.Start("v2rayUpgrade.exe", "\"" + fileName + "\"");
                            if (process.Id > 0)
                            {
                                menuExit_Click(null, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            AppendText(false, ex.Message);
                        }
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    AppendText(true, args.GetException().Message);
                };
            }

            AppendText(false, string.Format(UIRes.I18N("MsgStartUpdating"), "v2rayN"));
            downloadHandle.CheckUpdateAsync("v2rayN");
        }

        private void tsbCheckUpdateCore_Click(object sender, EventArgs e)
        {
            DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();
                downloadHandle.AbsoluteCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        AppendText(false, string.Format(UIRes.I18N("MsgParsingSuccessfully"), "v2rayCore"));

                        string url = args.Msg;
                        this.Invoke((MethodInvoker)(delegate
                        {
                            askToDownload(downloadHandle, url);
                        }));
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        AppendText(false, UIRes.I18N("MsgDownloadV2rayCoreSuccessfully"));
                        AppendText(false, UIRes.I18N("MsgUnpacking"));

                        try
                        {
                            Closes();

                            string fileName = downloadHandle.DownloadFileName;
                            fileName = Utils.GetPath(fileName);
                            FileManager.ZipExtractToFile(fileName);

                            AppendText(false, UIRes.I18N("MsgUpdateV2rayCoreSuccessfullyMore"));

                            Global.reloadV2ray = true;
                            LoadV2ray();

                            AppendText(false, UIRes.I18N("MsgUpdateV2rayCoreSuccessfully"));
                        }
                        catch (Exception ex)
                        {
                            AppendText(false, ex.Message);
                        }
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    AppendText(true, args.GetException().Message);
                };
            }

            AppendText(false, string.Format(UIRes.I18N("MsgStartUpdating"), "v2rayCore"));
            downloadHandle.CheckUpdateAsync("Core");
        }

        private void tsbCheckUpdatePACList_Click(object sender, EventArgs e)
        {
            DownloadHandle pacListHandle = null;
            if (pacListHandle == null)
            {
                pacListHandle = new DownloadHandle();
                pacListHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        string result = args.Msg;
                        if (Utils.IsNullOrEmpty(result))
                        {
                            return;
                        }
                        pacListHandle.GenPacFile(result);

                        AppendText(false, UIRes.I18N("MsgPACUpdateSuccessfully"));
                    }
                    else
                    {
                        AppendText(false, UIRes.I18N("MsgPACUpdateFailed"));
                    }
                };
                pacListHandle.Error += (sender2, args) =>
                {
                    AppendText(true, args.GetException().Message);
                };
            }
            AppendText(false, UIRes.I18N("MsgStartUpdatingPAC"));
            string url = Utils.IsNullOrEmpty(config.urlGFWList) ? Global.GFWLIST_URL : config.urlGFWList;
            pacListHandle.WebDownloadString(url);
        }

        private void tsbCheckClearPACList_Click(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllText(Utils.GetPath(Global.pacFILE), Utils.GetEmbedText(Global.BlankPacFileName), Encoding.UTF8);
                AppendText(false, UIRes.I18N("MsgSimplifyPAC"));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
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

        #region ScanScreen


        private void bgwScan_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            string ret = Utils.ScanScreen();
            bgwScan.ReportProgress(0, ret);
        }

        private void bgwScan_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            ShowForm();

            string result = Convert.ToString(e.UserState);
            if (Utils.IsNullOrEmpty(result))
            {
                UI.ShowWarning(UIRes.I18N("NoValidQRcodeFound"));
            }
            else
            {
                if (AddBatchServers(result) > 0)
                {
                    UI.Show(UIRes.I18N("SuccessfullyImportedServerViaScan"));
                }
            }
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
            UpdateSubscriptionProcess();
        }

        /// <summary>
        /// the subscription update process
        /// </summary>
        private void UpdateSubscriptionProcess()
        {
            AppendText(false, UIRes.I18N("MsgUpdateSubscriptionStart"));

            if (config.subItem == null || config.subItem.Count <= 0)
            {
                AppendText(false, UIRes.I18N("MsgNoValidSubscription"));
                return;
            }

            for (int k = 1; k <= config.subItem.Count; k++)
            {
                string id = config.subItem[k - 1].id.TrimEx();
                string url = config.subItem[k - 1].url.TrimEx();
                string hashCode = $"{k}->";
                if (config.subItem[k - 1].enabled == false)
                {
                    continue;
                }
                if (Utils.IsNullOrEmpty(id) || Utils.IsNullOrEmpty(url))
                {
                    AppendText(false, $"{hashCode}{UIRes.I18N("MsgNoValidSubscription")}");
                    continue;
                }

                DownloadHandle downloadHandle3 = new DownloadHandle();
                downloadHandle3.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        AppendText(false, $"{hashCode}{UIRes.I18N("MsgGetSubscriptionSuccessfully")}");
                        string result = Utils.Base64Decode(args.Msg);
                        if (Utils.IsNullOrEmpty(result))
                        {
                            AppendText(false, $"{hashCode}{UIRes.I18N("MsgSubscriptionDecodingFailed")}");
                            return;
                        }

                        ConfigHandler.RemoveServerViaSubid(ref config, id);
                        AppendText(false, $"{hashCode}{UIRes.I18N("MsgClearSubscription")}");
                        RefreshServers();
                        if (AddBatchServers(result, id) > 0)
                        {
                        }
                        else
                        {
                            AppendText(false, $"{hashCode}{UIRes.I18N("MsgFailedImportSubscription")}");
                        }
                        AppendText(false, $"{hashCode}{UIRes.I18N("MsgUpdateSubscriptionEnd")}");
                    }
                    else
                    {
                        AppendText(false, args.Msg);
                    }
                };
                downloadHandle3.Error += (sender2, args) =>
                {
                    AppendText(true, args.GetException().Message);
                };

                downloadHandle3.WebDownloadString(url);
                AppendText(false, $"{hashCode}{UIRes.I18N("MsgStartGettingSubscriptions")}");
            }
        }

        private void tsbQRCodeSwitch_CheckedChanged(object sender, EventArgs e)
        {
            bool bShow = tsbQRCodeSwitch.Checked;
            scMain.Panel2Collapsed = !bShow;
            RefreshQRCodePanel();
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
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            RefreshTaryIcon();
        }

        private async void toolSslServerLatencyRefresh()
        {
            toolSslServerLatencySet(UIRes.I18N("ServerLatencyChecking"));
            string result = await httpProxyTest() + "ms";
            toolSslServerLatencySet(result);
        }
        private void toolSslServerLatencySet(string text = "")
        {
            toolSslServerLatency.Text = string.Format(UIRes.I18N("toolSslServerLatency"), text);
        }
        private void toolSslServerLatency_Click(object sender, EventArgs e)
        {
            toolSslServerLatencyRefresh();
        }

        private void toolSslServerSpeed_Click(object sender, EventArgs e)
        {
            //toolSslServerLatencyRefresh();
        }

        private void toolSslRouting_Click(object sender, EventArgs e)
        {
            tsbOptionSetting_Click(toolSslRouting, null);
        }

        private int lastSortedColIndex = -1;
        private bool lastSortedColDirection = false;
        private void lvServers_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            Sorter s = (Sorter)lvServers.ListViewItemSorter;
            s.Column = e.Column;

            int doIntSort;
            bool isNum = lvServers.Columns[e.Column].Tag?.ToString() == Global.sortMode.Numeric.ToString();
            if (lastSortedColIndex < 0  // 首次
                || (lastSortedColIndex >= 0 && lastSortedColIndex != e.Column) // 排序了其他列
                || (lastSortedColIndex == e.Column && !lastSortedColDirection) // 已排序
                )
            {
                lastSortedColDirection = true;
                doIntSort = isNum ? 1 : -1; // 正序
            }
            else
            {
                lastSortedColDirection = false;
                doIntSort = isNum ? 2 : -2; // 倒序
            }
            lastSortedColIndex = e.Column;

            s.Sorting = doIntSort;

            lvServers.Sort();
            reInterlaceColoring();
        }
        private void reInterlaceColoring()
        {
            if (!config.interlaceColoring) return;
            for (int k = 0; k < lvServers.Items.Count; k++)
            {
                if (config.interlaceColoring && k % 2 == 1)
                    lvServers.Items[k].BackColor = SystemColors.Control;
                else
                    lvServers.Items[k].BackColor = lvServers.BackColor;
            }
        }

        private void lvServers_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            ColumnHeader c = lvServers.Columns[e.ColumnIndex];
            ConfigHandler.AddformMainLvColWidth(ref config, c.Name, c.Width);
            Task.Run(() => ConfigHandler.SaveConfigToFile(ref config));
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            config.uiItem.mainSize = new Size(this.Width, this.Height);
            Task.Run(() => ConfigHandler.SaveConfigToFile(ref config));
        }

        private async void lvServers_ColumnReordered(object sender, ColumnReorderedEventArgs e)
        {
            await Task.Delay(500);
            var names = (from col in lvServers.Columns.Cast<ColumnHeader>()
                         orderby col.DisplayIndex
                         select col.Name).ToList();
            config.uiItem.mainLvColLayout = names;
            _ = Task.Run(() => ConfigHandler.SaveConfigToFile(ref config));
        }
    }
}
