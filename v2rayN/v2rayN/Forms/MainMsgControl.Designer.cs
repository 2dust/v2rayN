
namespace v2rayN.Forms
{
    partial class MainMsgControl
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainMsgControl));
            this.txtMsgBox = new System.Windows.Forms.TextBox();
            this.cmsMsgBox = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuMsgBoxSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMsgBoxCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMsgBoxCopyAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMsgBoxClear = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMsgBoxAddRoutingRule = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMsgBoxFilter = new System.Windows.Forms.ToolStripMenuItem();
            this.gbMsgTitle = new System.Windows.Forms.GroupBox();
            this.ssMain = new System.Windows.Forms.StatusStrip();
            this.toolSslInboundInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslRoutingRule = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslServerSpeed = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolSslBlank4 = new System.Windows.Forms.ToolStripStatusLabel();
            this.cmsMsgBox.SuspendLayout();
            this.gbMsgTitle.SuspendLayout();
            this.ssMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtMsgBox
            // 
            resources.ApplyResources(this.txtMsgBox, "txtMsgBox");
            this.txtMsgBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(49)))), ((int)(((byte)(52)))));
            this.txtMsgBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtMsgBox.ContextMenuStrip = this.cmsMsgBox;
            this.txtMsgBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(226)))), ((int)(((byte)(228)))));
            this.txtMsgBox.Name = "txtMsgBox";
            this.txtMsgBox.ReadOnly = true;
            // 
            // cmsMsgBox
            // 
            resources.ApplyResources(this.cmsMsgBox, "cmsMsgBox");
            this.cmsMsgBox.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuMsgBoxSelectAll,
            this.menuMsgBoxCopy,
            this.menuMsgBoxCopyAll,
            this.menuMsgBoxClear,
            this.menuMsgBoxAddRoutingRule,
            this.menuMsgBoxFilter});
            this.cmsMsgBox.Name = "cmsMsgBox";
            // 
            // menuMsgBoxSelectAll
            // 
            resources.ApplyResources(this.menuMsgBoxSelectAll, "menuMsgBoxSelectAll");
            this.menuMsgBoxSelectAll.Name = "menuMsgBoxSelectAll";
            this.menuMsgBoxSelectAll.Click += new System.EventHandler(this.menuMsgBoxSelectAll_Click);
            // 
            // menuMsgBoxCopy
            // 
            resources.ApplyResources(this.menuMsgBoxCopy, "menuMsgBoxCopy");
            this.menuMsgBoxCopy.Name = "menuMsgBoxCopy";
            this.menuMsgBoxCopy.Click += new System.EventHandler(this.menuMsgBoxCopy_Click);
            // 
            // menuMsgBoxCopyAll
            // 
            resources.ApplyResources(this.menuMsgBoxCopyAll, "menuMsgBoxCopyAll");
            this.menuMsgBoxCopyAll.Name = "menuMsgBoxCopyAll";
            this.menuMsgBoxCopyAll.Click += new System.EventHandler(this.menuMsgBoxCopyAll_Click);
            // 
            // menuMsgBoxClear
            // 
            resources.ApplyResources(this.menuMsgBoxClear, "menuMsgBoxClear");
            this.menuMsgBoxClear.Name = "menuMsgBoxClear";
            this.menuMsgBoxClear.Click += new System.EventHandler(this.menuMsgBoxClear_Click);
            // 
            // menuMsgBoxAddRoutingRule
            // 
            resources.ApplyResources(this.menuMsgBoxAddRoutingRule, "menuMsgBoxAddRoutingRule");
            this.menuMsgBoxAddRoutingRule.Name = "menuMsgBoxAddRoutingRule";
            this.menuMsgBoxAddRoutingRule.Click += new System.EventHandler(this.menuMsgBoxAddRoutingRule_Click);
            // 
            // menuMsgBoxFilter
            // 
            resources.ApplyResources(this.menuMsgBoxFilter, "menuMsgBoxFilter");
            this.menuMsgBoxFilter.Name = "menuMsgBoxFilter";
            this.menuMsgBoxFilter.Click += new System.EventHandler(this.menuMsgBoxFilter_Click);
            // 
            // gbMsgTitle
            // 
            resources.ApplyResources(this.gbMsgTitle, "gbMsgTitle");
            this.gbMsgTitle.Controls.Add(this.txtMsgBox);
            this.gbMsgTitle.Controls.Add(this.ssMain);
            this.gbMsgTitle.Name = "gbMsgTitle";
            this.gbMsgTitle.TabStop = false;
            // 
            // ssMain
            // 
            resources.ApplyResources(this.ssMain, "ssMain");
            this.ssMain.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.ssMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolSslInboundInfo,
            this.toolSslBlank1,
            this.toolSslRoutingRule,
            this.toolSslBlank2,
            this.toolSslServerSpeed,
            this.toolSslBlank4});
            this.ssMain.Name = "ssMain";
            // 
            // toolSslInboundInfo
            // 
            resources.ApplyResources(this.toolSslInboundInfo, "toolSslInboundInfo");
            this.toolSslInboundInfo.Name = "toolSslInboundInfo";
            // 
            // toolSslBlank1
            // 
            resources.ApplyResources(this.toolSslBlank1, "toolSslBlank1");
            this.toolSslBlank1.Name = "toolSslBlank1";
            this.toolSslBlank1.Spring = true;
            // 
            // toolSslRoutingRule
            // 
            resources.ApplyResources(this.toolSslRoutingRule, "toolSslRoutingRule");
            this.toolSslRoutingRule.Name = "toolSslRoutingRule";
            // 
            // toolSslBlank2
            // 
            resources.ApplyResources(this.toolSslBlank2, "toolSslBlank2");
            this.toolSslBlank2.Name = "toolSslBlank2";
            this.toolSslBlank2.Spring = true;
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
            // MainMsgControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gbMsgTitle);
            this.Name = "MainMsgControl";
            this.Load += new System.EventHandler(this.MainMsgControl_Load);
            this.cmsMsgBox.ResumeLayout(false);
            this.gbMsgTitle.ResumeLayout(false);
            this.gbMsgTitle.PerformLayout();
            this.ssMain.ResumeLayout(false);
            this.ssMain.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox txtMsgBox;
        private System.Windows.Forms.ContextMenuStrip cmsMsgBox;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxSelectAll;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxCopy;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxCopyAll;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxClear;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxAddRoutingRule;
        private System.Windows.Forms.ToolStripMenuItem menuMsgBoxFilter;
        private System.Windows.Forms.GroupBox gbMsgTitle;
        private System.Windows.Forms.StatusStrip ssMain;
        private System.Windows.Forms.ToolStripStatusLabel toolSslInboundInfo;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank1;
        private System.Windows.Forms.ToolStripStatusLabel toolSslRoutingRule;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank2;
        private System.Windows.Forms.ToolStripStatusLabel toolSslServerSpeed;
        private System.Windows.Forms.ToolStripStatusLabel toolSslBlank4;
    }
}
