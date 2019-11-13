using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using v2rayN.Base;
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
                var color = ColorTranslator.FromHtml("#3399CC");
                var index = config.listenerType;
                if (index > 0)
                {
                    color = (new Color[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange })[index - 1];
                    //color = ColorTranslator.FromHtml(new string[] { "#CC0066", "#CC6600", "#99CC99", "#666699" }[index - 1]);
                }

                var width = 128;
                var height = 128;

                var bitmap = new Bitmap(width, height);
                var graphics = Graphics.FromImage(bitmap);
                var drawBrush = new SolidBrush(color);

                graphics.FillEllipse(drawBrush, new Rectangle(0, 0, width, height));
                var zoom = 16;
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

            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Config|*.json";
            fileDialog.FilterIndex = 2;
            fileDialog.RestoreDirectory = true;
            if (fileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string fileName = fileDialog.FileName;
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            Config configCopy = Utils.DeepCopy<Config>(config);
            configCopy.index = index;
            string msg;
            if (V2rayConfigHandler.Export2ClientConfig(configCopy, fileName, out msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.Show(string.Format(UIRes.I18N("SaveClientConfigurationIn"), fileName));
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

            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Config|*.json";
            fileDialog.FilterIndex = 2;
            fileDialog.RestoreDirectory = true;
            if (fileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string fileName = fileDialog.FileName;
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            Config configCopy = Utils.DeepCopy<Config>(config);
            configCopy.index = index;
            string msg;
            if (V2rayConfigHandler.Export2ServerConfig(configCopy, fileName, out msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.Show(string.Format(UIRes.I18N("SaveServerConfigurationIn"), fileName));
            }
        }

        
    }
}
