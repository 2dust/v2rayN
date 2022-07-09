namespace v2rayN.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.scServers = new System.Windows.Forms.SplitContainer();
            this.lvServers = new v2rayN.Base.ListViewFlickerFree();
            this.cmsLv = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuAddVmessServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddVlessServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddShadowsocksServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddSocksServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddTrojanServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddCustomServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddServers = new System.Windows.Forms.ToolStripMenuItem();
            this.menuScanScreen = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuRemoveServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRemoveDuplicateServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCopyServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSetDefaultServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuServerFilter = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.menuMoveToGroup = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveEvent = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveTop = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveUp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveDown = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveBottom = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            this.menuPingServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTcpingServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRealPingServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSpeedServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSortServerResult = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbTestMe = new System.Windows.Forms.ToolStripMenuItem();
            this.menuClearServerStatistics = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExport2ClientConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2ServerConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2ShareUrl = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2SubContent = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbServer = new System.Windows.Forms.ToolStripDropDownButton();
            this.tabGroup = new System.Windows.Forms.TabControl();
            this.qrCodeControl = new v2rayN.Forms.QRCodeControl();
            this.scBig = new System.Windows.Forms.SplitContainer();
            this.gbServers = new System.Windows.Forms.GroupBox();
            this.mainMsgControl = new v2rayN.Forms.MainMsgControl();
            this.notifyMain = new System.Windows.Forms.NotifyIcon(this.components);
            this.cmsMain = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuSysAgentMode = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeepClear = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGlobal = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeepNothing = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRoutings = new System.Windows.Forms.ToolStripMenuItem();
            this.menuServers = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.menuAddServers2 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuScanScreen2 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUpdateSubscriptions = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUpdateSubViaProxy = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tsMain = new System.Windows.Forms.ToolStrip();
            this.tsbQRCodeSwitch = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbSub = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbSubSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbSubUpdate = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbSubUpdateViaProxy = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbSetting = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbOptionSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbRoutingSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbGlobalHotkeySetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbGroupSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbBackupGuiNConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbReload = new System.Windows.Forms.ToolStripButton();
            this.tsbCheckUpdate = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbCheckUpdateN = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCheckUpdateCore = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCheckUpdateXrayCore = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator16 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbCheckUpdateClashCore = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCheckUpdateClashMetaCore = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbCheckUpdateGeo = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbHelp = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbV2rayWebsite = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbLanguageDef = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbLanguageZhHans = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbClose = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            ((System.ComponentModel.ISupportInitialize)(this.scServers)).BeginInit();
            this.scServers.Panel1.SuspendLayout();
            this.scServers.Panel2.SuspendLayout();
            this.scServers.SuspendLayout();
            this.cmsLv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scBig)).BeginInit();
            this.scBig.Panel1.SuspendLayout();
            this.scBig.Panel2.SuspendLayout();
            this.scBig.SuspendLayout();
            this.gbServers.SuspendLayout();
            this.cmsMain.SuspendLayout();
            this.tsMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // scServers
            // 
            resources.ApplyResources(this.scServers, "scServers");
            this.scServers.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.scServers.Name = "scServers";
            // 
            // scServers.Panel1
            // 
            this.scServers.Panel1.Controls.Add(this.lvServers);
            this.scServers.Panel1.Controls.Add(this.tabGroup);
            // 
            // scServers.Panel2
            // 
            this.scServers.Panel2.Controls.Add(this.qrCodeControl);
            this.scServers.TabStop = false;
            // 
            // lvServers
            // 
            this.lvServers.ContextMenuStrip = this.cmsLv;
            resources.ApplyResources(this.lvServers, "lvServers");
            this.lvServers.FullRowSelect = true;
            this.lvServers.GridLines = true;
            this.lvServers.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvServers.HideSelection = false;
            this.lvServers.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            ((System.Windows.Forms.ListViewItem)(resources.GetObject("lvServers.Items")))});
            this.lvServers.MultiSelect = false;
            this.lvServers.Name = "lvServers";
            this.lvServers.UseCompatibleStateImageBehavior = false;
            this.lvServers.View = System.Windows.Forms.View.Details;
            this.lvServers.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lvServers_ColumnClick);
            this.lvServers.SelectedIndexChanged += new System.EventHandler(this.lvServers_SelectedIndexChanged);
            this.lvServers.Click += new System.EventHandler(this.lvServers_Click);
            this.lvServers.DoubleClick += new System.EventHandler(this.lvServers_DoubleClick);
            this.lvServers.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvServers_KeyDown);
            // 
            // cmsLv
            // 
            this.cmsLv.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsLv.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAddVmessServer,
            this.menuAddVlessServer,
            this.menuAddShadowsocksServer,
            this.menuAddSocksServer,
            this.menuAddTrojanServer,
            this.menuAddCustomServer,
            this.menuAddServers,
            this.menuScanScreen,
            this.toolStripSeparator1,
            this.menuRemoveServer,
            this.menuRemoveDuplicateServer,
            this.menuCopyServer,
            this.menuSetDefaultServer,
            this.menuServerFilter,
            this.toolStripSeparator3,
            this.menuMoveToGroup,
            this.menuMoveEvent,
            this.menuSelectAll,
            this.toolStripSeparator9,
            this.menuPingServer,
            this.menuTcpingServer,
            this.menuRealPingServer,
            this.menuSpeedServer,
            this.menuSortServerResult,
            this.tsbTestMe,
            this.menuClearServerStatistics,
            this.toolStripSeparator6,
            this.menuExport2ClientConfig,
            this.menuExport2ServerConfig,
            this.menuExport2ShareUrl,
            this.menuExport2SubContent});
            this.cmsLv.Name = "cmsLv";
            resources.ApplyResources(this.cmsLv, "cmsLv");
            // 
            // menuAddVmessServer
            // 
            this.menuAddVmessServer.Name = "menuAddVmessServer";
            resources.ApplyResources(this.menuAddVmessServer, "menuAddVmessServer");
            this.menuAddVmessServer.Click += new System.EventHandler(this.menuAddVmessServer_Click);
            // 
            // menuAddVlessServer
            // 
            this.menuAddVlessServer.Name = "menuAddVlessServer";
            resources.ApplyResources(this.menuAddVlessServer, "menuAddVlessServer");
            this.menuAddVlessServer.Click += new System.EventHandler(this.menuAddVlessServer_Click);
            // 
            // menuAddShadowsocksServer
            // 
            this.menuAddShadowsocksServer.Name = "menuAddShadowsocksServer";
            resources.ApplyResources(this.menuAddShadowsocksServer, "menuAddShadowsocksServer");
            this.menuAddShadowsocksServer.Click += new System.EventHandler(this.menuAddShadowsocksServer_Click);
            // 
            // menuAddSocksServer
            // 
            this.menuAddSocksServer.Name = "menuAddSocksServer";
            resources.ApplyResources(this.menuAddSocksServer, "menuAddSocksServer");
            this.menuAddSocksServer.Click += new System.EventHandler(this.menuAddSocksServer_Click);
            // 
            // menuAddTrojanServer
            // 
            this.menuAddTrojanServer.Name = "menuAddTrojanServer";
            resources.ApplyResources(this.menuAddTrojanServer, "menuAddTrojanServer");
            this.menuAddTrojanServer.Click += new System.EventHandler(this.menuAddTrojanServer_Click);
            // 
            // menuAddCustomServer
            // 
            this.menuAddCustomServer.Name = "menuAddCustomServer";
            resources.ApplyResources(this.menuAddCustomServer, "menuAddCustomServer");
            this.menuAddCustomServer.Click += new System.EventHandler(this.menuAddCustomServer_Click);
            // 
            // menuAddServers
            // 
            this.menuAddServers.Name = "menuAddServers";
            resources.ApplyResources(this.menuAddServers, "menuAddServers");
            this.menuAddServers.Click += new System.EventHandler(this.menuAddServers_Click);
            // 
            // menuScanScreen
            // 
            this.menuScanScreen.Name = "menuScanScreen";
            resources.ApplyResources(this.menuScanScreen, "menuScanScreen");
            this.menuScanScreen.Click += new System.EventHandler(this.menuScanScreen_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // menuRemoveServer
            // 
            this.menuRemoveServer.Name = "menuRemoveServer";
            resources.ApplyResources(this.menuRemoveServer, "menuRemoveServer");
            this.menuRemoveServer.Click += new System.EventHandler(this.menuRemoveServer_Click);
            // 
            // menuRemoveDuplicateServer
            // 
            this.menuRemoveDuplicateServer.Name = "menuRemoveDuplicateServer";
            resources.ApplyResources(this.menuRemoveDuplicateServer, "menuRemoveDuplicateServer");
            this.menuRemoveDuplicateServer.Click += new System.EventHandler(this.menuRemoveDuplicateServer_Click);
            // 
            // menuCopyServer
            // 
            this.menuCopyServer.Name = "menuCopyServer";
            resources.ApplyResources(this.menuCopyServer, "menuCopyServer");
            this.menuCopyServer.Click += new System.EventHandler(this.menuCopyServer_Click);
            // 
            // menuSetDefaultServer
            // 
            this.menuSetDefaultServer.Name = "menuSetDefaultServer";
            resources.ApplyResources(this.menuSetDefaultServer, "menuSetDefaultServer");
            this.menuSetDefaultServer.Click += new System.EventHandler(this.menuSetDefaultServer_Click);
            // 
            // menuServerFilter
            // 
            this.menuServerFilter.Name = "menuServerFilter";
            resources.ApplyResources(this.menuServerFilter, "menuServerFilter");
            this.menuServerFilter.Click += new System.EventHandler(this.menuServerFilter_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
            // 
            // menuMoveToGroup
            // 
            this.menuMoveToGroup.Name = "menuMoveToGroup";
            resources.ApplyResources(this.menuMoveToGroup, "menuMoveToGroup");
            this.menuMoveToGroup.Click += new System.EventHandler(this.menuMoveToGroup_Click);
            // 
            // menuMoveEvent
            // 
            this.menuMoveEvent.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuMoveTop,
            this.menuMoveUp,
            this.menuMoveDown,
            this.menuMoveBottom});
            this.menuMoveEvent.Name = "menuMoveEvent";
            resources.ApplyResources(this.menuMoveEvent, "menuMoveEvent");
            // 
            // menuMoveTop
            // 
            this.menuMoveTop.Name = "menuMoveTop";
            resources.ApplyResources(this.menuMoveTop, "menuMoveTop");
            this.menuMoveTop.Click += new System.EventHandler(this.menuMoveTop_Click);
            // 
            // menuMoveUp
            // 
            this.menuMoveUp.Name = "menuMoveUp";
            resources.ApplyResources(this.menuMoveUp, "menuMoveUp");
            this.menuMoveUp.Click += new System.EventHandler(this.menuMoveUp_Click);
            // 
            // menuMoveDown
            // 
            this.menuMoveDown.Name = "menuMoveDown";
            resources.ApplyResources(this.menuMoveDown, "menuMoveDown");
            this.menuMoveDown.Click += new System.EventHandler(this.menuMoveDown_Click);
            // 
            // menuMoveBottom
            // 
            this.menuMoveBottom.Name = "menuMoveBottom";
            resources.ApplyResources(this.menuMoveBottom, "menuMoveBottom");
            this.menuMoveBottom.Click += new System.EventHandler(this.menuMoveBottom_Click);
            // 
            // menuSelectAll
            // 
            this.menuSelectAll.Name = "menuSelectAll";
            resources.ApplyResources(this.menuSelectAll, "menuSelectAll");
            this.menuSelectAll.Click += new System.EventHandler(this.menuSelectAll_Click);
            // 
            // toolStripSeparator9
            // 
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            resources.ApplyResources(this.toolStripSeparator9, "toolStripSeparator9");
            // 
            // menuPingServer
            // 
            this.menuPingServer.Name = "menuPingServer";
            resources.ApplyResources(this.menuPingServer, "menuPingServer");
            this.menuPingServer.Click += new System.EventHandler(this.menuPingServer_Click);
            // 
            // menuTcpingServer
            // 
            this.menuTcpingServer.Name = "menuTcpingServer";
            resources.ApplyResources(this.menuTcpingServer, "menuTcpingServer");
            this.menuTcpingServer.Click += new System.EventHandler(this.menuTcpingServer_Click);
            // 
            // menuRealPingServer
            // 
            this.menuRealPingServer.Name = "menuRealPingServer";
            resources.ApplyResources(this.menuRealPingServer, "menuRealPingServer");
            this.menuRealPingServer.Click += new System.EventHandler(this.menuRealPingServer_Click);
            // 
            // menuSpeedServer
            // 
            this.menuSpeedServer.Name = "menuSpeedServer";
            resources.ApplyResources(this.menuSpeedServer, "menuSpeedServer");
            this.menuSpeedServer.Click += new System.EventHandler(this.menuSpeedServer_Click);
            // 
            // menuSortServerResult
            // 
            this.menuSortServerResult.Name = "menuSortServerResult";
            resources.ApplyResources(this.menuSortServerResult, "menuSortServerResult");
            this.menuSortServerResult.Click += new System.EventHandler(this.menuSortServerResult_Click);
            // 
            // tsbTestMe
            // 
            this.tsbTestMe.Name = "tsbTestMe";
            resources.ApplyResources(this.tsbTestMe, "tsbTestMe");
            this.tsbTestMe.Click += new System.EventHandler(this.tsbTestMe_Click);
            // 
            // menuClearServerStatistics
            // 
            this.menuClearServerStatistics.Name = "menuClearServerStatistics";
            resources.ApplyResources(this.menuClearServerStatistics, "menuClearServerStatistics");
            this.menuClearServerStatistics.Click += new System.EventHandler(this.menuClearStatistic_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            resources.ApplyResources(this.toolStripSeparator6, "toolStripSeparator6");
            // 
            // menuExport2ClientConfig
            // 
            this.menuExport2ClientConfig.Name = "menuExport2ClientConfig";
            resources.ApplyResources(this.menuExport2ClientConfig, "menuExport2ClientConfig");
            this.menuExport2ClientConfig.Click += new System.EventHandler(this.menuExport2ClientConfig_Click);
            // 
            // menuExport2ServerConfig
            // 
            this.menuExport2ServerConfig.Name = "menuExport2ServerConfig";
            resources.ApplyResources(this.menuExport2ServerConfig, "menuExport2ServerConfig");
            this.menuExport2ServerConfig.Click += new System.EventHandler(this.menuExport2ServerConfig_Click);
            // 
            // menuExport2ShareUrl
            // 
            this.menuExport2ShareUrl.Name = "menuExport2ShareUrl";
            resources.ApplyResources(this.menuExport2ShareUrl, "menuExport2ShareUrl");
            this.menuExport2ShareUrl.Click += new System.EventHandler(this.menuExport2ShareUrl_Click);
            // 
            // menuExport2SubContent
            // 
            this.menuExport2SubContent.Name = "menuExport2SubContent";
            resources.ApplyResources(this.menuExport2SubContent, "menuExport2SubContent");
            this.menuExport2SubContent.Click += new System.EventHandler(this.menuExport2SubContent_Click);
            // 
            // tsbServer
            // 
            this.tsbServer.DropDown = this.cmsLv;
            this.tsbServer.Image = global::v2rayN.Properties.Resources.server;
            resources.ApplyResources(this.tsbServer, "tsbServer");
            this.tsbServer.Name = "tsbServer";
            // 
            // tabGroup
            // 
            resources.ApplyResources(this.tabGroup, "tabGroup");
            this.tabGroup.Name = "tabGroup";
            this.tabGroup.SelectedIndex = 0;
            this.tabGroup.SelectedIndexChanged += new System.EventHandler(this.tabGroup_SelectedIndexChanged);
            // 
            // qrCodeControl
            // 
            resources.ApplyResources(this.qrCodeControl, "qrCodeControl");
            this.qrCodeControl.Name = "qrCodeControl";
            // 
            // scBig
            // 
            resources.ApplyResources(this.scBig, "scBig");
            this.scBig.Name = "scBig";
            // 
            // scBig.Panel1
            // 
            this.scBig.Panel1.Controls.Add(this.gbServers);
            // 
            // scBig.Panel2
            // 
            this.scBig.Panel2.Controls.Add(this.mainMsgControl);
            // 
            // gbServers
            // 
            this.gbServers.Controls.Add(this.scServers);
            resources.ApplyResources(this.gbServers, "gbServers");
            this.gbServers.Name = "gbServers";
            this.gbServers.TabStop = false;
            // 
            // mainMsgControl
            // 
            resources.ApplyResources(this.mainMsgControl, "mainMsgControl");
            this.mainMsgControl.Name = "mainMsgControl";
            // 
            // notifyMain
            // 
            this.notifyMain.ContextMenuStrip = this.cmsMain;
            resources.ApplyResources(this.notifyMain, "notifyMain");
            this.notifyMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyMain_MouseClick);
            // 
            // cmsMain
            // 
            this.cmsMain.ImageScalingSize = new System.Drawing.Size(20, 20);
            resources.ApplyResources(this.cmsMain, "cmsMain");
            this.cmsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuSysAgentMode,
            this.menuRoutings,
            this.menuServers,
            this.toolStripSeparator13,
            this.menuAddServers2,
            this.menuScanScreen2,
            this.menuUpdateSubscriptions,
            this.menuUpdateSubViaProxy,
            this.toolStripSeparator2,
            this.menuExit});
            this.cmsMain.Name = "contextMenuStrip1";
            this.cmsMain.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.cmsMain.ShowCheckMargin = true;
            this.cmsMain.ShowImageMargin = false;
            // 
            // menuSysAgentMode
            // 
            this.menuSysAgentMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuKeepClear,
            this.menuGlobal,
            this.menuKeepNothing});
            this.menuSysAgentMode.Name = "menuSysAgentMode";
            resources.ApplyResources(this.menuSysAgentMode, "menuSysAgentMode");
            // 
            // menuKeepClear
            // 
            this.menuKeepClear.Name = "menuKeepClear";
            resources.ApplyResources(this.menuKeepClear, "menuKeepClear");
            this.menuKeepClear.Click += new System.EventHandler(this.menuKeepClear_Click);
            // 
            // menuGlobal
            // 
            this.menuGlobal.Name = "menuGlobal";
            resources.ApplyResources(this.menuGlobal, "menuGlobal");
            this.menuGlobal.Click += new System.EventHandler(this.menuGlobal_Click);
            // 
            // menuKeepNothing
            // 
            this.menuKeepNothing.Name = "menuKeepNothing";
            resources.ApplyResources(this.menuKeepNothing, "menuKeepNothing");
            this.menuKeepNothing.Click += new System.EventHandler(this.menuKeepNothing_Click);
            // 
            // menuRoutings
            // 
            this.menuRoutings.Name = "menuRoutings";
            resources.ApplyResources(this.menuRoutings, "menuRoutings");
            // 
            // menuServers
            // 
            this.menuServers.Name = "menuServers";
            resources.ApplyResources(this.menuServers, "menuServers");
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            resources.ApplyResources(this.toolStripSeparator13, "toolStripSeparator13");
            // 
            // menuAddServers2
            // 
            this.menuAddServers2.Name = "menuAddServers2";
            resources.ApplyResources(this.menuAddServers2, "menuAddServers2");
            this.menuAddServers2.Click += new System.EventHandler(this.menuAddServers_Click);
            // 
            // menuScanScreen2
            // 
            this.menuScanScreen2.Name = "menuScanScreen2";
            resources.ApplyResources(this.menuScanScreen2, "menuScanScreen2");
            this.menuScanScreen2.Click += new System.EventHandler(this.menuScanScreen_Click);
            // 
            // menuUpdateSubscriptions
            // 
            this.menuUpdateSubscriptions.Name = "menuUpdateSubscriptions";
            resources.ApplyResources(this.menuUpdateSubscriptions, "menuUpdateSubscriptions");
            this.menuUpdateSubscriptions.Click += new System.EventHandler(this.menuUpdateSubscriptions_Click);
            // 
            // menuUpdateSubViaProxy
            // 
            this.menuUpdateSubViaProxy.Name = "menuUpdateSubViaProxy";
            resources.ApplyResources(this.menuUpdateSubViaProxy, "menuUpdateSubViaProxy");
            this.menuUpdateSubViaProxy.Click += new System.EventHandler(this.menuUpdateSubViaProxy_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            resources.ApplyResources(this.menuExit, "menuExit");
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // tsMain
            // 
            this.tsMain.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.tsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbServer,
            this.toolStripSeparator5,
            this.tsbSub,
            this.tsbQRCodeSwitch,
            this.toolStripSeparator4,
            this.tsbSetting,
            this.toolStripSeparator7,
            this.tsbReload,
            this.tsbCheckUpdate,
            this.tsbHelp,
            this.toolStripSeparator11,
            this.tsbClose});
            resources.ApplyResources(this.tsMain, "tsMain");
            this.tsMain.Name = "tsMain";
            this.tsMain.TabStop = true;
            // 
            // tsbQRCodeSwitch
            // 
            this.tsbQRCodeSwitch.CheckOnClick = true;
            this.tsbQRCodeSwitch.ForeColor = System.Drawing.Color.Black;
            this.tsbQRCodeSwitch.Image = global::v2rayN.Properties.Resources.share;
            resources.ApplyResources(this.tsbQRCodeSwitch, "tsbQRCodeSwitch");
            this.tsbQRCodeSwitch.Name = "tsbQRCodeSwitch";
            this.tsbQRCodeSwitch.CheckedChanged += new System.EventHandler(this.tsbQRCodeSwitch_CheckedChanged);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            resources.ApplyResources(this.toolStripSeparator5, "toolStripSeparator5");
            // 
            // tsbSub
            // 
            this.tsbSub.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbSubSetting,
            this.tsbSubUpdate,
            this.tsbSubUpdateViaProxy});
            this.tsbSub.Image = global::v2rayN.Properties.Resources.sub;
            resources.ApplyResources(this.tsbSub, "tsbSub");
            this.tsbSub.Name = "tsbSub";
            // 
            // tsbSubSetting
            // 
            this.tsbSubSetting.Name = "tsbSubSetting";
            resources.ApplyResources(this.tsbSubSetting, "tsbSubSetting");
            this.tsbSubSetting.Click += new System.EventHandler(this.tsbSubSetting_Click);
            // 
            // tsbSubUpdate
            // 
            this.tsbSubUpdate.Name = "tsbSubUpdate";
            resources.ApplyResources(this.tsbSubUpdate, "tsbSubUpdate");
            this.tsbSubUpdate.Click += new System.EventHandler(this.tsbSubUpdate_Click);
            // 
            // tsbSubUpdateViaProxy
            // 
            this.tsbSubUpdateViaProxy.Name = "tsbSubUpdateViaProxy";
            resources.ApplyResources(this.tsbSubUpdateViaProxy, "tsbSubUpdateViaProxy");
            this.tsbSubUpdateViaProxy.Click += new System.EventHandler(this.tsbSubUpdateViaProxy_Click);
            // 
            // tsbSetting
            // 
            this.tsbSetting.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbOptionSetting,
            this.tsbRoutingSetting,
            this.tsbGlobalHotkeySetting,
            this.tsbGroupSetting,
            this.toolStripSeparator14,
            this.tsbBackupGuiNConfig});
            this.tsbSetting.Image = global::v2rayN.Properties.Resources.option;
            resources.ApplyResources(this.tsbSetting, "tsbSetting");
            this.tsbSetting.Name = "tsbSetting";
            // 
            // tsbOptionSetting
            // 
            this.tsbOptionSetting.Name = "tsbOptionSetting";
            resources.ApplyResources(this.tsbOptionSetting, "tsbOptionSetting");
            this.tsbOptionSetting.Click += new System.EventHandler(this.tsbOptionSetting_Click);
            // 
            // tsbRoutingSetting
            // 
            this.tsbRoutingSetting.Name = "tsbRoutingSetting";
            resources.ApplyResources(this.tsbRoutingSetting, "tsbRoutingSetting");
            this.tsbRoutingSetting.Click += new System.EventHandler(this.tsbRoutingSetting_Click);
            // 
            // tsbGlobalHotkeySetting
            // 
            this.tsbGlobalHotkeySetting.Name = "tsbGlobalHotkeySetting";
            resources.ApplyResources(this.tsbGlobalHotkeySetting, "tsbGlobalHotkeySetting");
            this.tsbGlobalHotkeySetting.Click += new System.EventHandler(this.tsbGlobalHotkeySetting_Click);
            // 
            // tsbGroupSetting
            // 
            this.tsbGroupSetting.Name = "tsbGroupSetting";
            resources.ApplyResources(this.tsbGroupSetting, "tsbGroupSetting");
            this.tsbGroupSetting.Click += new System.EventHandler(this.tsbGroupSetting_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            resources.ApplyResources(this.toolStripSeparator14, "toolStripSeparator14");
            // 
            // tsbBackupGuiNConfig
            // 
            this.tsbBackupGuiNConfig.Name = "tsbBackupGuiNConfig";
            resources.ApplyResources(this.tsbBackupGuiNConfig, "tsbBackupGuiNConfig");
            this.tsbBackupGuiNConfig.Click += new System.EventHandler(this.tsbBackupGuiNConfig_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(this.toolStripSeparator7, "toolStripSeparator7");
            // 
            // tsbReload
            // 
            this.tsbReload.Image = global::v2rayN.Properties.Resources.restart;
            resources.ApplyResources(this.tsbReload, "tsbReload");
            this.tsbReload.Name = "tsbReload";
            this.tsbReload.Click += new System.EventHandler(this.tsbReload_Click);
            // 
            // tsbCheckUpdate
            // 
            this.tsbCheckUpdate.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbCheckUpdateN,
            this.tsbCheckUpdateCore,
            this.tsbCheckUpdateXrayCore,
            this.toolStripSeparator16,
            this.tsbCheckUpdateClashCore,
            this.tsbCheckUpdateClashMetaCore,
            this.toolStripSeparator15,
            this.tsbCheckUpdateGeo});
            this.tsbCheckUpdate.Image = global::v2rayN.Properties.Resources.checkupdate;
            resources.ApplyResources(this.tsbCheckUpdate, "tsbCheckUpdate");
            this.tsbCheckUpdate.Name = "tsbCheckUpdate";
            this.tsbCheckUpdate.Click += new System.EventHandler(this.tsbCheckUpdate_Click);
            // 
            // tsbCheckUpdateN
            // 
            this.tsbCheckUpdateN.Name = "tsbCheckUpdateN";
            resources.ApplyResources(this.tsbCheckUpdateN, "tsbCheckUpdateN");
            this.tsbCheckUpdateN.Click += new System.EventHandler(this.tsbCheckUpdateN_Click);
            // 
            // tsbCheckUpdateCore
            // 
            this.tsbCheckUpdateCore.Name = "tsbCheckUpdateCore";
            resources.ApplyResources(this.tsbCheckUpdateCore, "tsbCheckUpdateCore");
            this.tsbCheckUpdateCore.Click += new System.EventHandler(this.tsbCheckUpdateCore_Click);
            // 
            // tsbCheckUpdateXrayCore
            // 
            this.tsbCheckUpdateXrayCore.Name = "tsbCheckUpdateXrayCore";
            resources.ApplyResources(this.tsbCheckUpdateXrayCore, "tsbCheckUpdateXrayCore");
            this.tsbCheckUpdateXrayCore.Click += new System.EventHandler(this.tsbCheckUpdateXrayCore_Click);
            // 
            // toolStripSeparator16
            // 
            this.toolStripSeparator16.Name = "toolStripSeparator16";
            resources.ApplyResources(this.toolStripSeparator16, "toolStripSeparator16");
            // 
            // tsbCheckUpdateClashCore
            // 
            this.tsbCheckUpdateClashCore.Name = "tsbCheckUpdateClashCore";
            resources.ApplyResources(this.tsbCheckUpdateClashCore, "tsbCheckUpdateClashCore");
            this.tsbCheckUpdateClashCore.Click += new System.EventHandler(this.tsbCheckUpdateClashCore_Click);
            // 
            // tsbCheckUpdateClashMetaCore
            // 
            this.tsbCheckUpdateClashMetaCore.Name = "tsbCheckUpdateClashMetaCore";
            resources.ApplyResources(this.tsbCheckUpdateClashMetaCore, "tsbCheckUpdateClashMetaCore");
            this.tsbCheckUpdateClashMetaCore.Click += new System.EventHandler(this.tsbCheckUpdateClashMetaCore_Click);
            // 
            // toolStripSeparator15
            // 
            this.toolStripSeparator15.Name = "toolStripSeparator15";
            resources.ApplyResources(this.toolStripSeparator15, "toolStripSeparator15");
            // 
            // tsbCheckUpdateGeo
            // 
            this.tsbCheckUpdateGeo.Name = "tsbCheckUpdateGeo";
            resources.ApplyResources(this.tsbCheckUpdateGeo, "tsbCheckUpdateGeo");
            this.tsbCheckUpdateGeo.Click += new System.EventHandler(this.tsbCheckUpdateGeo_Click);
            // 
            // tsbHelp
            // 
            this.tsbHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbAbout,
            this.tsbV2rayWebsite,
            this.toolStripSeparator12,
            this.tsbLanguageDef,
            this.tsbLanguageZhHans});
            this.tsbHelp.Image = global::v2rayN.Properties.Resources.help;
            resources.ApplyResources(this.tsbHelp, "tsbHelp");
            this.tsbHelp.Name = "tsbHelp";
            // 
            // tsbAbout
            // 
            this.tsbAbout.Name = "tsbAbout";
            resources.ApplyResources(this.tsbAbout, "tsbAbout");
            this.tsbAbout.Click += new System.EventHandler(this.tsbAbout_Click);
            // 
            // tsbV2rayWebsite
            // 
            this.tsbV2rayWebsite.Name = "tsbV2rayWebsite";
            resources.ApplyResources(this.tsbV2rayWebsite, "tsbV2rayWebsite");
            this.tsbV2rayWebsite.Click += new System.EventHandler(this.tsbV2rayWebsite_Click);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            resources.ApplyResources(this.toolStripSeparator12, "toolStripSeparator12");
            // 
            // tsbLanguageDef
            // 
            this.tsbLanguageDef.Name = "tsbLanguageDef";
            resources.ApplyResources(this.tsbLanguageDef, "tsbLanguageDef");
            this.tsbLanguageDef.Click += new System.EventHandler(this.tsbLanguageDef_Click);
            // 
            // tsbLanguageZhHans
            // 
            this.tsbLanguageZhHans.Name = "tsbLanguageZhHans";
            resources.ApplyResources(this.tsbLanguageZhHans, "tsbLanguageZhHans");
            this.tsbLanguageZhHans.Click += new System.EventHandler(this.tsbLanguageZhHans_Click);
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            resources.ApplyResources(this.toolStripSeparator11, "toolStripSeparator11");
            // 
            // tsbClose
            // 
            this.tsbClose.Image = global::v2rayN.Properties.Resources.minimize;
            resources.ApplyResources(this.tsbClose, "tsbClose");
            this.tsbClose.Name = "tsbClose";
            this.tsbClose.Click += new System.EventHandler(this.tsbClose_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            resources.ApplyResources(this.toolStripSeparator4, "toolStripSeparator4");
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.scBig);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.tsMain);
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.VisibleChanged += new System.EventHandler(this.MainForm_VisibleChanged);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.scServers.Panel1.ResumeLayout(false);
            this.scServers.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.scServers)).EndInit();
            this.scServers.ResumeLayout(false);
            this.cmsLv.ResumeLayout(false);
            this.scBig.Panel1.ResumeLayout(false);
            this.scBig.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.scBig)).EndInit();
            this.scBig.ResumeLayout(false);
            this.gbServers.ResumeLayout(false);
            this.cmsMain.ResumeLayout(false);
            this.tsMain.ResumeLayout(false);
            this.tsMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

#endregion

        private System.Windows.Forms.GroupBox gbServers;
        private v2rayN.Base.ListViewFlickerFree lvServers;
        private System.Windows.Forms.NotifyIcon notifyMain;
        private System.Windows.Forms.ContextMenuStrip cmsMain;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStripMenuItem menuServers;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ContextMenuStrip cmsLv;
        private System.Windows.Forms.ToolStripMenuItem menuAddVmessServer;
        private System.Windows.Forms.ToolStripMenuItem menuRemoveServer;
        private System.Windows.Forms.ToolStripMenuItem menuSetDefaultServer;
        private System.Windows.Forms.ToolStripMenuItem menuCopyServer;
        private System.Windows.Forms.ToolStripMenuItem menuPingServer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem menuExport2ClientConfig;
        private System.Windows.Forms.ToolStripMenuItem menuExport2ServerConfig;
        private System.Windows.Forms.ToolStrip tsMain;
        private System.Windows.Forms.ToolStripDropDownButton tsbServer;
        private System.Windows.Forms.ToolStripButton tsbClose;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
        private System.Windows.Forms.ToolStripMenuItem menuSysAgentMode;
        private System.Windows.Forms.ToolStripMenuItem menuGlobal;
        private System.Windows.Forms.ToolStripMenuItem menuKeepClear;
        private System.Windows.Forms.ToolStripMenuItem menuAddCustomServer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem menuAddShadowsocksServer;
        private System.Windows.Forms.SplitContainer scServers;
        private QRCodeControl qrCodeControl;
        private System.Windows.Forms.ToolStripDropDownButton tsbCheckUpdate;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateN;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateCore;
        private System.Windows.Forms.ToolStripMenuItem menuAddServers;
        private System.Windows.Forms.ToolStripMenuItem menuExport2ShareUrl;
        private System.Windows.Forms.ToolStripMenuItem menuSpeedServer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripDropDownButton tsbHelp;
        private System.Windows.Forms.ToolStripMenuItem tsbAbout;
        private System.Windows.Forms.ToolStripMenuItem menuAddServers2;
        private System.Windows.Forms.ToolStripMenuItem menuScanScreen;
        private System.Windows.Forms.ToolStripMenuItem menuScanScreen2;
        private System.Windows.Forms.ToolStripDropDownButton tsbSub;
        private System.Windows.Forms.ToolStripMenuItem tsbSubSetting;
        private System.Windows.Forms.ToolStripMenuItem tsbSubUpdate;
        private System.Windows.Forms.ToolStripMenuItem menuSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuExport2SubContent;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem tsbLanguageDef;
        private System.Windows.Forms.ToolStripMenuItem tsbLanguageZhHans;
        private System.Windows.Forms.ToolStripMenuItem menuAddSocksServer;
        private System.Windows.Forms.ToolStripMenuItem menuRemoveDuplicateServer;
        private System.Windows.Forms.ToolStripMenuItem menuTcpingServer;
        private System.Windows.Forms.ToolStripMenuItem menuRealPingServer;
        private System.Windows.Forms.ToolStripMenuItem menuUpdateSubscriptions;
        private System.Windows.Forms.ToolStripMenuItem tsbV2rayWebsite;
        private System.Windows.Forms.ToolStripMenuItem menuKeepNothing;
        private System.Windows.Forms.ToolStripMenuItem tsbTestMe;
        private System.Windows.Forms.ToolStripButton tsbReload;
        private System.Windows.Forms.ToolStripButton tsbQRCodeSwitch;
        private System.Windows.Forms.ToolStripMenuItem menuAddVlessServer;
        private System.Windows.Forms.ToolStripMenuItem menuAddTrojanServer;
        private System.Windows.Forms.ToolStripDropDownButton tsbSetting;
        private System.Windows.Forms.ToolStripMenuItem tsbOptionSetting;
        private System.Windows.Forms.ToolStripMenuItem tsbRoutingSetting;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateXrayCore;
        private System.Windows.Forms.ToolStripMenuItem menuClearServerStatistics;
        private System.Windows.Forms.ToolStripMenuItem menuRoutings;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripMenuItem tsbBackupGuiNConfig;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateGeo;
        private System.Windows.Forms.SplitContainer scBig;
        private System.Windows.Forms.ToolStripMenuItem tsbSubUpdateViaProxy;
        private System.Windows.Forms.ToolStripMenuItem menuUpdateSubViaProxy;
        private System.Windows.Forms.ToolStripMenuItem tsbGlobalHotkeySetting;
        private System.Windows.Forms.TabControl tabGroup;
        private System.Windows.Forms.ToolStripMenuItem tsbGroupSetting;
        private System.Windows.Forms.ToolStripMenuItem menuMoveToGroup;
        private MainMsgControl mainMsgControl;
        private System.Windows.Forms.ToolStripMenuItem menuMoveEvent;
        private System.Windows.Forms.ToolStripMenuItem menuMoveTop;
        private System.Windows.Forms.ToolStripMenuItem menuMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuMoveDown;
        private System.Windows.Forms.ToolStripMenuItem menuMoveBottom;
        private System.Windows.Forms.ToolStripMenuItem menuServerFilter;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateClashCore;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateClashMetaCore;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator16;
        private System.Windows.Forms.ToolStripMenuItem menuSortServerResult;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
    }
}

