namespace v2rayN.Forms
{
    partial class ServerTransportControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ServerTransportControl));
            this.gbTransport = new System.Windows.Forms.GroupBox();
            this.panTlsMore = new System.Windows.Forms.Panel();
            this.txtSNI = new System.Windows.Forms.TextBox();
            this.labSNI = new System.Windows.Forms.Label();
            this.labAllowInsecure = new System.Windows.Forms.Label();
            this.cmbAllowInsecure = new System.Windows.Forms.ComboBox();
            this.tipNetwork = new System.Windows.Forms.Label();
            this.txtPath = new System.Windows.Forms.TextBox();
            this.cmbNetwork = new System.Windows.Forms.ComboBox();
            this.labNetwork = new System.Windows.Forms.Label();
            this.labPath = new System.Windows.Forms.Label();
            this.tipPath = new System.Windows.Forms.Label();
            this.tipRequestHost = new System.Windows.Forms.Label();
            this.labStreamSecurity = new System.Windows.Forms.Label();
            this.cmbStreamSecurity = new System.Windows.Forms.ComboBox();
            this.tipHeaderType = new System.Windows.Forms.Label();
            this.txtRequestHost = new System.Windows.Forms.TextBox();
            this.labHeaderType = new System.Windows.Forms.Label();
            this.labRequestHost = new System.Windows.Forms.Label();
            this.cmbHeaderType = new System.Windows.Forms.ComboBox();
            this.gbTransport.SuspendLayout();
            this.panTlsMore.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbTransport
            // 
            this.gbTransport.Controls.Add(this.panTlsMore);
            this.gbTransport.Controls.Add(this.tipNetwork);
            this.gbTransport.Controls.Add(this.txtPath);
            this.gbTransport.Controls.Add(this.cmbNetwork);
            this.gbTransport.Controls.Add(this.labNetwork);
            this.gbTransport.Controls.Add(this.labPath);
            this.gbTransport.Controls.Add(this.tipPath);
            this.gbTransport.Controls.Add(this.tipRequestHost);
            this.gbTransport.Controls.Add(this.labStreamSecurity);
            this.gbTransport.Controls.Add(this.cmbStreamSecurity);
            this.gbTransport.Controls.Add(this.tipHeaderType);
            this.gbTransport.Controls.Add(this.txtRequestHost);
            this.gbTransport.Controls.Add(this.labHeaderType);
            this.gbTransport.Controls.Add(this.labRequestHost);
            this.gbTransport.Controls.Add(this.cmbHeaderType);
            resources.ApplyResources(this.gbTransport, "gbTransport");
            this.gbTransport.Name = "gbTransport";
            this.gbTransport.TabStop = false;
            // 
            // panTlsMore
            // 
            this.panTlsMore.Controls.Add(this.txtSNI);
            this.panTlsMore.Controls.Add(this.labSNI);
            this.panTlsMore.Controls.Add(this.labAllowInsecure);
            this.panTlsMore.Controls.Add(this.cmbAllowInsecure);
            resources.ApplyResources(this.panTlsMore, "panTlsMore");
            this.panTlsMore.Name = "panTlsMore";
            // 
            // txtSNI
            // 
            resources.ApplyResources(this.txtSNI, "txtSNI");
            this.txtSNI.Name = "txtSNI";
            // 
            // labSNI
            // 
            resources.ApplyResources(this.labSNI, "labSNI");
            this.labSNI.Name = "labSNI";
            // 
            // labAllowInsecure
            // 
            resources.ApplyResources(this.labAllowInsecure, "labAllowInsecure");
            this.labAllowInsecure.Name = "labAllowInsecure";
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
            // tipNetwork
            // 
            resources.ApplyResources(this.tipNetwork, "tipNetwork");
            this.tipNetwork.Name = "tipNetwork";
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
            resources.GetString("cmbNetwork.Items4"),
            resources.GetString("cmbNetwork.Items5")});
            resources.ApplyResources(this.cmbNetwork, "cmbNetwork");
            this.cmbNetwork.Name = "cmbNetwork";
            this.cmbNetwork.SelectedIndexChanged += new System.EventHandler(this.cmbNetwork_SelectedIndexChanged);
            // 
            // labNetwork
            // 
            resources.ApplyResources(this.labNetwork, "labNetwork");
            this.labNetwork.Name = "labNetwork";
            // 
            // labPath
            // 
            resources.ApplyResources(this.labPath, "labPath");
            this.labPath.Name = "labPath";
            // 
            // tipPath
            // 
            resources.ApplyResources(this.tipPath, "tipPath");
            this.tipPath.Name = "tipPath";
            // 
            // tipRequestHost
            // 
            resources.ApplyResources(this.tipRequestHost, "tipRequestHost");
            this.tipRequestHost.Name = "tipRequestHost";
            // 
            // labStreamSecurity
            // 
            resources.ApplyResources(this.labStreamSecurity, "labStreamSecurity");
            this.labStreamSecurity.Name = "labStreamSecurity";
            // 
            // cmbStreamSecurity
            // 
            this.cmbStreamSecurity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbStreamSecurity.FormattingEnabled = true;
            resources.ApplyResources(this.cmbStreamSecurity, "cmbStreamSecurity");
            this.cmbStreamSecurity.Name = "cmbStreamSecurity";
            this.cmbStreamSecurity.SelectedIndexChanged += new System.EventHandler(this.cmbStreamSecurity_SelectedIndexChanged);
            // 
            // tipHeaderType
            // 
            resources.ApplyResources(this.tipHeaderType, "tipHeaderType");
            this.tipHeaderType.Name = "tipHeaderType";
            // 
            // txtRequestHost
            // 
            resources.ApplyResources(this.txtRequestHost, "txtRequestHost");
            this.txtRequestHost.Name = "txtRequestHost";
            // 
            // labHeaderType
            // 
            resources.ApplyResources(this.labHeaderType, "labHeaderType");
            this.labHeaderType.Name = "labHeaderType";
            // 
            // labRequestHost
            // 
            resources.ApplyResources(this.labRequestHost, "labRequestHost");
            this.labRequestHost.Name = "labRequestHost";
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
            // ServerTransportControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gbTransport);
            this.Name = "ServerTransportControl";
            this.Load += new System.EventHandler(this.ServerTransportControl_Load);
            this.gbTransport.ResumeLayout(false);
            this.gbTransport.PerformLayout();
            this.panTlsMore.ResumeLayout(false);
            this.panTlsMore.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbTransport;
        private System.Windows.Forms.Panel panTlsMore;
        private System.Windows.Forms.TextBox txtSNI;
        private System.Windows.Forms.Label labSNI;
        private System.Windows.Forms.Label labAllowInsecure;
        private System.Windows.Forms.ComboBox cmbAllowInsecure;
        private System.Windows.Forms.Label tipNetwork;
        private System.Windows.Forms.TextBox txtPath;
        private System.Windows.Forms.ComboBox cmbNetwork;
        private System.Windows.Forms.Label labNetwork;
        private System.Windows.Forms.Label labPath;
        private System.Windows.Forms.Label tipPath;
        private System.Windows.Forms.Label tipRequestHost;
        private System.Windows.Forms.Label labStreamSecurity;
        private System.Windows.Forms.ComboBox cmbStreamSecurity;
        private System.Windows.Forms.Label tipHeaderType;
        private System.Windows.Forms.TextBox txtRequestHost;
        private System.Windows.Forms.Label labHeaderType;
        private System.Windows.Forms.Label labRequestHost;
        private System.Windows.Forms.ComboBox cmbHeaderType;
    }
}
