using System.Windows.Forms;
using v2rayN.Handler;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class QRCodeControl : UserControl
    {
        public QRCodeControl()
        {
            InitializeComponent();
        }
        private void QRCodeControl_Load(object sender, System.EventArgs e)
        {
            txtUrl.MouseUp += txtUrl_MouseUp;      
        }

        void txtUrl_MouseUp(object sender, MouseEventArgs e)
        {
            txtUrl.SelectAll();
        }

        public void showQRCode(int Index, Config config)
        {
            if (Index >= 0)
            {
                string url = ConfigHandler.GetVmessQRCode(config, Index);
                if (Utils.IsNullOrEmpty(url))
                {
                    picQRCode.Image = null;
                    txtUrl.Text = string.Empty;
                    return;
                }
                txtUrl.Text = url;
                picQRCode.Image = QRCodeHelper.GetQRCode(url);                
            }
        }
    }
}
