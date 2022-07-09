namespace v2rayN.Forms
{
    partial class RoutingRuleSettingDetailsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RoutingRuleSettingDetailsForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.clbInboundTag = new System.Windows.Forms.CheckedListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.clbProtocol = new System.Windows.Forms.CheckedListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.labRoutingTips = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.cmbOutboundTag = new System.Windows.Forms.ComboBox();
            this.panel4 = new System.Windows.Forms.Panel();
            this.chkAutoSort = new System.Windows.Forms.CheckBox();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.txtIP = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtDomain = new System.Windows.Forms.TextBox();
            this.linkRuleobjectDoc = new System.Windows.Forms.LinkLabel();
            this.panel3.SuspendLayout();
            this.panel4.SuspendLayout();
            this.panel2.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.linkRuleobjectDoc);
            this.panel3.Controls.Add(this.chkEnabled);
            this.panel3.Controls.Add(this.clbInboundTag);
            this.panel3.Controls.Add(this.label2);
            this.panel3.Controls.Add(this.clbProtocol);
            this.panel3.Controls.Add(this.label3);
            this.panel3.Controls.Add(this.txtPort);
            this.panel3.Controls.Add(this.label1);
            this.panel3.Controls.Add(this.labRoutingTips);
            this.panel3.Controls.Add(this.label4);
            this.panel3.Controls.Add(this.cmbOutboundTag);
            resources.ApplyResources(this.panel3, "panel3");
            this.panel3.Name = "panel3";
            // 
            // chkEnabled
            // 
            resources.ApplyResources(this.chkEnabled, "chkEnabled");
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.UseVisualStyleBackColor = true;
            // 
            // clbInboundTag
            // 
            this.clbInboundTag.CheckOnClick = true;
            resources.ApplyResources(this.clbInboundTag, "clbInboundTag");
            this.clbInboundTag.FormattingEnabled = true;
            this.clbInboundTag.Items.AddRange(new object[] {
            resources.GetString("clbInboundTag.Items"),
            resources.GetString("clbInboundTag.Items1"),
            resources.GetString("clbInboundTag.Items2"),
            resources.GetString("clbInboundTag.Items3")});
            this.clbInboundTag.MultiColumn = true;
            this.clbInboundTag.Name = "clbInboundTag";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // clbProtocol
            // 
            this.clbProtocol.CheckOnClick = true;
            resources.ApplyResources(this.clbProtocol, "clbProtocol");
            this.clbProtocol.FormattingEnabled = true;
            this.clbProtocol.Items.AddRange(new object[] {
            resources.GetString("clbProtocol.Items"),
            resources.GetString("clbProtocol.Items1"),
            resources.GetString("clbProtocol.Items2")});
            this.clbProtocol.MultiColumn = true;
            this.clbProtocol.Name = "clbProtocol";
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // labRoutingTips
            // 
            this.labRoutingTips.ForeColor = System.Drawing.Color.Brown;
            resources.ApplyResources(this.labRoutingTips, "labRoutingTips");
            this.labRoutingTips.Name = "labRoutingTips";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // cmbOutboundTag
            // 
            this.cmbOutboundTag.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOutboundTag.FormattingEnabled = true;
            this.cmbOutboundTag.Items.AddRange(new object[] {
            resources.GetString("cmbOutboundTag.Items"),
            resources.GetString("cmbOutboundTag.Items1"),
            resources.GetString("cmbOutboundTag.Items2")});
            resources.ApplyResources(this.cmbOutboundTag, "cmbOutboundTag");
            this.cmbOutboundTag.Name = "cmbOutboundTag";
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.chkAutoSort);
            this.panel4.Controls.Add(this.btnClose);
            this.panel4.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panel4, "panel4");
            this.panel4.Name = "panel4";
            // 
            // chkAutoSort
            // 
            resources.ApplyResources(this.chkAutoSort, "chkAutoSort");
            this.chkAutoSort.Name = "chkAutoSort";
            this.chkAutoSort.UseVisualStyleBackColor = true;
            // 
            // btnClose
            // 
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.btnClose, "btnClose");
            this.btnClose.Name = "btnClose";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.groupBox2);
            this.panel2.Controls.Add(this.groupBox1);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.txtIP);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // txtIP
            // 
            resources.ApplyResources(this.txtIP, "txtIP");
            this.txtIP.Name = "txtIP";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.txtDomain);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // txtDomain
            // 
            resources.ApplyResources(this.txtDomain, "txtDomain");
            this.txtDomain.Name = "txtDomain";
            // 
            // linkRuleobjectDoc
            // 
            resources.ApplyResources(this.linkRuleobjectDoc, "linkRuleobjectDoc");
            this.linkRuleobjectDoc.Name = "linkRuleobjectDoc";
            this.linkRuleobjectDoc.TabStop = true;
            this.linkRuleobjectDoc.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkRuleobjectDoc_LinkClicked);
            // 
            // RoutingRuleSettingDetailsForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel1);
            this.Name = "RoutingRuleSettingDetailsForm";
            this.Load += new System.EventHandler(this.RoutingRuleSettingDetailsForm_Load);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cmbOutboundTag;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtDomain;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtIP;
        private System.Windows.Forms.Label labRoutingTips;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckedListBox clbProtocol;
        private System.Windows.Forms.CheckedListBox clbInboundTag;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkEnabled;
        private System.Windows.Forms.CheckBox chkAutoSort;
        private System.Windows.Forms.LinkLabel linkRuleobjectDoc;
    }
}