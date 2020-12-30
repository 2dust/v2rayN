namespace v2rayN.Forms
{
    partial class RoutingSettingForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RoutingSettingForm));
            this.btnClose = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnOK = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.linkLabelRoutingDoc = new System.Windows.Forms.LinkLabel();
            this.cmbdomainStrategy = new System.Windows.Forms.ComboBox();
            this.lvRoutings = new v2rayN.Base.ListViewFlickerFree();
            this.cmsLv = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuAdd = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRemove = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportSelectedRules = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.menuMoveTop = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveUp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveDown = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveBottom = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl2 = new System.Windows.Forms.TabControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.menuServer = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImportRulesFromPreset = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImportRulesFromFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImportRulesFromClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.cmsLv.SuspendLayout();
            this.tabControl2.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.menuServer.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnClose
            // 
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.btnClose);
            this.panel2.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.linkLabelRoutingDoc);
            this.panel1.Controls.Add(this.cmbdomainStrategy);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // linkLabelRoutingDoc
            // 
            resources.ApplyResources(this.linkLabelRoutingDoc, "linkLabelRoutingDoc");
            this.linkLabelRoutingDoc.Name = "linkLabelRoutingDoc";
            this.linkLabelRoutingDoc.TabStop = true;
            this.linkLabelRoutingDoc.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelRoutingDoc_LinkClicked);
            // 
            // cmbdomainStrategy
            // 
            this.cmbdomainStrategy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbdomainStrategy.FormattingEnabled = true;
            this.cmbdomainStrategy.Items.AddRange(new object[] {
            resources.GetString("cmbdomainStrategy.Items"),
            resources.GetString("cmbdomainStrategy.Items1"),
            resources.GetString("cmbdomainStrategy.Items2")});
            resources.ApplyResources(this.cmbdomainStrategy, "cmbdomainStrategy");
            this.cmbdomainStrategy.Name = "cmbdomainStrategy";
            // 
            // lvRoutings
            // 
            this.lvRoutings.ContextMenuStrip = this.cmsLv;
            resources.ApplyResources(this.lvRoutings, "lvRoutings");
            this.lvRoutings.FullRowSelect = true;
            this.lvRoutings.GridLines = true;
            this.lvRoutings.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvRoutings.HideSelection = false;
            this.lvRoutings.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            ((System.Windows.Forms.ListViewItem)(resources.GetObject("lvRoutings.Items")))});
            this.lvRoutings.MultiSelect = false;
            this.lvRoutings.Name = "lvRoutings";
            this.lvRoutings.UseCompatibleStateImageBehavior = false;
            this.lvRoutings.View = System.Windows.Forms.View.Details;
            this.lvRoutings.DoubleClick += new System.EventHandler(this.lvRoutings_DoubleClick);
            this.lvRoutings.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvRoutings_KeyDown);
            // 
            // cmsLv
            // 
            this.cmsLv.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsLv.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAdd,
            this.menuRemove,
            this.menuSelectAll,
            this.menuExportSelectedRules,
            this.toolStripSeparator3,
            this.menuMoveTop,
            this.menuMoveUp,
            this.menuMoveDown,
            this.menuMoveBottom});
            this.cmsLv.Name = "cmsLv";
            resources.ApplyResources(this.cmsLv, "cmsLv");
            // 
            // menuAdd
            // 
            this.menuAdd.Name = "menuAdd";
            resources.ApplyResources(this.menuAdd, "menuAdd");
            this.menuAdd.Click += new System.EventHandler(this.menuAdd_Click);
            // 
            // menuRemove
            // 
            this.menuRemove.Name = "menuRemove";
            resources.ApplyResources(this.menuRemove, "menuRemove");
            this.menuRemove.Click += new System.EventHandler(this.menuRemove_Click);
            // 
            // menuSelectAll
            // 
            this.menuSelectAll.Name = "menuSelectAll";
            resources.ApplyResources(this.menuSelectAll, "menuSelectAll");
            this.menuSelectAll.Click += new System.EventHandler(this.menuSelectAll_Click);
            // 
            // menuExportSelectedRules
            // 
            this.menuExportSelectedRules.Name = "menuExportSelectedRules";
            resources.ApplyResources(this.menuExportSelectedRules, "menuExportSelectedRules");
            this.menuExportSelectedRules.Click += new System.EventHandler(this.menuExportSelectedRules_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
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
            // MenuItem1
            // 
            this.MenuItem1.DropDown = this.cmsLv;
            this.MenuItem1.Name = "MenuItem1";
            resources.ApplyResources(this.MenuItem1, "MenuItem1");
            // 
            // tabControl2
            // 
            this.tabControl2.Controls.Add(this.tabPage2);
            resources.ApplyResources(this.tabControl2, "tabControl2");
            this.tabControl2.Name = "tabControl2";
            this.tabControl2.SelectedIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.lvRoutings);
            resources.ApplyResources(this.tabPage2, "tabPage2");
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // menuServer
            // 
            this.menuServer.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuItem1,
            this.toolStripMenuItem1});
            resources.ApplyResources(this.menuServer, "menuServer");
            this.menuServer.Name = "menuServer";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuImportRulesFromPreset,
            this.menuImportRulesFromFile,
            this.menuImportRulesFromClipboard});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(this.toolStripMenuItem1, "toolStripMenuItem1");
            // 
            // menuImportRulesFromPreset
            // 
            this.menuImportRulesFromPreset.Name = "menuImportRulesFromPreset";
            resources.ApplyResources(this.menuImportRulesFromPreset, "menuImportRulesFromPreset");
            this.menuImportRulesFromPreset.Click += new System.EventHandler(this.menuImportRulesFromPreset_Click);
            // 
            // menuImportRulesFromFile
            // 
            this.menuImportRulesFromFile.Name = "menuImportRulesFromFile";
            resources.ApplyResources(this.menuImportRulesFromFile, "menuImportRulesFromFile");
            this.menuImportRulesFromFile.Click += new System.EventHandler(this.menuImportRulesFromFile_Click);
            // 
            // menuImportRulesFromClipboard
            // 
            this.menuImportRulesFromClipboard.Name = "menuImportRulesFromClipboard";
            resources.ApplyResources(this.menuImportRulesFromClipboard, "menuImportRulesFromClipboard");
            this.menuImportRulesFromClipboard.Click += new System.EventHandler(this.menuImportRulesFromClipboard_Click);
            // 
            // RoutingSettingForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.tabControl2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.menuServer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "RoutingSettingForm";
            this.Load += new System.EventHandler(this.RoutingSettingForm_Load);
            this.panel2.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.cmsLv.ResumeLayout(false);
            this.tabControl2.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.menuServer.ResumeLayout(false);
            this.menuServer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel linkLabelRoutingDoc;
        private System.Windows.Forms.ComboBox cmbdomainStrategy;
        private Base.ListViewFlickerFree lvRoutings;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ContextMenuStrip cmsLv;
        private System.Windows.Forms.ToolStripMenuItem menuRemove;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem menuMoveTop;
        private System.Windows.Forms.ToolStripMenuItem menuMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuMoveDown;
        private System.Windows.Forms.ToolStripMenuItem menuMoveBottom;
        private System.Windows.Forms.ToolStripMenuItem menuSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuAdd;
        private System.Windows.Forms.MenuStrip menuServer;
        private System.Windows.Forms.ToolStripMenuItem MenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem menuImportRulesFromPreset;
        private System.Windows.Forms.ToolStripMenuItem menuImportRulesFromFile;
        private System.Windows.Forms.ToolStripMenuItem menuImportRulesFromClipboard;
        private System.Windows.Forms.ToolStripMenuItem menuExportSelectedRules;
    }
}