using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Views
{
    public partial class GlobalHotkeySettingWindow
    {
        private static Config _config = default!;
        private Dictionary<object, KeyEventItem> _TextBoxKeyEventItem = default!;

        public GlobalHotkeySettingWindow()
        {
            InitializeComponent();

            // 设置窗口的尺寸不大于屏幕的尺寸
            if (this.Width > SystemParameters.WorkArea.Width)
            {
                this.Width = SystemParameters.WorkArea.Width;
            }
            if (this.Height > SystemParameters.WorkArea.Height)
            {
                this.Height = SystemParameters.WorkArea.Height;
            }

            this.Owner = Application.Current.MainWindow;
            _config = LazyConfig.Instance.GetConfig();
            _config.globalHotkeys ??= new List<KeyEventItem>();

            txtGlobalHotkey0.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey1.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey2.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey3.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey4.KeyDown += TxtGlobalHotkey_PreviewKeyDown;

            HotkeyHandler.Instance.IsPause = true;
            this.Closing += (s, e) => HotkeyHandler.Instance.IsPause = false;
            Utils.SetDarkBorder(this, _config.uiItem.colorModeDark);
            InitData();
        }

        private void InitData()
        {
            _TextBoxKeyEventItem = new()
            {
                { txtGlobalHotkey0,GetKeyEventItemByEGlobalHotkey(_config.globalHotkeys,EGlobalHotkey.ShowForm) },
                { txtGlobalHotkey1,GetKeyEventItemByEGlobalHotkey(_config.globalHotkeys,EGlobalHotkey.SystemProxyClear) },
                { txtGlobalHotkey2,GetKeyEventItemByEGlobalHotkey(_config.globalHotkeys,EGlobalHotkey.SystemProxySet) },
                { txtGlobalHotkey3,GetKeyEventItemByEGlobalHotkey(_config.globalHotkeys,EGlobalHotkey.SystemProxyUnchanged)},
                { txtGlobalHotkey4,GetKeyEventItemByEGlobalHotkey(_config.globalHotkeys,EGlobalHotkey.SystemProxyPac)}
            };
            BindingData();
        }

        private void TxtGlobalHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var _ModifierKeys = new Key[] { Key.LeftCtrl, Key.RightCtrl, Key.LeftShift,
                Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin};
            _TextBoxKeyEventItem[sender].KeyCode = e.Key == Key.System ? (_ModifierKeys.Contains(e.SystemKey) ? Key.None : e.SystemKey) : (_ModifierKeys.Contains(e.Key) ? Key.None : e.Key);
            _TextBoxKeyEventItem[sender].Alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            _TextBoxKeyEventItem[sender].Control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            _TextBoxKeyEventItem[sender].Shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            (sender as TextBox)!.Text = KeyEventItemToString(_TextBoxKeyEventItem[sender]);
        }

        private KeyEventItem GetKeyEventItemByEGlobalHotkey(List<KeyEventItem> KELsit, EGlobalHotkey eg)
        {
            return JsonUtils.DeepCopy(KELsit.Find((it) => it.eGlobalHotkey == eg) ?? new()
            {
                eGlobalHotkey = eg,
                Control = false,
                Alt = false,
                Shift = false,
                KeyCode = null
            });
        }

        private string KeyEventItemToString(KeyEventItem item)
        {
            var res = new StringBuilder();

            if (item.Control) res.Append($"{ModifierKeys.Control}+");
            if (item.Shift) res.Append($"{ModifierKeys.Shift}+");
            if (item.Alt) res.Append($"{ModifierKeys.Alt}+");
            if (item.KeyCode != null && item.KeyCode != Key.None)
                res.Append($"{item.KeyCode}");

            return res.ToString();
        }

        private void BindingData()
        {
            foreach (var item in _TextBoxKeyEventItem)
            {
                if (item.Value.KeyCode != null && item.Value.KeyCode != Key.None)
                {
                    (item.Key as TextBox)!.Text = KeyEventItemToString(item.Value);
                }
                else
                {
                    (item.Key as TextBox)!.Text = string.Empty;
                }
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            _config.globalHotkeys = _TextBoxKeyEventItem.Values.ToList();

            if (ConfigHandler.SaveConfig(_config, false) == 0)
            {
                HotkeyHandler.Instance.ReLoad();
                this.DialogResult = true;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _TextBoxKeyEventItem.Keys)
            {
                _TextBoxKeyEventItem[k].Alt = false;
                _TextBoxKeyEventItem[k].Control = false;
                _TextBoxKeyEventItem[k].Shift = false;
                _TextBoxKeyEventItem[k].KeyCode = Key.None;
            }
            BindingData();
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