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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvServers = new v2rayN.Base.ListViewFlickerFree();
            this.cmsLv = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuAddVmessServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddShadowsocksServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddSocksServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddCustomServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddServers = new System.Windows.Forms.ToolStripMenuItem();
            this.menuScanScreen = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuRemoveServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRemoveDuplicateServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCopyServer = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSetDefaultServer = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
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
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExport2ClientConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2ServerConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2ShareUrl = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExport2SubContent = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbServer = new System.Windows.Forms.ToolStripDropDownButton();
            this.qrCodeControl = new v2rayN.Forms.QRCodeControl();
            this.notifyMain = new System.Windows.Forms.NotifyIcon(this.components);
            this.cmsMain = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuSysAgentMode = new System.Windows.Forms.ToolStripMenuItem();
            this.menuNotEnabledHttp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGlobal = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGlobalPAC = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeep = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeepPAC = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeepNothing = new System.Windows.Forms.ToolStripMenuItem();
            this.menuKeepPACNothing = new System.Windows.Forms.ToolStripMenuItem();
            this.menuServers = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddServers2 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuScanScreen2 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCopyPACUrl = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUpdateSubscriptions = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.bgwScan = new System.ComponentModel.BackgroundWorker();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.txtMsgBox = new System.Windows.Forms.TextBox();
            this.ssMain = new System.Windows.Forms.StatusStrip();
            this.toolSslSocksPortLab = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslSocksPort = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslHttpPortLab = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslHttpPort = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslPacPortLab = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslPacPort = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslServerSpeed = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank4 = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tsMain = new System.Windows.Forms.ToolStrip();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbSub = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbSubSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbSubUpdate = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbOptionSetting = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbService = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbReload = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbTestMe = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbCheckUpdate = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbCheckUpdateN = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCheckUpdateCore = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbCheckUpdatePACList = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbCheckClearPACList = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbHelp = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbV2rayWebsite = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbLanguageDef = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbLanguageZhHans = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbPromotion = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbClose = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.cmsLv.SuspendLayout();
            this.cmsMain.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.ssMain.SuspendLayout();
            this.tsMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            resources.ApplyResources(this.splitContainer1, "splitContainer1");
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            resources.ApplyResources(this.splitContainer1.Panel1, "splitContainer1.Panel1");
            this.splitContainer1.Panel1.Controls.Add(this.lvServers);
            // 
            // splitContainer1.Panel2
            // 
            resources.ApplyResources(this.splitContainer1.Panel2, "splitContainer1.Panel2");
            this.splitContainer1.Panel2.Controls.Add(this.qrCodeControl);
            this.splitContainer1.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitContainer1_SplitterMoved);
            // 
            // lvServers
            // 
            resources.ApplyResources(this.lvServers, "lvServers");
            this.lvServers.ContextMenuStrip = this.cmsLv;
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
            this.lvServers.SelectedIndexChanged += new System.EventHandler(this.lvServers_SelectedIndexChanged);
            this.lvServers.Click += new System.EventHandler(this.lvServers_Click);
            this.lvServers.DoubleClick += new System.EventHandler(this.lvServers_DoubleClick);
            this.lvServers.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvServers_KeyDown);
            // 
            // cmsLv
            // 
            resources.ApplyResources(this.cmsLv, "cmsLv");
            this.cmsLv.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsLv.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAddVmessServer,
            this.menuAddShadowsocksServer,
            this.menuAddSocksServer,
            this.menuAddCustomServer,
            this.menuAddServers,
            this.menuScanScreen,
            this.toolStripSeparator1,
            this.menuRemoveServer,
            this.menuRemoveDuplicateServer,
            this.menuCopyServer,
            this.menuSetDefaultServer,
            this.toolStripSeparator3,
            this.menuMoveTop,
            this.menuMoveUp,
            this.menuMoveDown,
            this.menuMoveBottom,
            this.menuSelectAll,
            this.toolStripSeparator9,
            this.menuPingServer,
            this.menuTcpingServer,
            this.menuRealPingServer,
            this.menuSpeedServer,
            this.toolStripSeparator6,
            this.menuExport2ClientConfig,
            this.menuExport2ServerConfig,
            this.menuExport2ShareUrl,
            this.menuExport2SubContent});
            this.cmsLv.Name = "cmsLv";
            this.cmsLv.OwnerItem = this.tsbServer;
            // 
            // menuAddVmessServer
            // 
            resources.ApplyResources(this.menuAddVmessServer, "menuAddVmessServer");
            this.menuAddVmessServer.Name = "menuAddVmessServer";
            this.menuAddVmessServer.Click += new System.EventHandler(this.menuAddVmessServer_Click);
            // 
            // menuAddShadowsocksServer
            // 
            resources.ApplyResources(this.menuAddShadowsocksServer, "menuAddShadowsocksServer");
            this.menuAddShadowsocksServer.Name = "menuAddShadowsocksServer";
            this.menuAddShadowsocksServer.Click += new System.EventHandler(this.menuAddShadowsocksServer_Click);
            // 
            // menuAddSocksServer
            // 
            resources.ApplyResources(this.menuAddSocksServer, "menuAddSocksServer");
            this.menuAddSocksServer.Name = "menuAddSocksServer";
            this.menuAddSocksServer.Click += new System.EventHandler(this.menuAddSocksServer_Click);
            // 
            // menuAddCustomServer
            // 
            resources.ApplyResources(this.menuAddCustomServer, "menuAddCustomServer");
            this.menuAddCustomServer.Name = "menuAddCustomServer";
            this.menuAddCustomServer.Click += new System.EventHandler(this.menuAddCustomServer_Click);
            // 
            // menuAddServers
            // 
            resources.ApplyResources(this.menuAddServers, "menuAddServers");
            this.menuAddServers.Name = "menuAddServers";
            this.menuAddServers.Click += new System.EventHandler(this.menuAddServers_Click);
            // 
            // menuScanScreen
            // 
            resources.ApplyResources(this.menuScanScreen, "menuScanScreen");
            this.menuScanScreen.Name = "menuScanScreen";
            this.menuScanScreen.Click += new System.EventHandler(this.menuScanScreen_Click);
            // 
            // toolStripSeparator1
            // 
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            // 
            // menuRemoveServer
            // 
            resources.ApplyResources(this.menuRemoveServer, "menuRemoveServer");
            this.menuRemoveServer.Name = "menuRemoveServer";
            this.menuRemoveServer.Click += new System.EventHandler(this.menuRemoveServer_Click);
            // 
            // menuRemoveDuplicateServer
            // 
            resources.ApplyResources(this.menuRemoveDuplicateServer, "menuRemoveDuplicateServer");
            this.menuRemoveDuplicateServer.Name = "menuRemoveDuplicateServer";
            this.menuRemoveDuplicateServer.Click += new System.EventHandler(this.menuRemoveDuplicateServer_Click);
            // 
            // menuCopyServer
            // 
            resources.ApplyResources(this.menuCopyServer, "menuCopyServer");
            this.menuCopyServer.Name = "menuCopyServer";
            this.menuCopyServer.Click += new System.EventHandler(this.menuCopyServer_Click);
            // 
            // menuSetDefaultServer
            // 
            resources.ApplyResources(this.menuSetDefaultServer, "menuSetDefaultServer");
            this.menuSetDefaultServer.Name = "menuSetDefaultServer";
            this.menuSetDefaultServer.Click += new System.EventHandler(this.menuSetDefaultServer_Click);
            // 
            // toolStripSeparator3
            // 
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            // 
            // menuMoveTop
            // 
            resources.ApplyResources(this.menuMoveTop, "menuMoveTop");
            this.menuMoveTop.Name = "menuMoveTop";
            this.menuMoveTop.Click += new System.EventHandler(this.menuMoveTop_Click);
            // 
            // menuMoveUp
            // 
            resources.ApplyResources(this.menuMoveUp, "menuMoveUp");
            this.menuMoveUp.Name = "menuMoveUp";
            this.menuMoveUp.Click += new System.EventHandler(this.menuMoveUp_Click);
            // 
            // menuMoveDown
            // 
            resources.ApplyResources(this.menuMoveDown, "menuMoveDown");
            this.menuMoveDown.Name = "menuMoveDown";
            this.menuMoveDown.Click += new System.EventHandler(this.menuMoveDown_Click);
            // 
            // menuMoveBottom
            // 
            resources.ApplyResources(this.menuMoveBottom, "menuMoveBottom");
            this.menuMoveBottom.Name = "menuMoveBottom";
            this.menuMoveBottom.Click += new System.EventHandler(this.menuMoveBottom_Click);
            // 
            // menuSelectAll
            // 
            resources.ApplyResources(this.menuSelectAll, "menuSelectAll");
            this.menuSelectAll.Name = "menuSelectAll";
            this.menuSelectAll.Click += new System.EventHandler(this.menuSelectAll_Click);
            // 
            // toolStripSeparator9
            // 
            resources.ApplyResources(this.toolStripSeparator9, "toolStripSeparator9");
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            // 
            // menuPingServer
            // 
            resources.ApplyResources(this.menuPingServer, "menuPingServer");
            this.menuPingServer.Name = "menuPingServer";
            this.menuPingServer.Click += new System.EventHandler(this.menuPingServer_Click);
            // 
            // menuTcpingServer
            // 
            resources.ApplyResources(this.menuTcpingServer, "menuTcpingServer");
            this.menuTcpingServer.Name = "menuTcpingServer";
            this.menuTcpingServer.Click += new System.EventHandler(this.menuTcpingServer_Click);
            // 
            // menuRealPingServer
            // 
            resources.ApplyResources(this.menuRealPingServer, "menuRealPingServer");
            this.menuRealPingServer.Name = "menuRealPingServer";
            this.menuRealPingServer.Click += new System.EventHandler(this.menuRealPingServer_Click);
            // 
            // menuSpeedServer
            // 
            resources.ApplyResources(this.menuSpeedServer, "menuSpeedServer");
            this.menuSpeedServer.Name = "menuSpeedServer";
            this.menuSpeedServer.Click += new System.EventHandler(this.menuSpeedServer_Click);
            // 
            // toolStripSeparator6
            // 
            resources.ApplyResources(this.toolStripSeparator6, "toolStripSeparator6");
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            // 
            // menuExport2ClientConfig
            // 
            resources.ApplyResources(this.menuExport2ClientConfig, "menuExport2ClientConfig");
            this.menuExport2ClientConfig.Name = "menuExport2ClientConfig";
            this.menuExport2ClientConfig.Click += new System.EventHandler(this.menuExport2ClientConfig_Click);
            // 
            // menuExport2ServerConfig
            // 
            resources.ApplyResources(this.menuExport2ServerConfig, "menuExport2ServerConfig");
            this.menuExport2ServerConfig.Name = "menuExport2ServerConfig";
            this.menuExport2ServerConfig.Click += new System.EventHandler(this.menuExport2ServerConfig_Click);
            // 
            // menuExport2ShareUrl
            // 
            resources.ApplyResources(this.menuExport2ShareUrl, "menuExport2ShareUrl");
            this.menuExport2ShareUrl.Name = "menuExport2ShareUrl";
            this.menuExport2ShareUrl.Click += new System.EventHandler(this.menuExport2ShareUrl_Click);
            // 
            // menuExport2SubContent
            // 
            resources.ApplyResources(this.menuExport2SubContent, "menuExport2SubContent");
            this.menuExport2SubContent.Name = "menuExport2SubContent";
            this.menuExport2SubContent.Click += new System.EventHandler(this.menuExport2SubContent_Click);
            // 
            // tsbServer
            // 
            resources.ApplyResources(this.tsbServer, "tsbServer");
            this.tsbServer.DropDown = this.cmsLv;
            this.tsbServer.Image = global::v2rayN.Properties.Resources.server;
            this.tsbServer.Name = "tsbServer";
            // 
            // qrCodeControl
            // 
            resources.ApplyResources(this.qrCodeControl, "qrCodeControl");
            this.qrCodeControl.Name = "qrCodeControl";
            // 
            // notifyMain
            // 
            resources.ApplyResources(this.notifyMain, "notifyMain");
            this.notifyMain.ContextMenuStrip = this.cmsMain;
            this.notifyMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyMain_MouseClick);
            // 
            // cmsMain
            // 
            resources.ApplyResources(this.cmsMain, "cmsMain");
            this.cmsMain.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuSysAgentMode,
            this.menuServers,
            this.menuAddServers2,
            this.menuScanScreen2,
            this.menuCopyPACUrl,
            this.menuUpdateSubscriptions,
            this.toolStripSeparator2,
            this.menuExit});
            this.cmsMain.Name = "contextMenuStrip1";
            this.cmsMain.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.cmsMain.ShowCheckMargin = true;
            this.cmsMain.ShowImageMargin = false;
            // 
            // menuSysAgentMode
            // 
            resources.ApplyResources(this.menuSysAgentMode, "menuSysAgentMode");
            this.menuSysAgentMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuNotEnabledHttp,
            this.menuGlobal,
            this.menuGlobalPAC,
            this.menuKeep,
            this.menuKeepPAC,
            this.menuKeepNothing,
            this.menuKeepPACNothing});
            this.menuSysAgentMode.Name = "menuSysAgentMode";
            // 
            // menuNotEnabledHttp
            // 
            resources.ApplyResources(this.menuNotEnabledHttp, "menuNotEnabledHttp");
            this.menuNotEnabledHttp.Name = "menuNotEnabledHttp";
            this.menuNotEnabledHttp.Click += new System.EventHandler(this.menuNotEnabledHttp_Click);
            // 
            // menuGlobal
            // 
            resources.ApplyResources(this.menuGlobal, "menuGlobal");
            this.menuGlobal.Name = "menuGlobal";
            this.menuGlobal.Click += new System.EventHandler(this.menuGlobal_Click);
            // 
            // menuGlobalPAC
            // 
            resources.ApplyResources(this.menuGlobalPAC, "menuGlobalPAC");
            this.menuGlobalPAC.Name = "menuGlobalPAC";
            this.menuGlobalPAC.Click += new System.EventHandler(this.menuGlobalPAC_Click);
            // 
            // menuKeep
            // 
            resources.ApplyResources(this.menuKeep, "menuKeep");
            this.menuKeep.Name = "menuKeep";
            this.menuKeep.Click += new System.EventHandler(this.menuKeep_Click);
            // 
            // menuKeepPAC
            // 
            resources.ApplyResources(this.menuKeepPAC, "menuKeepPAC");
            this.menuKeepPAC.Name = "menuKeepPAC";
            this.menuKeepPAC.Click += new System.EventHandler(this.menuKeepPAC_Click);
            // 
            // menuKeepNothing
            // 
            resources.ApplyResources(this.menuKeepNothing, "menuKeepNothing");
            this.menuKeepNothing.Name = "menuKeepNothing";
            this.menuKeepNothing.Click += new System.EventHandler(this.menuKeepNothing_Click);
            // 
            // menuKeepPACNothing
            // 
            resources.ApplyResources(this.menuKeepPACNothing, "menuKeepPACNothing");
            this.menuKeepPACNothing.Name = "menuKeepPACNothing";
            this.menuKeepPACNothing.Click += new System.EventHandler(this.menuKeepPACNothing_Click);
            // 
            // menuServers
            // 
            resources.ApplyResources(this.menuServers, "menuServers");
            this.menuServers.Name = "menuServers";
            // 
            // menuAddServers2
            // 
            resources.ApplyResources(this.menuAddServers2, "menuAddServers2");
            this.menuAddServers2.Name = "menuAddServers2";
            this.menuAddServers2.Click += new System.EventHandler(this.menuAddServers_Click);
            // 
            // menuScanScreen2
            // 
            resources.ApplyResources(this.menuScanScreen2, "menuScanScreen2");
            this.menuScanScreen2.Name = "menuScanScreen2";
            this.menuScanScreen2.Click += new System.EventHandler(this.menuScanScreen_Click);
            // 
            // menuCopyPACUrl
            // 
            resources.ApplyResources(this.menuCopyPACUrl, "menuCopyPACUrl");
            this.menuCopyPACUrl.Name = "menuCopyPACUrl";
            this.menuCopyPACUrl.Click += new System.EventHandler(this.menuCopyPACUrl_Click);
            // 
            // menuUpdateSubscriptions
            // 
            resources.ApplyResources(this.menuUpdateSubscriptions, "menuUpdateSubscriptions");
            this.menuUpdateSubscriptions.Name = "menuUpdateSubscriptions";
            this.menuUpdateSubscriptions.Click += new System.EventHandler(this.menuUpdateSubscriptions_Click);
            // 
            // toolStripSeparator2
            // 
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            // 
            // menuExit
            // 
            resources.ApplyResources(this.menuExit, "menuExit");
            this.menuExit.Name = "menuExit";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // bgwScan
            // 
            this.bgwScan.WorkerReportsProgress = true;
            this.bgwScan.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgwScan_DoWork);
            this.bgwScan.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.bgwScan_ProgressChanged);
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.splitContainer1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // groupBox2
            // 
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Controls.Add(this.txtMsgBox);
            this.groupBox2.Controls.Add(this.ssMain);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // txtMsgBox
            // 
            resources.ApplyResources(this.txtMsgBox, "txtMsgBox");
            this.txtMsgBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(49)))), ((int)(((byte)(52)))));
            this.txtMsgBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtMsgBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(226)))), ((int)(((byte)(228)))));
            this.txtMsgBox.Name = "txtMsgBox";
            this.txtMsgBox.ReadOnly = true;
            // 
            // ssMain
            // 
            resources.ApplyResources(this.ssMain, "ssMain");
            this.ssMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolSslSocksPortLab,
            this.toolSslSocksPort,
            this.toolSslBlank1,
            this.toolSslHttpPortLab,
            this.toolSslHttpPort,
            this.toolSslBlank2,
            this.toolSslPacPortLab,
            this.toolSslPacPort,
            this.toolSslBlank3,
            this.toolSslServerSpeed,
            this.toolSslBlank4});
            this.ssMain.Name = "ssMain";
            this.ssMain.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.ssMain_ItemClicked);
            // 
            // toolSslSocksPortLab
            // 
            resources.ApplyResources(this.toolSslSocksPortLab, "toolSslSocksPortLab");
            this.toolSslSocksPortLab.Name = "toolSslSocksPortLab";
            // 
            // toolSslSocksPort
            // 
            resources.ApplyResources(this.toolSslSocksPort, "toolSslSocksPort");
            this.toolSslSocksPort.Name = "toolSslSocksPort";
            // 
            // toolSslBlank1
            // 
            resources.ApplyResources(this.toolSslBlank1, "toolSslBlank1");
            this.toolSslBlank1.Name = "toolSslBlank1";
            this.toolSslBlank1.Spring = true;
            // 
            // toolSslHttpPortLab
            // 
            resources.ApplyResources(this.toolSslHttpPortLab, "toolSslHttpPortLab");
            this.toolSslHttpPortLab.Name = "toolSslHttpPortLab";
            // 
            // toolSslHttpPort
            // 
            resources.ApplyResources(this.toolSslHttpPort, "toolSslHttpPort");
            this.toolSslHttpPort.Name = "toolSslHttpPort";
            // 
            // toolSslBlank2
            // 
            resources.ApplyResources(this.toolSslBlank2, "toolSslBlank2");
            this.toolSslBlank2.Name = "toolSslBlank2";
            this.toolSslBlank2.Spring = true;
            // 
            // toolSslPacPortLab
            // 
            resources.ApplyResources(this.toolSslPacPortLab, "toolSslPacPortLab");
            this.toolSslPacPortLab.Name = "toolSslPacPortLab";
            // 
            // toolSslPacPort
            // 
            resources.ApplyResources(this.toolSslPacPort, "toolSslPacPort");
            this.toolSslPacPort.Name = "toolSslPacPort";
            // 
            // toolSslBlank3
            // 
            resources.ApplyResources(this.toolSslBlank3, "toolSslBlank3");
            this.toolSslBlank3.Name = "toolSslBlank3";
            this.toolSslBlank3.Spring = true;
            // 
            // toolSslServerSpeed
            // 
            resources.ApplyResources(this.toolSslServerSpeed, "toolSslServerSpeed");
            this.toolSslServerSpeed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolSslServerSpeed.Name = "toolSslServerSpeed";
            // 
            // toolSslBlank4
            // 
            resources.ApplyResources(this.toolSslBlank4, "toolSslBlank4");
            this.toolSslBlank4.Name = "toolSslBlank4";
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // tsMain
            // 
            resources.ApplyResources(this.tsMain, "tsMain");
            this.tsMain.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.tsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbServer,
            this.toolStripSeparator4,
            this.tsbSub,
            this.toolStripSeparator8,
            this.tsbOptionSetting,
            this.toolStripSeparator5,
            this.tsbService,
            this.toolStripSeparator7,
            this.tsbCheckUpdate,
            this.toolStripSeparator10,
            this.tsbHelp,
            this.tsbPromotion,
            this.toolStripSeparator11,
            this.tsbClose});
            this.tsMain.Name = "tsMain";
            // 
            // toolStripSeparator4
            // 
            resources.ApplyResources(this.toolStripSeparator4, "toolStripSeparator4");
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            // 
            // tsbSub
            // 
            resources.ApplyResources(this.tsbSub, "tsbSub");
            this.tsbSub.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbSubSetting,
            this.tsbSubUpdate});
            this.tsbSub.Image = global::v2rayN.Properties.Resources.sub;
            this.tsbSub.Name = "tsbSub";
            // 
            // tsbSubSetting
            // 
            resources.ApplyResources(this.tsbSubSetting, "tsbSubSetting");
            this.tsbSubSetting.Name = "tsbSubSetting";
            this.tsbSubSetting.Click += new System.EventHandler(this.tsbSubSetting_Click);
            // 
            // tsbSubUpdate
            // 
            resources.ApplyResources(this.tsbSubUpdate, "tsbSubUpdate");
            this.tsbSubUpdate.Name = "tsbSubUpdate";
            this.tsbSubUpdate.Click += new System.EventHandler(this.tsbSubUpdate_Click);
            // 
            // toolStripSeparator8
            // 
            resources.ApplyResources(this.toolStripSeparator8, "toolStripSeparator8");
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            // 
            // tsbOptionSetting
            // 
            resources.ApplyResources(this.tsbOptionSetting, "tsbOptionSetting");
            this.tsbOptionSetting.Image = global::v2rayN.Properties.Resources.option;
            this.tsbOptionSetting.Name = "tsbOptionSetting";
            this.tsbOptionSetting.Click += new System.EventHandler(this.tsbOptionSetting_Click);
            // 
            // toolStripSeparator5
            // 
            resources.ApplyResources(this.toolStripSeparator5, "toolStripSeparator5");
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            // 
            // tsbService
            // 
            resources.ApplyResources(this.tsbService, "tsbService");
            this.tsbService.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbReload,
            this.tsbTestMe});
            this.tsbService.Name = "tsbService";
            // 
            // tsbReload
            // 
            resources.ApplyResources(this.tsbReload, "tsbReload");
            this.tsbReload.Name = "tsbReload";
            this.tsbReload.Click += new System.EventHandler(this.tsbReload_Click);
            // 
            // tsbTestMe
            // 
            resources.ApplyResources(this.tsbTestMe, "tsbTestMe");
            this.tsbTestMe.Name = "tsbTestMe";
            this.tsbTestMe.Click += new System.EventHandler(this.tsbTestMe_Click);
            // 
            // toolStripSeparator7
            // 
            resources.ApplyResources(this.toolStripSeparator7, "toolStripSeparator7");
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            // 
            // tsbCheckUpdate
            // 
            resources.ApplyResources(this.tsbCheckUpdate, "tsbCheckUpdate");
            this.tsbCheckUpdate.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbCheckUpdateN,
            this.tsbCheckUpdateCore,
            this.tsbCheckUpdatePACList,
            this.toolStripSeparator13,
            this.tsbCheckClearPACList});
            this.tsbCheckUpdate.Image = global::v2rayN.Properties.Resources.checkupdate;
            this.tsbCheckUpdate.Name = "tsbCheckUpdate";
            // 
            // tsbCheckUpdateN
            // 
            resources.ApplyResources(this.tsbCheckUpdateN, "tsbCheckUpdateN");
            this.tsbCheckUpdateN.Name = "tsbCheckUpdateN";
            this.tsbCheckUpdateN.Click += new System.EventHandler(this.tsbCheckUpdateN_Click);
            // 
            // tsbCheckUpdateCore
            // 
            resources.ApplyResources(this.tsbCheckUpdateCore, "tsbCheckUpdateCore");
            this.tsbCheckUpdateCore.Name = "tsbCheckUpdateCore";
            this.tsbCheckUpdateCore.Click += new System.EventHandler(this.tsbCheckUpdateCore_Click);
            // 
            // tsbCheckUpdatePACList
            // 
            resources.ApplyResources(this.tsbCheckUpdatePACList, "tsbCheckUpdatePACList");
            this.tsbCheckUpdatePACList.Name = "tsbCheckUpdatePACList";
            this.tsbCheckUpdatePACList.Click += new System.EventHandler(this.tsbCheckUpdatePACList_Click);
            // 
            // toolStripSeparator13
            // 
            resources.ApplyResources(this.toolStripSeparator13, "toolStripSeparator13");
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            // 
            // tsbCheckClearPACList
            // 
            resources.ApplyResources(this.tsbCheckClearPACList, "tsbCheckClearPACList");
            this.tsbCheckClearPACList.Name = "tsbCheckClearPACList";
            this.tsbCheckClearPACList.Click += new System.EventHandler(this.tsbCheckClearPACList_Click);
            // 
            // toolStripSeparator10
            // 
            resources.ApplyResources(this.toolStripSeparator10, "toolStripSeparator10");
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            // 
            // tsbHelp
            // 
            resources.ApplyResources(this.tsbHelp, "tsbHelp");
            this.tsbHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbAbout,
            this.tsbV2rayWebsite,
            this.toolStripSeparator12,
            this.tsbLanguageDef,
            this.tsbLanguageZhHans});
            this.tsbHelp.Image = global::v2rayN.Properties.Resources.help;
            this.tsbHelp.Name = "tsbHelp";
            // 
            // tsbAbout
            // 
            resources.ApplyResources(this.tsbAbout, "tsbAbout");
            this.tsbAbout.Name = "tsbAbout";
            this.tsbAbout.Click += new System.EventHandler(this.tsbAbout_Click);
            // 
            // tsbV2rayWebsite
            // 
            resources.ApplyResources(this.tsbV2rayWebsite, "tsbV2rayWebsite");
            this.tsbV2rayWebsite.Name = "tsbV2rayWebsite";
            this.tsbV2rayWebsite.Click += new System.EventHandler(this.tsbV2rayWebsite_Click);
            // 
            // toolStripSeparator12
            // 
            resources.ApplyResources(this.toolStripSeparator12, "toolStripSeparator12");
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            // 
            // tsbLanguageDef
            // 
            resources.ApplyResources(this.tsbLanguageDef, "tsbLanguageDef");
            this.tsbLanguageDef.Name = "tsbLanguageDef";
            this.tsbLanguageDef.Click += new System.EventHandler(this.tsbLanguageDef_Click);
            // 
            // tsbLanguageZhHans
            // 
            resources.ApplyResources(this.tsbLanguageZhHans, "tsbLanguageZhHans");
            this.tsbLanguageZhHans.Name = "tsbLanguageZhHans";
            this.tsbLanguageZhHans.Click += new System.EventHandler(this.tsbLanguageZhHans_Click);
            // 
            // tsbPromotion
            // 
            resources.ApplyResources(this.tsbPromotion, "tsbPromotion");
            this.tsbPromotion.ForeColor = System.Drawing.Color.Black;
            this.tsbPromotion.Image = global::v2rayN.Properties.Resources.promotion;
            this.tsbPromotion.Name = "tsbPromotion";
            this.tsbPromotion.Click += new System.EventHandler(this.tsbPromotion_Click);
            // 
            // toolStripSeparator11
            // 
            resources.ApplyResources(this.toolStripSeparator11, "toolStripSeparator11");
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            // 
            // tsbClose
            // 
            resources.ApplyResources(this.tsbClose, "tsbClose");
            this.tsbClose.Name = "tsbClose";
            this.tsbClose.Click += new System.EventHandler(this.tsbClose_Click);
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox2);
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
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.cmsLv.ResumeLayout(false);
            this.cmsMain.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ssMain.ResumeLayout(false);
            this.ssMain.PerformLayout();
            this.tsMain.ResumeLayout(false);
            this.tsMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

#endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtMsgBox;
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
        private System.Windows.Forms.ToolStripButton tsbOptionSetting;
        private System.Windows.Forms.ToolStripButton tsbClose;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem menuMoveTop;
        private System.Windows.Forms.ToolStripMenuItem menuMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuMoveDown;
        private System.Windows.Forms.ToolStripMenuItem menuMoveBottom;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
        private System.Windows.Forms.ToolStripMenuItem menuSysAgentMode;
        private System.Windows.Forms.ToolStripMenuItem menuGlobal;
        private System.Windows.Forms.ToolStripMenuItem menuGlobalPAC;
        private System.Windows.Forms.ToolStripMenuItem menuKeep;
        private System.Windows.Forms.ToolStripMenuItem menuCopyPACUrl;
        private System.Windows.Forms.ToolStripMenuItem menuAddCustomServer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem menuAddShadowsocksServer;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private QRCodeControl qrCodeControl;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator10;
        private System.Windows.Forms.ToolStripDropDownButton tsbCheckUpdate;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateN;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdateCore;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckUpdatePACList;
        private System.Windows.Forms.ToolStripMenuItem menuAddServers;
        private System.Windows.Forms.ToolStripMenuItem menuExport2ShareUrl;
        private System.Windows.Forms.ToolStripMenuItem menuSpeedServer;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripDropDownButton tsbHelp;
        private System.Windows.Forms.ToolStripMenuItem tsbAbout;
        private System.Windows.Forms.ToolStripMenuItem menuAddServers2;
        private System.ComponentModel.BackgroundWorker bgwScan;
        private System.Windows.Forms.ToolStripMenuItem menuScanScreen;
        private System.Windows.Forms.ToolStripMenuItem menuScanScreen2;
        private System.Windows.Forms.ToolStripDropDownButton tsbSub;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripMenuItem tsbSubSetting;
        private System.Windows.Forms.ToolStripMenuItem tsbSubUpdate;
        private System.Windows.Forms.ToolStripMenuItem tsbCheckClearPACList;
        private System.Windows.Forms.ToolStripMenuItem menuKeepPAC;
        private System.Windows.Forms.ToolStripMenuItem menuSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuExport2SubContent;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem tsbLanguageDef;
        private System.Windows.Forms.ToolStripMenuItem tsbLanguageZhHans;
        private System.Windows.Forms.ToolStripButton tsbPromotion;
        private System.Windows.Forms.ToolStripMenuItem menuAddSocksServer;
        private System.Windows.Forms.StatusStrip ssMain;
        private System.Windows.Forms.ToolStripStatusLabel toolSslSocksPort;
        private System.Windows.Forms.ToolStripStatusLabel toolSslHttpPort;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank2;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank1;
        private System.Windows.Forms.ToolStripStatusLabel toolSslPacPort;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank3;
        private System.Windows.Forms.ToolStripStatusLabel toolSslSocksPortLab;
        private System.Windows.Forms.ToolStripStatusLabel toolSslHttpPortLab;
        private System.Windows.Forms.ToolStripStatusLabel toolSslPacPortLab;
        private System.Windows.Forms.ToolStripStatusLabel toolSslServerSpeed;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank4;
        private System.Windows.Forms.ToolStripMenuItem menuRemoveDuplicateServer;
        private System.Windows.Forms.ToolStripMenuItem menuTcpingServer;
        private System.Windows.Forms.ToolStripMenuItem menuRealPingServer;
        private System.Windows.Forms.ToolStripMenuItem menuNotEnabledHttp;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripMenuItem menuUpdateSubscriptions;
        private System.Windows.Forms.ToolStripMenuItem tsbV2rayWebsite;
        private System.Windows.Forms.ToolStripMenuItem menuKeepNothing;
        private System.Windows.Forms.ToolStripMenuItem menuKeepPACNothing;
        private System.Windows.Forms.ToolStripDropDownButton tsbService;
        private System.Windows.Forms.ToolStripMenuItem tsbReload;
        private System.Windows.Forms.ToolStripMenuItem tsbTestMe;
    }
}

