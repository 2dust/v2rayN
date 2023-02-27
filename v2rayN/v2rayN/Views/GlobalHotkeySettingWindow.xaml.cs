using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;
using Forms = System.Windows.Forms;

namespace v2rayN.Views
{
    public partial class GlobalHotkeySettingWindow
    {
        private static Config _config;
        List<KeyEventItem> lstKey;

        public GlobalHotkeySettingWindow()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            _config = LazyConfig.Instance.GetConfig();

            if (_config.globalHotkeys == null)
            {
                _config.globalHotkeys = new List<KeyEventItem>();
            }

            foreach (EGlobalHotkey it in Enum.GetValues(typeof(EGlobalHotkey)))
            {
                if (_config.globalHotkeys.FindIndex(t => t.eGlobalHotkey == it) >= 0)
                {
                    continue;
                }

                _config.globalHotkeys.Add(new KeyEventItem()
                {
                    eGlobalHotkey = it,
                    Alt = false,
                    Control = false,
                    Shift = false,
                    KeyCode = null
                });
            }

            lstKey = Utils.DeepCopy(_config.globalHotkeys);

            txtGlobalHotkey0.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey1.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey2.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey3.KeyDown += TxtGlobalHotkey_KeyDown;
            txtGlobalHotkey4.KeyDown += TxtGlobalHotkey_KeyDown;

            BindingData(-1);

            Utils.SetDarkBorder(this, _config.uiItem.colorModeDark);
        }

        private void TxtGlobalHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var _ModifierKeys =  new Key[]{ Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt };
            if (!_ModifierKeys.Contains(e.Key) && !_ModifierKeys.Contains(e.SystemKey))
            {
                var txt = ((TextBox)sender);
                var index = Utils.ToInt(txt.Name.Substring(txt.Name.Length - 1, 1));
                var formsKey = (Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

                lstKey[index].KeyCode = formsKey;
                lstKey[index].Alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
                lstKey[index].Control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                lstKey[index].Shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                BindingData(index);
            }
        }

        private void BindingData(int index)
        {
            for (int k = 0; k < lstKey.Count; k++)
            {
                if (index >= 0 && index != k)
                {
                    continue;
                }
                var item = lstKey[k];
                var keys = string.Empty;

                if (item.Control)
                {
                    keys += $"{Forms.Keys.Control} + ";
                }
                if (item.Alt)
                {
                    keys += $"{Forms.Keys.Alt} + ";
                }
                if (item.Shift)
                {
                    keys += $"{Forms.Keys.Shift} + ";
                }
                if (item.KeyCode != null)
                {
                    keys += $"{item.KeyCode}";
                }

                SetText($"txtGlobalHotkey{k}", keys);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            _config.globalHotkeys = lstKey;

            if (ConfigHandler.SaveConfig(ref _config, false) == 0)
            {
                this.DialogResult = true;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            lstKey.Clear();
            foreach (EGlobalHotkey it in Enum.GetValues(typeof(EGlobalHotkey)))
            {
                if (lstKey.FindIndex(t => t.eGlobalHotkey == it) >= 0)
                {
                    continue;
                }

                lstKey.Add(new KeyEventItem()
                {
                    eGlobalHotkey = it,
                    Alt = false,
                    Control = false,
                    Shift = false,
                    KeyCode = null
                });
            }
            BindingData(-1);
        }
        private void SetText(string name, string txt)
        {
            foreach (UIElement element in gridText.Children)
            {
                if (element is TextBox box)
                {
                    if (box.Name == name)
                    {
                        box.Text = txt;
                    }
                }
            }
        }

        private void GlobalHotkeySettingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
