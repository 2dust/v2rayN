using System;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class BaseForm : Form
    {
        protected static Config config;
        protected static System.Drawing.Icon icon;
        protected static System.Drawing.Icon globleAgentIcon;

        public BaseForm()
        {
            InitializeComponent();
            LoadCustomIcon();
            LoadGlobleAgentCustomIcon();
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

        //设置托盘图标
        private void LoadGlobleAgentCustomIcon()
        {
            //原图标进行颜色变化
            System.Drawing.Bitmap gImage = this.Icon.ToBitmap();
            for (int x = 0; x < gImage.Width; x++)
            {
                for (int y = 0; y < gImage.Height; y++)
                {
                    System.Drawing.Color pixelColor = gImage.GetPixel(x, y);

                    if ((0 == pixelColor.R
                       && 0 == pixelColor.G
                       && 0 == pixelColor.B)
                       || (253 == pixelColor.R
                       && 253 == pixelColor.G
                       && 253 == pixelColor.B))
                    {
                        continue;
                    }
                    //粉色
                    gImage.SetPixel(x, y, System.Drawing.Color.FromArgb(255, 174, 210));
                }
            }
            globleAgentIcon = System.Drawing.Icon.FromHandle(gImage.GetHicon());
        }
    }
}
