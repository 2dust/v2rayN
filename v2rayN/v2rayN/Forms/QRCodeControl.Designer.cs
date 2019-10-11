namespace v2rayN.Forms
{
    partial class QRCodeControl
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
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(QRCodeControl));
            this.txtUrl = new System.Windows.Forms.TextBox();
            this.picQRCode = new System.Windows.Forms.PictureBox();
            this.chkShow = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.picQRCode)).BeginInit();
            this.SuspendLayout();
            // 
            // txtUrl
            // 
            resources.ApplyResources(this.txtUrl, "txtUrl");
            this.txtUrl.Name = "txtUrl";
            this.txtUrl.ReadOnly = true;
            // 
            // picQRCode
            // 
            resources.ApplyResources(this.picQRCode, "picQRCode");
            this.picQRCode.Name = "picQRCode";
            this.picQRCode.TabStop = false;
            // 
            // chkShow
            // 
            resources.ApplyResources(this.chkShow, "chkShow");
            this.chkShow.Name = "chkShow";
            this.chkShow.UseVisualStyleBackColor = true;
            this.chkShow.CheckedChanged += new System.EventHandler(this.chkShow_CheckedChanged);
            // 
            // QRCodeControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.picQRCode);
            this.Controls.Add(this.txtUrl);
            this.Controls.Add(this.chkShow);
            this.Name = "QRCodeControl";
            this.Load += new System.EventHandler(this.QRCodeControl_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picQRCode)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtUrl;
        private System.Windows.Forms.PictureBox picQRCode;
        private System.Windows.Forms.CheckBox chkShow;
    }
}
