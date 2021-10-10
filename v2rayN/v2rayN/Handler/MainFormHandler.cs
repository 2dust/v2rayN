using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class MainFormHandler
    {
        private static MainFormHandler instance;
        Action<bool, string> _updateUI;

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
                int index = (int)config.sysProxyType;
                if (index > 0)
                {
                    color = (new Color[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange, Color.DarkSlateBlue, Color.RoyalBlue })[index - 1];
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
            if (config.vmess[index].configType != (int)EConfigType.Vmess
                && config.vmess[index].configType != (int)EConfigType.VLESS)
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
            if (config.vmess[index].configType != (int)EConfigType.Vmess
                && config.vmess[index].configType != (int)EConfigType.VLESS)
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

        public int AddBatchServers(Config config, string clipboardData, string subid = "")
        {
            int counter;
            int _Add()
            {
                return ConfigHandler.AddBatchServers(ref config, clipboardData, subid);
            }
            counter = _Add();
            if (counter < 1)
            {
                clipboardData = Utils.Base64Decode(clipboardData);
                counter = _Add();
            }

            return counter;
        }

        public void BackupGuiNConfig(Config config, bool auto = false)
        {
            string fileName = string.Empty;
            if (auto)
            {
                fileName = Utils.GetTempPath($"guiNConfig{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.json");
            }
            else
            {
                SaveFileDialog fileDialog = new SaveFileDialog
                {
                    Filter = "guiNConfig|*.json",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };
                if (fileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                fileName = fileDialog.FileName;
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            var ret = Utils.ToJsonFile(config, fileName);
            if (!auto)
            {
                if (ret == 0)
                {

                    UI.Show(UIRes.I18N("OperationSuccess"));
                }
                else
                {
                    UI.ShowWarning(UIRes.I18N("OperationFailed"));
                }
            }
        }

        public void UpdateTask(Config config, Action<bool, string> update)
        {
            _updateUI = update;
            Task.Run(() => UpdateTaskRun(config));
        }

        private void UpdateTaskRun(Config config)
        {
            var updateHandle = new UpdateHandle();
            while (true)
            {
                Utils.SaveLog("UpdateTaskRun");
                Thread.Sleep(60000);
                if (config.autoUpdateInterval <= 0)
                {
                    continue;
                }

                updateHandle.UpdateGeoFile("geosite", config, (bool success, string msg) =>
                {
                    _updateUI(false, msg);
                    if (success)
                        Utils.SaveLog("geosite" + msg);
                });

                Thread.Sleep(60000);

                updateHandle.UpdateGeoFile("geoip", config, (bool success, string msg) =>
                {
                    _updateUI(false, msg);
                    if (success)
                        Utils.SaveLog("geoip" + msg);
                });

                Thread.Sleep(60000 * config.autoUpdateInterval);
            }
        }
    }
}