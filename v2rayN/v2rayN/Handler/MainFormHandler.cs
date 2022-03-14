﻿using NHotkey;
using NHotkey.WindowsForms;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using v2rayN.Mode;

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

                var customIcon = false;
                if (config.enableRoutingAdvanced)
                {
                    var item = config.routings[config.routingIndex];
                    if (!Utils.IsNullOrEmpty(item.customIcon) && File.Exists(item.customIcon))
                    {
                        graphics.FillRectangle(drawBrush, new Rectangle(0, 0, width, height));
                        graphics.DrawImage(new Bitmap(item.customIcon), 0, 0);
                        customIcon = true;
                    }
                }
                if (!customIcon)
                {
                    graphics.FillEllipse(drawBrush, new Rectangle(0, 0, width, height));
                    int zoom = 16;
                    graphics.DrawImage(new Bitmap(Properties.Resources.notify, width - zoom, width - zoom), zoom / 2, zoom / 2);
                }

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

        public void Export2ClientConfig(VmessItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType != (int)EConfigType.Vmess
                && item.configType != (int)EConfigType.VLESS)
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
            //Config configCopy = Utils.DeepCopy(config);
            //configCopy.index = index;
            if (V2rayConfigHandler.Export2ClientConfig(item, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(UIRes.I18N("SaveClientConfigurationIn"), fileName));
            }
        }

        public void Export2ServerConfig(VmessItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType != (int)EConfigType.Vmess
                && item.configType != (int)EConfigType.VLESS)
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
            //Config configCopy = Utils.DeepCopy(config);
            //configCopy.index = index;
            if (V2rayConfigHandler.Export2ServerConfig(item, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(UIRes.I18N("SaveServerConfigurationIn"), fileName));
            }
        }

        public int AddBatchServers(Config config, string clipboardData, string subid, string groupId)
        {
            int counter;
            int _Add()
            {
                return ConfigHandler.AddBatchServers(ref config, clipboardData, subid, groupId);
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
            string fileName = $"guiNConfig_{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")}.json";
            if (auto)
            {
                fileName = Utils.GetTempPath(fileName);
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
            Task.Run(() => UpdateTaskRun(config, update));
        }

        private void UpdateTaskRun(Config config, Action<bool, string> update)
        {
            var updateHandle = new UpdateHandle();
            while (true)
            {
                Thread.Sleep(60000);
                if (config.autoUpdateInterval <= 0)
                {
                    continue;
                }
                Utils.SaveLog("UpdateTaskRun");

                updateHandle.UpdateGeoFile("geosite", config, (bool success, string msg) =>
                {
                    update(false, msg);
                    if (success)
                        Utils.SaveLog("geosite" + msg);
                });

                Thread.Sleep(60000);

                updateHandle.UpdateGeoFile("geoip", config, (bool success, string msg) =>
                {
                    update(false, msg);
                    if (success)
                        Utils.SaveLog("geoip" + msg);
                });

                Thread.Sleep(1000 * 3600 * config.autoUpdateInterval);
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
                    var msg = string.Format(UIRes.I18N("RegisterGlobalHotkeySuccessfully"), $"{item.eGlobalHotkey.ToString()} = {keys}");
                    update(false, msg);
                }
                catch (Exception ex)
                {
                    var msg = string.Format(UIRes.I18N("RegisterGlobalHotkeyFailed"), $"{item.eGlobalHotkey.ToString()} = {keys}", ex.Message);
                    update(false, msg);
                    Utils.SaveLog(msg);
                }
            }
        }

    }
}