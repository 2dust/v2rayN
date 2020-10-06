namespace v2rayN.Forms
{
    partial class AddServer5Form
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddServer5Form));
            this.btnClose = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cmbFlow = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnGUID = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label24 = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.panTlsMore = new System.Windows.Forms.Panel();
            this.label21 = new System.Windows.Forms.Label();
            this.cmbAllowInsecure = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.txtPath = new System.Windows.Forms.TextBox();
            this.cmbNetwork = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.cmbStreamSecurity = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.txtRequestHost = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.cmbHeaderType = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.cmbSecurity = new System.Windows.Forms.ComboBox();
            this.txtRemarks = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.txtId = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnOK = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.menuServer = new System.Windows.Forms.MenuStrip();
            this.MenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuItemImportClient = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuItemImportServer = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.MenuItemImportClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.panTlsMore.SuspendLayout();
            this.panel2.SuspendLayout();
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
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cmbFlow);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.btnGUID);
            this.groupBox1.Controls.Add(this.label13);
            this.groupBox1.Controls.Add(this.groupBox2);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.cmbSecurity);
            this.groupBox1.Controls.Add(this.txtRemarks);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.txtId);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.txtPort);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.txtAddress);
            this.groupBox1.Controls.Add(this.label1);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // cmbFlow
            // 
            this.cmbFlow.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFlow.FormattingEnabled = true;
            this.cmbFlow.Items.AddRange(new object[] {
            resources.GetString("cmbFlow.Items"),
            resources.GetString("cmbFlow.Items1"),
            resources.GetString("cmbFlow.Items2")});
            resources.ApplyResources(this.cmbFlow, "cmbFlow");
            this.cmbFlow.Name = "cmbFlow";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // btnGUID
            // 
            resources.ApplyResources(this.btnGUID, "btnGUID");
            this.btnGUID.Name = "btnGUID";
            this.btnGUID.UseVisualStyleBackColor = true;
            this.btnGUID.Click += new System.EventHandler(this.btnGUID_Click);
            // 
            // label13
            // 
            resources.ApplyResources(this.label13, "label13");
            this.label13.Name = "label13";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label24);
            this.groupBox2.Controls.Add(this.label23);
            this.groupBox2.Controls.Add(this.panTlsMore);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.label20);
            this.groupBox2.Controls.Add(this.txtPath);
            this.groupBox2.Controls.Add(this.cmbNetwork);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.label19);
            this.groupBox2.Controls.Add(this.label18);
            this.groupBox2.Controls.Add(this.label17);
            this.groupBox2.Controls.Add(this.label16);
            this.groupBox2.Controls.Add(this.label14);
            this.groupBox2.Controls.Add(this.label15);
            this.groupBox2.Controls.Add(this.cmbStreamSecurity);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.txtRequestHost);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.cmbHeaderType);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // label24
            // 
            resources.ApplyResources(this.label24, "label24");
            this.label24.Name = "label24";
            // 
            // label23
            // 
            resources.ApplyResources(this.label23, "label23");
            this.label23.Name = "label23";
            // 
            // panTlsMore
            // 
            this.panTlsMore.Controls.Add(this.label21);
            this.panTlsMore.Controls.Add(this.cmbAllowInsecure);
            resources.ApplyResources(this.panTlsMore, "panTlsMore");
            this.panTlsMore.Name = "panTlsMore";
            // 
            // label21
            // 
            resources.ApplyResources(this.label21, "label21");
            this.label21.Name = "label21";
            // 
            // cmbAllowInsecure
            // 
            this.cmbAllowInsecure.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbAllowInsecure.FormattingEnabled = true;
            this.cmbAllowInsecure.Items.AddRange(new object[] {
            resources.GetString("cmbAllowInsecure.Items"),
            resources.GetString("cmbAllowInsecure.Items1"),
            resources.GetString("cmbAllowInsecure.Items2")});
            resources.ApplyResources(this.cmbAllowInsecure, "cmbAllowInsecure");
            this.cmbAllowInsecure.Name = "cmbAllowInsecure";
            // 
            // label9
            // 
            resources.ApplyResources(this.label9, "label9");
            this.label9.Name = "label9";
            // 
            // label20
            // 
            resources.ApplyResources(this.label20, "label20");
            this.label20.Name = "label20";
            // 
            // txtPath
            // 
            resources.ApplyResources(this.txtPath, "txtPath");
            this.txtPath.Name = "txtPath";
            // 
            // cmbNetwork
            // 
            this.cmbNetwork.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbNetwork.FormattingEnabled = true;
            this.cmbNetwork.Items.AddRange(new object[] {
            resources.GetString("cmbNetwork.Items"),
            resources.GetString("cmbNetwork.Items1"),
            resources.GetString("cmbNetwork.Items2"),
            resources.GetString("cmbNetwork.Items3"),
            resources.GetString("cmbNetwork.Items4")});
            resources.ApplyResources(this.cmbNetwork, "cmbNetwork");
            this.cmbNetwork.Name = "cmbNetwork";
            this.cmbNetwork.SelectedIndexChanged += new System.EventHandler(this.cmbNetwork_SelectedIndexChanged);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // label19
            // 
            resources.ApplyResources(this.label19, "label19");
            this.label19.Name = "label19";
            // 
            // label18
            // 
            resources.ApplyResources(this.label18, "label18");
            this.label18.Name = "label18";
            // 
            // label17
            // 
            resources.ApplyResources(this.label17, "label17");
            this.label17.Name = "label17";
            // 
            // label16
            // 
            resources.ApplyResources(this.label16, "label16");
            this.label16.Name = "label16";
            // 
            // label14
            // 
            resources.ApplyResources(this.label14, "label14");
            this.label14.Name = "label14";
            // 
            // label15
            // 
            resources.ApplyResources(this.label15, "label15");
            this.label15.Name = "label15";
            // 
            // cmbStreamSecurity
            // 
            this.cmbStreamSecurity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbStreamSecurity.FormattingEnabled = true;
            this.cmbStreamSecurity.Items.AddRange(new object[] {
            resources.GetString("cmbStreamSecurity.Items"),
            resources.GetString("cmbStreamSecurity.Items1"),
            resources.GetString("cmbStreamSecurity.Items2")});
            resources.ApplyResources(this.cmbStreamSecurity, "cmbStreamSecurity");
            this.cmbStreamSecurity.Name = "cmbStreamSecurity";
            this.cmbStreamSecurity.SelectedIndexChanged += new System.EventHandler(this.cmbStreamSecurity_SelectedIndexChanged);
            // 
            // label12
            // 
            resources.ApplyResources(this.label12, "label12");
            this.label12.Name = "label12";
            // 
            // txtRequestHost
            // 
            resources.ApplyResources(this.txtRequestHost, "txtRequestHost");
            this.txtRequestHost.Name = "txtRequestHost";
            // 
            // label11
            // 
            resources.ApplyResources(this.label11, "label11");
            this.label11.Name = "label11";
            // 
            // label10
            // 
            resources.ApplyResources(this.label10, "label10");
            this.label10.Name = "label10";
            // 
            // cmbHeaderType
            // 
            this.cmbHeaderType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHeaderType.FormattingEnabled = true;
            this.cmbHeaderType.Items.AddRange(new object[] {
            resources.GetString("cmbHeaderType.Items"),
            resources.GetString("cmbHeaderType.Items1"),
            resources.GetString("cmbHeaderType.Items2"),
            resources.GetString("cmbHeaderType.Items3"),
            resources.GetString("cmbHeaderType.Items4"),
            resources.GetString("cmbHeaderType.Items5"),
            resources.GetString("cmbHeaderType.Items6")});
            resources.ApplyResources(this.cmbHeaderType, "cmbHeaderType");
            this.cmbHeaderType.Name = "cmbHeaderType";
            // 
            // label8
            // 
            resources.ApplyResources(this.label8, "label8");
            this.label8.Name = "label8";
            // 
            // cmbSecurity
            // 
            this.cmbSecurity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.Simple;
            this.cmbSecurity.FormattingEnabled = true;
            this.cmbSecurity.Items.AddRange(new object[] {
            resources.GetString("cmbSecurity.Items")});
            resources.ApplyResources(this.cmbSecurity, "cmbSecurity");
            this.cmbSecurity.Name = "cmbSecurity";
            // 
            // txtRemarks
            // 
            resources.ApplyResources(this.txtRemarks, "txtRemarks");
            this.txtRemarks.Name = "txtRemarks";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // txtId
            // 
            resources.ApplyResources(this.txtId, "txtId");
            this.txtId.Name = "txtId";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // txtPort
            // 
            resources.ApplyResources(this.txtPort, "txtPort");
            this.txtPort.Name = "txtPort";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // txtAddress
            // 
            resources.ApplyResources(this.txtAddress, "txtAddress");
            this.txtAddress.Name = "txtAddress";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
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
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // menuServer
            // 
            this.menuServer.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuItem1});
            resources.ApplyResources(this.menuServer, "menuServer");
            this.menuServer.Name = "menuServer";
            // 
            // MenuItem1
            // 
            this.MenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuItemImportClient,
            this.MenuItemImportServer,
            this.toolStripSeparator1,
            this.MenuItemImportClipboard});
            this.MenuItem1.Name = "MenuItem1";
            resources.ApplyResources(this.MenuItem1, "MenuItem1");
            // 
            // MenuItemImportClient
            // 
            this.MenuItemImportClient.Name = "MenuItemImportClient";
            resources.ApplyResources(this.MenuItemImportClient, "MenuItemImportClient");
            this.MenuItemImportClient.Click += new System.EventHandler(this.MenuItemImportClient_Click);
            // 
            // MenuItemImportServer
            // 
            this.MenuItemImportServer.Name = "MenuItemImportServer";
            resources.ApplyResources(this.MenuItemImportServer, "MenuItemImportServer");
            this.MenuItemImportServer.Click += new System.EventHandler(this.MenuItemImportServer_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(this.toolStripSeparator1, "toolStripSeparator1");
            // 
            // MenuItemImportClipboard
            // 
            this.MenuItemImportClipboard.Name = "MenuItemImportClipboard";
            resources.ApplyResources(this.MenuItemImportClipboard, "MenuItemImportClipboard");
            this.MenuItemImportClipboard.Click += new System.EventHandler(this.MenuItemImportClipboard_Click);
            // 
            // AddServer5Form
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.menuServer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "AddServer5Form";
            this.Load += new System.EventHandler(this.AddServer5Form_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.panTlsMore.ResumeLayout(false);
            this.panTlsMore.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.menuServer.ResumeLayout(false);
            this.menuServer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox txtRemarks;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtId;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtAddress;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbSecurity;
        private System.Windows.Forms.ComboBox cmbNetwork;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TextBox txtRequestHost;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox cmbHeaderType;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.MenuStrip menuServer;
        private System.Windows.Forms.ToolStripMenuItem MenuItem1;
        private System.Windows.Forms.ToolStripMenuItem MenuItemImportClient;
        private System.Windows.Forms.ToolStripMenuItem MenuItemImportServer;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox cmbStreamSecurity;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem MenuItemImportClipboard;
        private System.Windows.Forms.Button btnGUID;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox txtPath;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.ComboBox cmbAllowInsecure;
        private System.Windows.Forms.Panel panTlsMore;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.ComboBox cmbFlow;
        private System.Windows.Forms.Label label4;
    }
}