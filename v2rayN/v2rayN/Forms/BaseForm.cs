using System;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class BaseForm : Form
    {
        protected static Config config;

        public BaseForm()
        {
            InitializeComponent();
            LoadCustomIcon();
        }

        private void LoadCustomIcon()
        {
            try
            {
                string file = Utils.GetPath(Global.CustomIconName);
                if (System.IO.File.Exists(file))
                {
                    this.Icon = new System.Drawing.Icon(file);
                    return;
                }

                this.Icon = Properties.Resources.NotifyIcon1;
            }
            catch (Exception e)
            {
                Utils.SaveLog($"Loading custom icon failed: {e.Message}");
            }
        }

    }
}
