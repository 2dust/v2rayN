using System;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class BaseForm : Form
    {
        protected static Config config;
        protected static System.Drawing.Icon icon;

        public BaseForm()
        {
            InitializeComponent();
            this.Font= new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.Name, System.Drawing.SystemFonts.DefaultFont.Size);
            LoadCustomIcon();
        }

        private void LoadCustomIcon()
        {
            try
            {
                if (icon == null)
                {
                    string file = Utils.GetPath(Global.CustomIconName);
                    if (!System.IO.File.Exists(file))
                    {
                        return;
                    }
                    icon = new System.Drawing.Icon(file);
                }
                this.Icon = icon;
            }
            catch (Exception e)
            {
                Utils.SaveLog($"Loading custom icon failed: {e.Message}");
            }
        }

    }
}
