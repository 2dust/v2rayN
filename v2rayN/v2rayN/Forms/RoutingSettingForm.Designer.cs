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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RoutingSettingForm));
            this.btnClose = new System.Windows.Forms.Button();
            this.panCon = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnSetDefRountingRule = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.labRoutingTips = new System.Windows.Forms.Label();
            this.linkLabelRoutingDoc = new System.Windows.Forms.LinkLabel();
            this.cmbdomainStrategy = new System.Windows.Forms.ComboBox();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
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
            // panCon
            // 
            resources.ApplyResources(this.panCon, "panCon");
            this.panCon.Name = "panCon";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.btnAdd);
            this.panel2.Controls.Add(this.btnClose);
            this.panel2.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // btnAdd
            // 
            resources.ApplyResources(this.btnAdd, "btnAdd");
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnSetDefRountingRule
            // 
            resources.ApplyResources(this.btnSetDefRountingRule, "btnSetDefRountingRule");
            this.btnSetDefRountingRule.Name = "btnSetDefRountingRule";
            this.btnSetDefRountingRule.UseVisualStyleBackColor = true;
            this.btnSetDefRountingRule.Click += new System.EventHandler(this.btnSetDefRountingRule_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnSetDefRountingRule);
            this.panel1.Controls.Add(this.labRoutingTips);
            this.panel1.Controls.Add(this.linkLabelRoutingDoc);
            this.panel1.Controls.Add(this.cmbdomainStrategy);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // labRoutingTips
            // 
            this.labRoutingTips.ForeColor = System.Drawing.Color.Brown;
            resources.ApplyResources(this.labRoutingTips, "labRoutingTips");
            this.labRoutingTips.Name = "labRoutingTips";
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
            // RoutingSettingForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.panCon);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panel2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "RoutingSettingForm";
            this.Load += new System.EventHandler(this.RoutingSettingForm_Load);
            this.panel2.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Panel panCon;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label labRoutingTips;
        private System.Windows.Forms.LinkLabel linkLabelRoutingDoc;
        private System.Windows.Forms.ComboBox cmbdomainStrategy;
        private System.Windows.Forms.Button btnSetDefRountingRule;
    }
}