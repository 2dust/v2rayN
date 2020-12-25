namespace v2rayN.Forms
{
    partial class RoutingSettingControl
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

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RoutingSettingControl));
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnExpand = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.cmbroutingMode = new System.Windows.Forms.ComboBox();
            this.cmbOutboundTag = new System.Windows.Forms.ComboBox();
            this.btnRemove = new System.Windows.Forms.Button();
            this.txtUserRule = new System.Windows.Forms.TextBox();
            this.txtRemarks = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox2
            // 
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Controls.Add(this.btnExpand);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.cmbroutingMode);
            this.groupBox2.Controls.Add(this.cmbOutboundTag);
            this.groupBox2.Controls.Add(this.btnRemove);
            this.groupBox2.Controls.Add(this.txtUserRule);
            this.groupBox2.Controls.Add(this.txtRemarks);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // btnExpand
            // 
            resources.ApplyResources(this.btnExpand, "btnExpand");
            this.btnExpand.Name = "btnExpand";
            this.btnExpand.UseVisualStyleBackColor = true;
            this.btnExpand.Click += new System.EventHandler(this.btnExpand_Click);
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // cmbroutingMode
            // 
            resources.ApplyResources(this.cmbroutingMode, "cmbroutingMode");
            this.cmbroutingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbroutingMode.FormattingEnabled = true;
            this.cmbroutingMode.Items.AddRange(new object[] {
            resources.GetString("cmbroutingMode.Items"),
            resources.GetString("cmbroutingMode.Items1"),
            resources.GetString("cmbroutingMode.Items2"),
            resources.GetString("cmbroutingMode.Items3"),
            resources.GetString("cmbroutingMode.Items4")});
            this.cmbroutingMode.Name = "cmbroutingMode";
            // 
            // cmbOutboundTag
            // 
            resources.ApplyResources(this.cmbOutboundTag, "cmbOutboundTag");
            this.cmbOutboundTag.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOutboundTag.FormattingEnabled = true;
            this.cmbOutboundTag.Items.AddRange(new object[] {
            resources.GetString("cmbOutboundTag.Items"),
            resources.GetString("cmbOutboundTag.Items1"),
            resources.GetString("cmbOutboundTag.Items2")});
            this.cmbOutboundTag.Name = "cmbOutboundTag";
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // txtUserRule
            // 
            resources.ApplyResources(this.txtUserRule, "txtUserRule");
            this.txtUserRule.Name = "txtUserRule";
            this.txtUserRule.Leave += new System.EventHandler(this.txtRemarks_Leave);
            // 
            // txtRemarks
            // 
            resources.ApplyResources(this.txtRemarks, "txtRemarks");
            this.txtRemarks.Name = "txtRemarks";
            this.txtRemarks.Leave += new System.EventHandler(this.txtRemarks_Leave);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // RoutingSettingControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox2);
            this.Name = "RoutingSettingControl";
            this.Load += new System.EventHandler(this.RoutingSettingControl_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtUserRule;
        private System.Windows.Forms.TextBox txtRemarks;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.ComboBox cmbOutboundTag;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbroutingMode;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnExpand;
    }
}
