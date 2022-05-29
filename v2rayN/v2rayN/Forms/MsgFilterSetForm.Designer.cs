namespace v2rayN.Forms
{
    partial class MsgFilterSetForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MsgFilterSetForm));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnFilderProxy = new System.Windows.Forms.Button();
            this.btnFilterDirect = new System.Windows.Forms.Button();
            this.txtMsgFilter = new System.Windows.Forms.TextBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnFilderProxy);
            this.groupBox1.Controls.Add(this.btnFilterDirect);
            this.groupBox1.Controls.Add(this.txtMsgFilter);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // btnFilderProxy
            // 
            resources.ApplyResources(this.btnFilderProxy, "btnFilderProxy");
            this.btnFilderProxy.Name = "btnFilderProxy";
            this.btnFilderProxy.UseVisualStyleBackColor = true;
            this.btnFilderProxy.Click += new System.EventHandler(this.btnFilderProxy_Click);
            // 
            // btnFilterDirect
            // 
            resources.ApplyResources(this.btnFilterDirect, "btnFilterDirect");
            this.btnFilterDirect.Name = "btnFilterDirect";
            this.btnFilterDirect.UseVisualStyleBackColor = true;
            this.btnFilterDirect.Click += new System.EventHandler(this.btnFilterDirect_Click);
            // 
            // txtMsgFilter
            // 
            resources.ApplyResources(this.txtMsgFilter, "txtMsgFilter");
            this.txtMsgFilter.Name = "txtMsgFilter";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.btnClear);
            this.panel2.Controls.Add(this.btnClose);
            this.panel2.Controls.Add(this.btnOK);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
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
            // btnClear
            // 
            resources.ApplyResources(this.btnClear, "btnClear");
            this.btnClear.Name = "btnClear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // MsgFilterSetForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel2);
            this.Name = "MsgFilterSetForm";
            this.Load += new System.EventHandler(this.MsgFilterSetForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtMsgFilter;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnFilderProxy;
        private System.Windows.Forms.Button btnFilterDirect;
        private System.Windows.Forms.Button btnClear;
    }
}