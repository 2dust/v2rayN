using NHotkey;
using NHotkey.Wpf;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    public sealed class MainFormHandler
    {
        private static readonly Lazy<MainFormHandler> instance = new(() => new());
        //Action<bool, string> _updateUI;

        //private DownloadHandle downloadHandle2;
        //private Config _config;
        //private V2rayHandler _v2rayHandler;
        //private List<int> _selecteds;
        //private Thread _workThread;
        //Action<int, string> _updateFunc;
        public static MainFormHandler Instance => instance.Value;

        public Icon GetNotifyIcon(Config config)
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
                return index switch
                {
                    0 => Properties.Resources.NotifyIcon1,
                    1 => Properties.Resources.NotifyIcon2,
                    2 => Properties.Resources.NotifyIcon3,
                    3 => Properties.Resources.NotifyIcon2,
                    _ => Properties.Resources.NotifyIcon1, // default
                };
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return Properties.Resources.NotifyIcon1;
            }
        }

        public System.Windows.Media.ImageSource GetAppIcon(Config config)
        {
            int index = 1;
            switch ((int)config.sysProxyType)
            {
                case 0:
                    index = 1;
                    break;
                case 1:
                case 3:
                    index = 2;
                    break;
                case 2:
                    index = 3;
                    break;
            }
            return BitmapFrame.Create(new Uri($"pack://application:,,,/Resources/NotifyIcon{index}.ico", UriKind.RelativeOrAbsolute));
        }

        private Icon? GetNotifyIcon4Routing(Config config)
        {
            try
            {
                if (!config.routingBasicItem.enableRoutingAdvanced)
                {
                    return null;
                }

                var item = ConfigHandler.GetDefaultRouting(ref config);
                if (item == null || Utils.IsNullOrEmpty(item.customIcon) || !File.Exists(item.customIcon))
                {
                    return null;
                }

                Color color = ColorTranslator.FromHtml("#3399CC");
                int index = (int)config.sysProxyType;
                if (index > 0)
                {
                    color = (new[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange, Color.DarkSlateBlue, Color.RoyalBlue })[index - 1];
                }

                int width = 128;
                int height = 128;

                Bitmap bitmap = new(width, height);
                Graphics graphics = Graphics.FromImage(bitmap);
                SolidBrush drawBrush = new(color);

                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                //graphics.FillRectangle(drawBrush, new Rectangle(0, 0, width, height));                
                graphics.DrawImage(new Bitmap(item.customIcon), 0, 0, width, height);
                graphics.FillEllipse(drawBrush, width / 2, width / 2, width / 2, width / 2);

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

        public void Export2ClientConfig(ProfileItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType == EConfigType.Custom)
            {
                UI.Show(ResUI.NonVmessService);
                return;
            }

            SaveFileDialog fileDialog = new()
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
            if (CoreConfigHandler.Export2ClientConfig(item, fileName, out string msg) != 0)
            {
                UI.Show(msg);
            }
            else
            {
                UI.ShowWarning(string.Format(ResUI.SaveClientConfigurationIn, fileName));
            }
        }

        public void Export2ServerConfig(ProfileItem item, Config config)
        {
            if (item == null)
            {
                return;
            }
            if (item.configType is not EConfigType.VMess and not EConfigType.VLESS)
            {
                UI.Show(ResUI.NonVmessService);
                return;
            }

            SaveFileDialog fileDialog = new()
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
            if (CoreConfigHandler.Export2ServerConfig(item, fileName, out string msg) != 0)
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
            string fileName = $"guiNConfig_{DateTime.Now:yyyy_MM_dd_HH_mm_ss_fff}.json";
            if (auto)
            {
                fileName = Utils.GetBackupPath(fileName);
            }
            else
            {
                SaveFileDialog fileDialog = new()
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

        public bool RestoreGuiNConfig(ref Config config)
        {
            var fileContent = string.Empty;
            using (OpenFileDialog fileDialog = new())
            {
                fileDialog.InitialDirectory = Utils.GetBackupPath("");
                fileDialog.Filter = "guiNConfig|*.json|All|*.*";
                fileDialog.FilterIndex = 2;
                fileDialog.RestoreDirectory = true;

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileContent = Utils.LoadResource(fileDialog.FileName);
                }
                else
                {
                    return false;
                }
            }
            if (Utils.IsNullOrEmpty(fileContent))
            {
                UI.ShowWarning(ResUI.OperationFailed);
                return false;
            }

            var resConfig = Utils.FromJson<Config>(fileContent);
            if (resConfig == null)
            {
                UI.ShowWarning(ResUI.OperationFailed);
                return false;
            }
            //backup first
            BackupGuiNConfig(config, true);

            config = resConfig;
            LazyConfig.Instance.SetConfig(config);

            return true;
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

                if (config.guiItem.autoUpdateSubInterval > 0)
                {
                    if ((dtNow - autoUpdateSubTime).Hours % config.guiItem.autoUpdateSubInterval == 0)
                    {
                        updateHandle.UpdateSubscriptionProcess(config, "", true, (bool success, string msg) =>
                        {
                            update(success, msg);
                            if (success)
                                Utils.SaveLog("subscription" + msg);
                        });
                        autoUpdateSubTime = dtNow;
                    }
                    Thread.Sleep(60000);
                }

                if (config.guiItem.autoUpdateInterval > 0)
                {
                    if ((dtNow - autoUpdateGeoTime).Hours % config.guiItem.autoUpdateInterval == 0)
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

        public void RegisterGlobalHotkey(Config config, Action<EGlobalHotkey> handler, Action<bool, string> update)
        {
            HotkeyHandler.Instance.UpdateViewEvent += update;
            HotkeyHandler.Instance.HotkeyTriggerEvent += handler;
            HotkeyHandler.Instance.Load();
        }

    }
}