using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.HttpProxyHandler;
using v2rayN.Mode;
using v2rayN.Base;
using v2rayN.Tool;
using System.Diagnostics;
using v2rayN.Properties;
using Newtonsoft.Json;

namespace v2rayN.Forms
{
    public partial class MainForm : BaseForm
    {
        private V2rayHandler v2rayHandler;
        private List<int> lvSelecteds = new List<int>();
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
                v2rayHandler.V2rayStop();

                HttpProxyHandle.CloseHttpAgent(config);
                PACServerHandle.Stop();

                ConfigHandler.SaveConfig(ref config);
                statistics?.SaveToFile();
                statistics?.Close();
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
            lvServers.AutoResizeColumns();

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
            //    //this.splitContainer1.SplitterDistance = config.uiItem.mainQRCodeWidth;
            //}
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            //config.uiItem.mainQRCodeWidth = splitContainer1.SplitterDistance;
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

        #region 显示服务器 listview 和 menu

        /// <summary>
        /// 刷新服务器
        /// </summary>
        private void RefreshServers()
        {
            RefreshServersView();
            RefreshServersMenu();
        }

        /// <summary>
        /// 初始化服务器列表
        /// </summary>
        private void InitServersView()
        {
            lvServers.Items.Clear();

            lvServers.GridLines = true;
            lvServers.FullRowSelect = true;
            lvServers.View = View.Details;
            lvServers.Scrollable = true;
            lvServers.MultiSelect = true;
            lvServers.HeaderStyle = ColumnHeaderStyle.Nonclickable;

            lvServers.Columns.Add("", 30, HorizontalAlignment.Center);
            lvServers.Columns.Add(UIRes.I18N("LvServiceType"), 80, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvAlias"), 100, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvAddress"), 120, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvPort"), 50, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvEncryptionMethod"), 90, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvTransportProtocol"), 70, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvSubscription"), 50, HorizontalAlignment.Left);
            lvServers.Columns.Add(UIRes.I18N("LvTestResults"), 70, HorizontalAlignment.Left);

            if (statistics != null && statistics.Enable)
            {
                lvServers.Columns.Add(UIRes.I18N("LvTotalUploadDataAmount"), 70, HorizontalAlignment.Left);
                lvServers.Columns.Add(UIRes.I18N("LvTotalDownloadDataAmount"), 70, HorizontalAlignment.Left);
                lvServers.Columns.Add(UIRes.I18N("LvTodayUploadDataAmount"), 70, HorizontalAlignment.Left);
                lvServers.Columns.Add(UIRes.I18N("LvTodayDownloadDataAmount"), 70, HorizontalAlignment.Left);
            }
        }

        /// <summary>
        /// 刷新服务器列表
        /// </summary>
        private void RefreshServersView()
        {
            lvServers.Items.Clear();

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

                ListViewItem lvItem = null;
                if (statistics != null && statistics.Enable)
                {
                    var index = statistics.Statistic.FindIndex(item_ => item_.itemId == item.getItemId());
                    if (index != -1)
                    {
                        totalUp = Utils.HumanFy(statistics.Statistic[index].totalUp);
                        totalDown = Utils.HumanFy(statistics.Statistic[index].totalDown);
                        todayUp = Utils.HumanFy(statistics.Statistic[index].todayUp);
                        todayDown = Utils.HumanFy(statistics.Statistic[index].todayDown);
                    }

                    lvItem = new ListViewItem(new string[]
                    {
                    def,
                    ((EConfigType)item.configType).ToString(),
                    item.remarks,
                    item.address,
                    item.port.ToString(),
                    //item.id,
                    //item.alterId.ToString(),
                    item.security,
                    item.network,
                    item.getSubRemarks(config),
                    item.testResult,
                    totalUp,
                    totalDown,
                    todayUp,
                    todayDown
                    });
                }
                else
                {
                    lvItem = new ListViewItem(new string[]
                   {
                    def,
                    ((EConfigType)item.configType).ToString(),
                    item.remarks,
                    item.address,
                    item.port.ToString(),
                    //item.id,
                    //item.alterId.ToString(),
                    item.security,
                    item.network,
                    item.getSubRemarks(config),
                    item.testResult
                    //totalUp,
                    //totalDown,
                    //todayUp,
                    //todayDown,
                   });
                }

                if (lvItem != null) lvServers.Items.Add(lvItem);
            }

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

                ToolStripMenuItem ts = new ToolStripMenuItem(name);
                ts.Tag = k;
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
            toolSslSocksPort.Text =
            toolSslHttpPort.Text =
            toolSslPacPort.Text = "NONE";

            toolSslSocksPort.Text = $"{Global.Loopback}:{config.inbound[0].localPort}";

            if (config.listenerType != 0)
            {
                toolSslHttpPort.Text = $"{Global.Loopback}:{Global.httpPort}";
                if (config.listenerType == 2 || config.listenerType == 4)
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

            notifyMain.Icon = MainFormHandler.Instance.GetNotifyIcon(config, this.Icon);
        }
        private void ssMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!Utils.IsNullOrEmpty(e.ClickedItem.Text))
            {
                Utils.SetClipboardData(e.ClickedItem.Text);
            }
        }

        #endregion

        #region v2ray 操作

        /// <summary>
        /// 载入V2ray
        /// </summary>
        private void LoadV2ray()
        {
            if (Global.reloadV2ray)
            {
                ClearMsg();
            }
            v2rayHandler.LoadV2ray(config);
            Global.reloadV2ray = false;
            ConfigHandler.SaveConfig(ref config, false);
            statistics?.SaveToFile();

            ChangePACButtonStatus(config.listenerType);
        }

        /// <summary>
        /// 关闭V2ray
        /// </summary>
        private void CloseV2ray()
        {
            ConfigHandler.SaveConfig(ref config, false);
            statistics?.SaveToFile();

            ChangePACButtonStatus(0);

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
            qrCodeControl.showQRCode(index, config);
        }

        private void lvServers_DoubleClick(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }

            if (config.vmess[index].configType == (int)EConfigType.Vmess)
            {
                var fm = new AddServerForm();
                fm.EditIndex = index;
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    //刷新
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else if (config.vmess[index].configType == (int)EConfigType.Shadowsocks)
            {
                var fm = new AddServer3Form();
                fm.EditIndex = index;
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else if (config.vmess[index].configType == (int)EConfigType.Socks)
            {
                var fm = new AddServer4Form();
                fm.EditIndex = index;
                if (fm.ShowDialog() == DialogResult.OK)
                {
                    RefreshServers();
                    LoadV2ray();
                }
            }
            else
            {
                var fm2 = new AddServer2Form();
                fm2.EditIndex = index;
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
                    case Keys.P:
                        menuPingServer_Click(null, null);
                        break;
                    case Keys.O:
                        menuTcpingServer_Click(null, null);
                        break;
                    case Keys.R:
                        menuRealPingServer_Click(null, null);
                        break;
                    case Keys.T:
                        menuSpeedServer_Click(null, null);
                        break;
                }
            }
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    menuSetDefaultServer_Click(null, null);
                    break;
                case Keys.Delete:
                    menuRemoveServer_Click(null, null);
                    break;
                case Keys.U:
                    menuMoveUp_Click(null, null);
                    break;
                case Keys.D:
                    menuMoveDown_Click(null, null);
                    break;
            }
        }

        private void menuAddVmessServer_Click(object sender, EventArgs e)
        {
            AddServerForm fm = new AddServerForm();
            fm.EditIndex = -1;
            if (fm.ShowDialog() == DialogResult.OK)
            {
                //刷新
                RefreshServers();
                LoadV2ray();
            }
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
            List<Mode.VmessItem> servers = null;
            Utils.DedupServerList(config.vmess, out servers);
            if (servers != null)
            {
                config.vmess = servers;
            }
            //刷新
            RefreshServers();
            LoadV2ray();
        }

        private void menuCopyServer_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (ConfigHandler.CopyServer(ref config, index) == 0)
            {
                //刷新
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
            GetLvSelectedIndex();
            ClearTestResult();
            var statistics = new SpeedtestHandler(ref config, ref v2rayHandler, lvSelecteds, actionType, UpdateSpeedtestHandler);
        }

        private void menuExport2ClientConfig_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            MainFormHandler.Instance.Export2ClientConfig(index, config);
        }

        private void menuExport2ServerConfig_Click(object sender, EventArgs e)
        {
            int index = GetLvSelectedIndex();
            MainFormHandler.Instance.Export2ServerConfig(index, config);
        }

        private void menuExport2ShareUrl_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();

            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < lvSelecteds.Count; k++)
            {
                string url = ConfigHandler.GetVmessQRCode(config, lvSelecteds[k]);
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
                UI.Show(UIRes.I18N("BatchExportURLSuccessfully"));
            }
        }

        private void menuExport2SubContent_Click(object sender, EventArgs e)
        {
            GetLvSelectedIndex();

            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < lvSelecteds.Count; k++)
            {
                string url = ConfigHandler.GetVmessQRCode(config, lvSelecteds[k]);
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
                //刷新
                RefreshServers();
                LoadV2ray();
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
            lvSelecteds.Clear();
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
                    lvSelecteds.Add(i);
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
            UI.Show(UIRes.I18N("CustomServerTips"));

            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = false;
            fileDialog.Filter = "Config|*.json|All|*.*";
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
                UI.Show(UIRes.I18N("FailedImportedCustomServer"));
            }
        }

        private void menuAddShadowsocksServer_Click(object sender, EventArgs e)
        {
            var fm = new AddServer3Form();
            fm.EditIndex = -1;
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
            var fm = new AddServer4Form();
            fm.EditIndex = -1;
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
            if (AddBatchServers(clipboardData) == 0)
            {
                UI.Show(UIRes.I18N("SuccessfullyImportedServerViaClipboard"));
            }
        }

        private void menuScanScreen_Click(object sender, EventArgs e)
        {
            HideForm();
            bgwScan.RunWorkerAsync();
        }

        private int AddBatchServers(string clipboardData, string subid = "")
        {
            if (ConfigHandler.AddBatchServers(ref config, clipboardData, subid) != 0)
            {
                clipboardData = Utils.Base64Decode(clipboardData);
                if (ConfigHandler.AddBatchServers(ref config, clipboardData, subid) != 0)
                {
                    return -1;
                }
            }
            RefreshServers();
            return 0;
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
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
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
            //this.notifyIcon1.Visible = false;
            this.ShowInTaskbar = true;
            this.txtMsgBox.ScrollToCaret();

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

        private void SetTestResult(int k, string txt)
        {
            config.vmess[k].testResult = txt;
            lvServers.Items[k].SubItems[8].Text = txt;
        }
        private void ClearTestResult()
        {
            for (int k = 0; k < lvSelecteds.Count; k++)
            {
                SetTestResult(lvSelecteds[k], "");
            }
        }
        private void UpdateSpeedtestHandler(int index, string msg)
        {
            lvServers.Invoke((MethodInvoker)delegate
            {
                lvServers.SuspendLayout();

                SetTestResult(index, msg);

                lvServers.ResumeLayout();
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
                for (int i = 0; i < config.vmess.Count; i++)
                {
                    var index = statistics.FindIndex(item_ => item_.itemId == config.vmess[i].getItemId());
                    if (index != -1)
                    {
                        lvServers.Invoke((MethodInvoker)delegate
                        {
                            lvServers.SuspendLayout();

                            var indexStart = 9;
                            lvServers.Items[i].SubItems[indexStart++].Text = Utils.HumanFy(statistics[index].totalUp);
                            lvServers.Items[i].SubItems[indexStart++].Text = Utils.HumanFy(statistics[index].totalDown);
                            lvServers.Items[i].SubItems[indexStart++].Text = Utils.HumanFy(statistics[index].todayUp);
                            lvServers.Items[i].SubItems[indexStart++].Text = Utils.HumanFy(statistics[index].todayDown);

                            lvServers.ResumeLayout();
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
            int index = GetLvSelectedIndex();
            if (index < 0)
            {
                UI.Show(UIRes.I18N("PleaseSelectServer"));
                return;
            }
            if (ConfigHandler.MoveServer(ref config, index, eMove) == 0)
            {
                //刷新
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
            SetListenerType(0);
        }

        private void menuGlobal_Click(object sender, EventArgs e)
        {
            SetListenerType(1);
        }

        private void menuGlobalPAC_Click(object sender, EventArgs e)
        {
            SetListenerType(2);
        }

        private void menuKeep_Click(object sender, EventArgs e)
        {
            SetListenerType(3);
        }

        private void menuKeepPAC_Click(object sender, EventArgs e)
        {
            SetListenerType(4);
        }

        private void SetListenerType(int type)
        {
            config.listenerType = type;
            ChangePACButtonStatus(type);
        }

        private void ChangePACButtonStatus(int type)
        {
            if (type != 0)
            {
                HttpProxyHandle.RestartHttpAgent(config, false);
            }
            else
            {
                HttpProxyHandle.CloseHttpAgent(config);
            }

            for (int k = 0; k < menuSysAgentMode.DropDownItems.Count; k++)
            {
                var item = ((ToolStripMenuItem)menuSysAgentMode.DropDownItems[k]);
                item.Checked = (type == k);
            }

            ConfigHandler.SaveConfig(ref config, false);
            DisplayToolStatus();
        }

        #endregion


        #region CheckUpdate

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
                        AppendText(false, UIRes.I18N("MsgParsingV2rayCoreSuccessfully"));

                        string url = args.Msg;
                        this.Invoke((MethodInvoker)(delegate
                        {

                            if (UI.ShowYesNo(string.Format(UIRes.I18N("DownloadYesNo"), url)) == DialogResult.No)
                            {
                                return;
                            }
                            else
                            {
                                downloadHandle.DownloadFileAsync(config, url, null, -1);
                            }
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
                            var fileName = Utils.GetPath(downloadHandle.DownloadFileName);
                            var process = Process.Start("v2rayUpgrade.exe", fileName);
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

            AppendText(false, UIRes.I18N("MsgStartUpdatingV2rayCore"));
            downloadHandle.AbsoluteV2rayN(config);
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
                        AppendText(false, UIRes.I18N("MsgParsingV2rayCoreSuccessfully"));

                        string url = args.Msg;
                        this.Invoke((MethodInvoker)(delegate
                        {

                            if (UI.ShowYesNo(string.Format(UIRes.I18N("DownloadYesNo"), url)) == DialogResult.No)
                            {
                                return;
                            }
                            else
                            {
                                downloadHandle.DownloadFileAsync(config, url, null, -1);
                            }
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
                            CloseV2ray();

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

            AppendText(false, UIRes.I18N("MsgStartUpdatingV2rayCore"));
            downloadHandle.AbsoluteV2rayCore(config);
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
                        var result = args.Msg;
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
            pacListHandle.WebDownloadString(config.urlGFWList);
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
            System.Diagnostics.Process.Start(Global.AboutUrl);
        }

        private void tsbPromotion_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
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
                UI.Show(UIRes.I18N("NoValidQRcodeFound"));
            }
            else
            {
                if (AddBatchServers(result) == 0)
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
                        var result = Utils.Base64Decode(args.Msg);
                        if (Utils.IsNullOrEmpty(result))
                        {
                            AppendText(false, $"{hashCode}{UIRes.I18N("MsgSubscriptionDecodingFailed")}");
                            return;
                        }

                        ConfigHandler.RemoveServerViaSubid(ref config, id);
                        AppendText(false, $"{hashCode}{UIRes.I18N("MsgClearSubscription")}");
                        RefreshServers();
                        if (AddBatchServers(result, id) == 0)
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
        }

        #endregion


    }
}
