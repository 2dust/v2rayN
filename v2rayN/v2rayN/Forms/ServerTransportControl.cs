using System;
using System.Collections.Generic;
using System.Windows.Forms;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Forms
{
    public partial class ServerTransportControl : UserControl
    {
        public bool AllowXtls { get; set; }
        private VmessItem vmessItem = null;

        public ServerTransportControl()
        {
            InitializeComponent();
        }
        private void ServerTransportControl_Load(object sender, EventArgs e)
        {
        }

        private void Init(VmessItem item)
        {
            vmessItem = item;

            cmbNetwork.Items.AddRange(Global.networks.ToArray());

            cmbStreamSecurity.Items.Clear();
            cmbStreamSecurity.Items.Add(string.Empty);
            cmbStreamSecurity.Items.Add(Global.StreamSecurity);
            if (AllowXtls)
            {
                cmbStreamSecurity.Items.Add(Global.StreamSecurityX);
            }
        }

        public void BindingServer(VmessItem item)
        {
            Init(item);

            cmbNetwork.Text = vmessItem.network;
            cmbHeaderType.Text = vmessItem.headerType;
            txtRequestHost.Text = vmessItem.requestHost;
            txtPath.Text = vmessItem.path;
            cmbStreamSecurity.Text = vmessItem.streamSecurity;
            cmbAllowInsecure.Text = vmessItem.allowInsecure;
            txtSNI.Text = vmessItem.sni;

            if (vmessItem.alpn != null)
            {
                for (int i = 0; i < clbAlpn.Items.Count; i++)
                {
                    if (vmessItem.alpn.Contains(clbAlpn.Items[i].ToString()))
                    {
                        clbAlpn.SetItemChecked(i, true);
                    }
                }
            }
        }

        public void ClearServer(VmessItem item)
        {
            Init(item);

            cmbNetwork.Text = Global.DefaultNetwork;
            cmbHeaderType.Text = Global.None;
            txtRequestHost.Text = "";
            cmbStreamSecurity.Text = "";
            cmbAllowInsecure.Text = "";
            txtPath.Text = "";
            txtSNI.Text = "";
            for (int i = 0; i < clbAlpn.Items.Count; i++)
            {
                clbAlpn.SetItemChecked(i, false);
            }
        }

        public void EndBindingServer()
        {
            string network = cmbNetwork.Text;
            string headerType = cmbHeaderType.Text;
            string requestHost = txtRequestHost.Text;
            string path = txtPath.Text;
            string streamSecurity = cmbStreamSecurity.Text;
            string allowInsecure = cmbAllowInsecure.Text;
            string sni = txtSNI.Text;

            vmessItem.network = network;
            vmessItem.headerType = headerType;
            vmessItem.requestHost = requestHost.Replace(" ", "");
            vmessItem.path = path.Replace(" ", "");
            vmessItem.streamSecurity = streamSecurity;
            vmessItem.allowInsecure = allowInsecure;
            vmessItem.sni = sni;

            var alpn = new List<string>();
            for (int i = 0; i < clbAlpn.Items.Count; i++)
            {
                if (clbAlpn.GetItemChecked(i))
                {
                    alpn.Add(clbAlpn.Items[i].ToString());
                }
            }
            vmessItem.alpn = alpn;
        }

        private void cmbNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetHeaderType();
            SetTips();
        }

        private void SetHeaderType()
        {
            cmbHeaderType.Items.Clear();

            string network = cmbNetwork.Text;
            if (Utils.IsNullOrEmpty(network))
            {
                cmbHeaderType.Items.Add(Global.None);
                return;
            }

            if (network.Equals(Global.DefaultNetwork))
            {
                cmbHeaderType.Items.Add(Global.None);
                cmbHeaderType.Items.Add(Global.TcpHeaderHttp);
            }
            else if (network.Equals("kcp") || network.Equals("quic"))
            {
                cmbHeaderType.Items.Add(Global.None);
                cmbHeaderType.Items.AddRange(Global.kcpHeaderTypes.ToArray());
            }
            else if (network.Equals("grpc"))
            {
                cmbHeaderType.Items.Add(Global.GrpcgunMode);
                cmbHeaderType.Items.Add(Global.GrpcmultiMode);
            }
            else
            {
                cmbHeaderType.Items.Add(Global.None);
            }
            cmbHeaderType.SelectedIndex = 0;
        }

        private void SetTips()
        {
            string network = cmbNetwork.Text;
            if (Utils.IsNullOrEmpty(network))
            {
                network = Global.DefaultNetwork;
            }
            labHeaderType.Visible = true;
            tipRequestHost.Text =
            tipPath.Text =
            tipHeaderType.Text = string.Empty;

            if (network.Equals(Global.DefaultNetwork))
            {
                tipRequestHost.Text = ResUI.TransportRequestHostTip1;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip1;
            }
            else if (network.Equals("kcp"))
            {
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip2;
                tipPath.Text = ResUI.TransportPathTip5;                
            }
            else if (network.Equals("ws"))
            {
                tipRequestHost.Text = ResUI.TransportRequestHostTip2;
                tipPath.Text = ResUI.TransportPathTip1;
            }
            else if (network.Equals("h2"))
            {
                tipRequestHost.Text = ResUI.TransportRequestHostTip3;
                tipPath.Text = ResUI.TransportPathTip2;
            }
            else if (network.Equals("quic"))
            {
                tipRequestHost.Text = ResUI.TransportRequestHostTip4;
                tipPath.Text = ResUI.TransportPathTip3;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip3;
            }
            else if (network.Equals("grpc"))
            {
                tipPath.Text = ResUI.TransportPathTip4;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip4;
                labHeaderType.Visible = false;
            }
        }

        private void cmbStreamSecurity_SelectedIndexChanged(object sender, EventArgs e)
        {
            string security = cmbStreamSecurity.Text;
            if (Utils.IsNullOrEmpty(security))
            {
                panTlsMore.Hide();
            }
            else
            {
                panTlsMore.Show();
            }
        }
    }
}