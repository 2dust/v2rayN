using System;
using System.Drawing;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class MainFormHandler
    {
        private static MainFormHandler instance;

        //private DownloadHandle downloadHandle2;
        //private Config _config;
        //private V2rayHandler _v2rayHandler;
        //private List<int> _selecteds;
        //private Thread _workThread;
        //Action<int, string> _updateFunc;
        public static MainFormHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MainFormHandler();
                }
                return instance;
            }
        }

        public Icon GetNotifyIcon(Config config, Icon def)
        {
            try
            {
                Color color = ColorTranslator.FromHtml("#3399CC");
                int index = (int)config.listenerType;
                if (index > 0)
                {
                    color = (new Color[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange, Color.DarkSlateBlue, Color.RoyalBlue, Color.DarkSlateBlue, Color.RoyalBlue })[index - 1];
                    //color = ColorTranslator.FromHtml(new string[] { "#CC0066", "#CC6600", "#99CC99", "#666699" }[index - 1]);
                }

                int width = 128;
                int height = 128;

                Bitmap bitmap = new Bitmap(width, height);
                Graphics graphics = Graphics.FromImage(bitmap);
                SolidBrush drawBrush = new SolidBrush(color);

                graphics.FillEllipse(drawBrush, new Rectangle(0, 0, width, height));
                int zoom = 16;
                graphics.DrawImage(new Bitmap(Properties.Resources.notify, width - zoom, width - zoom), zoom / 2, zoom / 2);

                Icon createdIcon = Icon.FromHandle(bitmap.GetHicon());

                drawBrush.Dispose();
                graphics.Dispose();
                bitmap.Dispose();

                return createdIcon;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return def;
            }
        }

        public void Export2ClientConfig(int index, Config config)
        {
            //int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (config.vmess[index].configType != (int)EConfigType.Vmess)
            {
                UI.Show(UIRes.I18N("NonVmessService"));
                return;
            }

            SaveFileDialog fileDialog = new SaveFileDialog
            {
                Filter = "Config|*.json",
                FilterIndex = 2,
                RestoreDirectory = true
            };
            if (fileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string fileName = fileDialog.FileName;
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            Config configCopy = Utils.DeepCopy(config);
            configCopy.index = index;
            if (V2rayConfigHandler.Export2ClientConfig(configCopy, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(UIRes.I18N("SaveClientConfigurationIn"), fileName));
            }
        }

        public void Export2ServerConfig(int index, Config config)
        {
            //int index = GetLvSelectedIndex();
            if (index < 0)
            {
                return;
            }
            if (config.vmess[index].configType != (int)EConfigType.Vmess)
            {
                UI.Show(UIRes.I18N("NonVmessService"));
                return;
            }

            SaveFileDialog fileDialog = new SaveFileDialog
            {
                Filter = "Config|*.json",
                FilterIndex = 2,
                RestoreDirectory = true
            };
            if (fileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string fileName = fileDialog.FileName;
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            Config configCopy = Utils.DeepCopy(config);
            configCopy.index = index;
            if (V2rayConfigHandler.Export2ServerConfig(configCopy, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(UIRes.I18N("SaveServerConfigurationIn"), fileName));
            }
        }


    }
}
