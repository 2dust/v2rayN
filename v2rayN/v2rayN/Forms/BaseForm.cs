using System;
using System.Drawing;
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

        protected Icon GetNotifyIcon()
        {
            try
            {
                var index = config.sysAgentEnabled ? config.listenerType : 0;
                if (index <= 0)
                {
                    return this.Icon;
                }
                var color = (new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.Green })[index - 1];
                var text = index.ToString();

                var width = 128;
                var height = 128;
                //Create bitmap, kind of canvas
                Bitmap bitmap = new Bitmap(width, height);

                //var drawFont = new Font(FontFamily.Families[0], 64f, FontStyle.Bold);
                //var drawBrush = new SolidBrush(color);
                var pen = new Pen(color, 24);

                var graphics = Graphics.FromImage(bitmap);
                //graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
                graphics.DrawIcon(this.Icon, 0, 0);
                graphics.DrawEllipse(pen, new Rectangle(0, 0, width, height));
                //graphics.DrawString(text, drawFont, drawBrush, width / 4, height / 8);

                //To Save icon to disk
                bitmap.Save(Utils.GetPath("temp_icon.ico"), System.Drawing.Imaging.ImageFormat.Icon);

                Icon createdIcon = Icon.FromHandle(bitmap.GetHicon());

                //drawFont.Dispose();
                //drawBrush.Dispose();
                pen.Dispose();
                graphics.Dispose();
                bitmap.Dispose();

                return createdIcon;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return this.Icon;
            }
        }
    }
}
