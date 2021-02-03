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
            this.labRoutingTips = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.chkenableRoutingAdvanced = new System.Windows.Forms.CheckBox();
            this.linkLabelRoutingDoc = new System.Windows.Forms.LinkLabel();
            this.cmbdomainStrategy = new System.Windows.Forms.ComboBox();
            this.cmsLv = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuAdd = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRemove = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSetDefaultRouting = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuServer = new System.Windows.Forms.MenuStrip();
            this.tabNormal = new System.Windows.Forms.TabControl();
            this.tabPageProxy = new System.Windows.Forms.TabPage();
            this.panel5 = new System.Windows.Forms.Panel();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.txtProxyIp = new System.Windows.Forms.TextBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.txtProxyDomain = new System.Windows.Forms.TextBox();
            this.tabPageDirect = new System.Windows.Forms.TabPage();
            this.panel4 = new System.Windows.Forms.Panel();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.txtDirectIp = new System.Windows.Forms.TextBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.txtDirectDomain = new System.Windows.Forms.TextBox();
            this.tabPageBlock = new System.Windows.Forms.TabPage();
            this.panel3 = new System.Windows.Forms.Panel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.txtBlockIp = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtBlockDomain = new System.Windows.Forms.TextBox();
            this.tabPageRuleList = new System.Windows.Forms.TabPage();
            this.lvRoutings = new v2rayN.Base.ListViewFlickerFree();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.cmsLv.SuspendLayout();
            this.menuServer.SuspendLayout();
            this.tabNormal.SuspendLayout();
            this.tabPageProxy.SuspendLayout();
            this.panel5.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.tabPageDirect.SuspendLayout();
            this.panel4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.tabPageBlock.SuspendLayout();
            this.panel3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPageRuleList.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnClose
            // 
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // panel2
            // 
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Controls.Add(this.labRoutingTips);
            this.panel2.Controls.Add(this.btnClose);
            this.panel2.Controls.Add(this.btnOK);
            this.panel2.Name = "panel2";
            // 
            // labRoutingTips
            // 
            resources.ApplyResources(this.labRoutingTips, "labRoutingTips");
            this.labRoutingTips.ForeColor = System.Drawing.Color.Brown;
            this.labRoutingTips.Name = "labRoutingTips";
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
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Controls.Add(this.chkenableRoutingAdvanced);
            this.panel1.Controls.Add(this.linkLabelRoutingDoc);
            this.panel1.Controls.Add(this.cmbdomainStrategy);
            this.panel1.Name = "panel1";
            // 
            // chkenableRoutingAdvanced
            // 
            resources.ApplyResources(this.chkenableRoutingAdvanced, "chkenableRoutingAdvanced");
            this.chkenableRoutingAdvanced.Name = "chkenableRoutingAdvanced";
            this.chkenableRoutingAdvanced.UseVisualStyleBackColor = true;
            this.chkenableRoutingAdvanced.CheckedChanged += new System.EventHandler(this.chkenableRoutingAdvanced_CheckedChanged_1);
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
            resources.ApplyResources(this.cmbdomainStrategy, "cmbdomainStrategy");
            this.cmbdomainStrategy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbdomainStrategy.FormattingEnabled = true;
            this.cmbdomainStrategy.Items.AddRange(new object[] {
            resources.GetString("cmbdomainStrategy.Items"),
            resources.GetString("cmbdomainStrategy.Items1"),
            resources.GetString("cmbdomainStrategy.Items2")});
            this.cmbdomainStrategy.Name = "cmbdomainStrategy";
            // 
            // cmsLv
            // 
            resources.ApplyResources(this.cmsLv, "cmsLv");
            this.cmsLv.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsLv.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuAdd,
            this.menuRemove,
            this.menuSelectAll,
            this.menuSetDefaultRouting});
            this.cmsLv.Name = "cmsLv";
            this.cmsLv.OwnerItem = this.MenuItem1;
            // 
            // menuAdd
            // 
            resources.ApplyResources(this.menuAdd, "menuAdd");
            this.menuAdd.Name = "menuAdd";
            this.menuAdd.Click += new System.EventHandler(this.menuAdd_Click);
            // 
            // menuRemove
            // 
            resources.ApplyResources(this.menuRemove, "menuRemove");
            this.menuRemove.Name = "menuRemove";
            this.menuRemove.Click += new System.EventHandler(this.menuRemove_Click);
            // 
            // menuSelectAll
            // 
            resources.ApplyResources(this.menuSelectAll, "menuSelectAll");
            this.menuSelectAll.Name = "menuSelectAll";
            this.menuSelectAll.Click += new System.EventHandler(this.menuSelectAll_Click);
            // 
            // menuSetDefaultRouting
            // 
            resources.ApplyResources(this.menuSetDefaultRouting, "menuSetDefaultRouting");
            this.menuSetDefaultRouting.Name = "menuSetDefaultRouting";
            this.menuSetDefaultRouting.Click += new System.EventHandler(this.menuSetDefaultRouting_Click);
            // 
            // MenuItem1
            // 
            resources.ApplyResources(this.MenuItem1, "MenuItem1");
            this.MenuItem1.DropDown = this.cmsLv;
            this.MenuItem1.Name = "MenuItem1";
            // 
            // menuServer
            // 
            resources.ApplyResources(this.menuServer, "menuServer");
            this.menuServer.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuItem1});
            this.menuServer.Name = "menuServer";
            // 
            // tabNormal
            // 
            resources.ApplyResources(this.tabNormal, "tabNormal");
            this.tabNormal.Controls.Add(this.tabPageProxy);
            this.tabNormal.Controls.Add(this.tabPageDirect);
            this.tabNormal.Controls.Add(this.tabPageBlock);
            this.tabNormal.Controls.Add(this.tabPageRuleList);
            this.tabNormal.Name = "tabNormal";
            this.tabNormal.SelectedIndex = 0;
            this.tabNormal.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.tabNormal_Selecting);
            // 
            // tabPageProxy
            // 
            resources.ApplyResources(this.tabPageProxy, "tabPageProxy");
            this.tabPageProxy.Controls.Add(this.panel5);
            this.tabPageProxy.Name = "tabPageProxy";
            this.tabPageProxy.UseVisualStyleBackColor = true;
            // 
            // panel5
            // 
            resources.ApplyResources(this.panel5, "panel5");
            this.panel5.Controls.Add(this.groupBox5);
            this.panel5.Controls.Add(this.groupBox6);
            this.panel5.Name = "panel5";
            // 
            // groupBox5
            // 
            resources.ApplyResources(this.groupBox5, "groupBox5");
            this.groupBox5.Controls.Add(this.txtProxyIp);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.TabStop = false;
            // 
            // txtProxyIp
            // 
            resources.ApplyResources(this.txtProxyIp, "txtProxyIp");
            this.txtProxyIp.Name = "txtProxyIp";
            // 
            // groupBox6
            // 
            resources.ApplyResources(this.groupBox6, "groupBox6");
            this.groupBox6.Controls.Add(this.txtProxyDomain);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.TabStop = false;
            // 
            // txtProxyDomain
            // 
            resources.ApplyResources(this.txtProxyDomain, "txtProxyDomain");
            this.txtProxyDomain.Name = "txtProxyDomain";
            // 
            // tabPageDirect
            // 
            resources.ApplyResources(this.tabPageDirect, "tabPageDirect");
            this.tabPageDirect.Controls.Add(this.panel4);
            this.tabPageDirect.Name = "tabPageDirect";
            this.tabPageDirect.UseVisualStyleBackColor = true;
            // 
            // panel4
            // 
            resources.ApplyResources(this.panel4, "panel4");
            this.panel4.Controls.Add(this.groupBox3);
            this.panel4.Controls.Add(this.groupBox4);
            this.panel4.Name = "panel4";
            // 
            // groupBox3
            // 
            resources.ApplyResources(this.groupBox3, "groupBox3");
            this.groupBox3.Controls.Add(this.txtDirectIp);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.TabStop = false;
            // 
            // txtDirectIp
            // 
            resources.ApplyResources(this.txtDirectIp, "txtDirectIp");
            this.txtDirectIp.Name = "txtDirectIp";
            // 
            // groupBox4
            // 
            resources.ApplyResources(this.groupBox4, "groupBox4");
            this.groupBox4.Controls.Add(this.txtDirectDomain);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.TabStop = false;
            // 
            // txtDirectDomain
            // 
            resources.ApplyResources(this.txtDirectDomain, "txtDirectDomain");
            this.txtDirectDomain.Name = "txtDirectDomain";
            // 
            // tabPageBlock
            // 
            resources.ApplyResources(this.tabPageBlock, "tabPageBlock");
            this.tabPageBlock.Controls.Add(this.panel3);
            this.tabPageBlock.Name = "tabPageBlock";
            this.tabPageBlock.UseVisualStyleBackColor = true;
            // 
            // panel3
            // 
            resources.ApplyResources(this.panel3, "panel3");
            this.panel3.Controls.Add(this.groupBox2);
            this.panel3.Controls.Add(this.groupBox1);
            this.panel3.Name = "panel3";
            // 
            // groupBox2
            // 
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Controls.Add(this.txtBlockIp);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // txtBlockIp
            // 
            resources.ApplyResources(this.txtBlockIp, "txtBlockIp");
            this.txtBlockIp.Name = "txtBlockIp";
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.txtBlockDomain);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // txtBlockDomain
            // 
            resources.ApplyResources(this.txtBlockDomain, "txtBlockDomain");
            this.txtBlockDomain.Name = "txtBlockDomain";
            // 
            // tabPageRuleList
            // 
            resources.ApplyResources(this.tabPageRuleList, "tabPageRuleList");
            this.tabPageRuleList.Controls.Add(this.lvRoutings);
            this.tabPageRuleList.Name = "tabPageRuleList";
            this.tabPageRuleList.UseVisualStyleBackColor = true;
            // 
            // lvRoutings
            // 
            resources.ApplyResources(this.lvRoutings, "lvRoutings");
            this.lvRoutings.ContextMenuStrip = this.cmsLv;
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
            // 
            // RoutingSettingForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.tabNormal);
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
            this.menuServer.ResumeLayout(false);
            this.menuServer.PerformLayout();
            this.tabNormal.ResumeLayout(false);
            this.tabPageProxy.ResumeLayout(false);
            this.panel5.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.tabPageDirect.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.tabPageBlock.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabPageRuleList.ResumeLayout(false);
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
        private System.Windows.Forms.ContextMenuStrip cmsLv;
        private System.Windows.Forms.ToolStripMenuItem menuRemove;
        private System.Windows.Forms.ToolStripMenuItem menuSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuAdd;
        private System.Windows.Forms.MenuStrip menuServer;
        private System.Windows.Forms.ToolStripMenuItem MenuItem1;
        private System.Windows.Forms.ToolStripMenuItem menuSetDefaultRouting;
        private System.Windows.Forms.TabControl tabNormal;
        private System.Windows.Forms.TabPage tabPageProxy;
        private System.Windows.Forms.TabPage tabPageDirect;
        private System.Windows.Forms.TabPage tabPageBlock;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.TextBox txtProxyIp;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.TextBox txtProxyDomain;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox txtDirectIp;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox txtDirectDomain;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtBlockIp;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtBlockDomain;
        private System.Windows.Forms.TabPage tabPageRuleList;
        private Base.ListViewFlickerFree lvRoutings;
        private System.Windows.Forms.Label labRoutingTips;
        private System.Windows.Forms.CheckBox chkenableRoutingAdvanced;
    }
}