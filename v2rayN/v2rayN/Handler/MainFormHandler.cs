using NHotkey;
using NHotkey.WindowsForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Mode;
using System.Linq;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    public sealed class MainFormHandler
    {
        private static readonly Lazy<MainFormHandler> instance = new Lazy<MainFormHandler>(() => new MainFormHandler());
        //Action<bool, string> _updateUI;

        //private DownloadHandle downloadHandle2;
        //private Config _config;
        //private V2rayHandler _v2rayHandler;
        //private List<int> _selecteds;
        //private Thread _workThread;
        //Action<int, string> _updateFunc;
        public static MainFormHandler Instance
        {
            get { return instance.Value; }
        }
        public Icon GetNotifyIcon(Config config, Icon def)
        {
            try
            {
                int index = (int)config.sysProxyType;

                //Load from routing setting
                var createdIcon = GetNotifyIcon4Routing(config);
                if (createdIcon != null)
                {
                    return createdIcon;
                }

                //Load from local file
                var fileName = Utils.GetPath($"NotifyIcon{index + 1}.ico");
                if (File.Exists(fileName))
                {
                    return new Icon(fileName);
                }
                switch (index)
                {
                    case 0:
                        return Properties.Resources.NotifyIcon1;
                    case 1:
                        return Properties.Resources.NotifyIcon2;
                    case 2:
                        return Properties.Resources.NotifyIcon3;
                }

                return Properties.Resources.NotifyIcon1;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return def;
            }
        }
        private Icon GetNotifyIcon4Routing(Config config)
        {
            try
            {
                if (!config.enableRoutingAdvanced)
                {
                    return null;
                }

                var item = config.routings[config.routingIndex];
                if (Utils.IsNullOrEmpty(item.customIcon) || !File.Exists(item.customIcon))
                {
                    return null;
                }

                Color color = ColorTranslator.FromHtml("#3399CC");
                int index = (int)config.sysProxyType;
                if (index > 0)
                {
                    color = (new Color[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange, Color.DarkSlateBlue, Color.RoyalBlue })[index - 1];
                }

                int width = 128;
                int height = 128;

                Bitmap bitmap = new Bitmap(width, height);
                Graphics graphics = Graphics.FromImage(bitmap);
                SolidBrush drawBrush = new SolidBrush(color);

                graphics.FillRectangle(drawBrush, new Rectangle(0, 0, width, height));
                graphics.DrawImage(new Bitmap(item.customIcon), 0, 0, width, height);
                Icon createdIcon = Icon.FromHandle(bitmap.GetHicon());

                drawBrush.Dispose();
                graphics.Dispose();
                bitmap.Dispose();

                return createdIcon;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return null;
            }
        }

        public void Export2ClientConfig(VmessItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType != EConfigType.Vmess
                && item.configType != EConfigType.VLESS)
            {
                UI.Show(ResUI.NonVmessService);
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
            //Config configCopy = Utils.DeepCopy(config);
            //configCopy.index = index;
            if (V2rayConfigHandler.Export2ClientConfig(item, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(ResUI.SaveClientConfigurationIn, fileName));
            }
        }

        public void Export2ServerConfig(VmessItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType != EConfigType.Vmess
                && item.configType != EConfigType.VLESS)
            {
                UI.Show(ResUI.NonVmessService);
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
            //Config configCopy = Utils.DeepCopy(config);
            //configCopy.index = index;
            if (V2rayConfigHandler.Export2ServerConfig(item, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(ResUI.SaveServerConfigurationIn, fileName));
            }
        }

        public void BackupGuiNConfig(Config config, bool auto = false)
        {
            string fileName = $"guiNConfig_{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")}.json";
            if (auto)
            {
                fileName = Utils.GetBackupPath(fileName);
            }
            else
            {
                SaveFileDialog fileDialog = new SaveFileDialog
                {
                    FileName = fileName,
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

                    UI.Show(ResUI.OperationSuccess);
                }
                else
                {
                    UI.ShowWarning(ResUI.OperationFailed);
                }
            }
        }

        public void UpdateTask(Config config, Action<bool, string> update)
        {
            Task.Run(() => UpdateTaskRun(config, update));
        }

        private void UpdateTaskRun(Config config, Action<bool, string> update)
        {
            var autoUpdateSubTime = DateTime.Now;
            var autoUpdateGeoTime = DateTime.Now;

            Thread.Sleep(60000);
            Utils.SaveLog("UpdateTaskRun");

            var updateHandle = new UpdateHandle();
            while (true)
            {
                var dtNow = DateTime.Now;

                if (config.autoUpdateSubInterval > 0)
                {
                    if ((dtNow - autoUpdateSubTime).Hours % config.autoUpdateSubInterval == 0)
                    {
                        updateHandle.UpdateSubscriptionProcess(config, true, (bool success, string msg) =>
                        {
                            update(success, msg);
                            if (success)
                                Utils.SaveLog("subscription" + msg);
                        });
                        autoUpdateSubTime = dtNow;
                    }
                    Thread.Sleep(60000);
                }

                if (config.autoUpdateInterval > 0)
                {
                    if ((dtNow - autoUpdateGeoTime).Hours % config.autoUpdateInterval == 0)
                    {
                        updateHandle.UpdateGeoFile("geosite", config, (bool success, string msg) =>
                        {
                            update(false, msg);
                            if (success)
                                Utils.SaveLog("geosite" + msg);
                        });

                        updateHandle.UpdateGeoFile("geoip", config, (bool success, string msg) =>
                        {
                            update(false, msg);
                            if (success)
                                Utils.SaveLog("geoip" + msg);
                        });
                        autoUpdateGeoTime = dtNow;
                    }
                }

                Thread.Sleep(1000 * 3600);
            }
        }

        public void RegisterGlobalHotkey(Config config, EventHandler<HotkeyEventArgs> handler, Action<bool, string> update)
        {
            if (config.globalHotkeys == null)
            {
                return;
            }

            foreach (var item in config.globalHotkeys)
            {
                if (item.KeyCode == null)
                {
                    continue;
                }

                Keys keys = (Keys)item.KeyCode;
                if (item.Control)
                {
                    keys |= Keys.Control;
                }
                if (item.Alt)
                {
                    keys |= Keys.Alt;
                }
                if (item.Shift)
                {
                    keys |= Keys.Shift;
                }

                try
                {
                    HotkeyManager.Current.AddOrReplace(((int)item.eGlobalHotkey).ToString(), keys, handler);
                    var msg = string.Format(ResUI.RegisterGlobalHotkeySuccessfully, $"{item.eGlobalHotkey.ToString()} = {keys}");
                    update(false, msg);
                }
                catch (Exception ex)
                {
                    var msg = string.Format(ResUI.RegisterGlobalHotkeyFailed, $"{item.eGlobalHotkey.ToString()} = {keys}", ex.Message);
                    update(false, msg);
                    Utils.SaveLog(msg);
                }
            }
        }

    }
}