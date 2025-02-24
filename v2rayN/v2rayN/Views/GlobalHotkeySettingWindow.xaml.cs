using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using v2rayN.Handler;

namespace v2rayN.Views
{
    public partial class GlobalHotkeySettingWindow
    {
        private static Config? _config;
        private Dictionary<object, KeyEventItem> _textBoxKeyEventItem = new();

        public GlobalHotkeySettingWindow()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            _config = AppHandler.Instance.Config;
            _config.GlobalHotkeys ??= new();

            btnReset.Click += btnReset_Click;
            btnSave.Click += btnSave_ClickAsync;

            txtGlobalHotkey0.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey1.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey2.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey3.KeyDown += TxtGlobalHotkey_PreviewKeyDown;
            txtGlobalHotkey4.KeyDown += TxtGlobalHotkey_PreviewKeyDown;

            HotkeyHandler.Instance.IsPause = true;
            this.Closing += (s, e) => HotkeyHandler.Instance.IsPause = false;
            WindowsUtils.SetDarkBorder(this, _config.UiItem.CurrentTheme);
            InitData();
        }

        private void InitData()
        {
            _textBoxKeyEventItem = new()
            {
                { txtGlobalHotkey0,GetKeyEventItemByEGlobalHotkey(_config.GlobalHotkeys,EGlobalHotkey.ShowForm) },
                { txtGlobalHotkey1,GetKeyEventItemByEGlobalHotkey(_config.GlobalHotkeys,EGlobalHotkey.SystemProxyClear) },
                { txtGlobalHotkey2,GetKeyEventItemByEGlobalHotkey(_config.GlobalHotkeys,EGlobalHotkey.SystemProxySet) },
                { txtGlobalHotkey3,GetKeyEventItemByEGlobalHotkey(_config.GlobalHotkeys,EGlobalHotkey.SystemProxyUnchanged)},
                { txtGlobalHotkey4,GetKeyEventItemByEGlobalHotkey(_config.GlobalHotkeys,EGlobalHotkey.SystemProxyPac)}
            };

            BindingData();
        }

        private void TxtGlobalHotkey_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (sender is null)
            {
                return;
            }

            var item = _textBoxKeyEventItem[sender];
            var modifierKeys = new Key[] { Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin };

            item.KeyCode = (int)(e.Key == Key.System ? (modifierKeys.Contains(e.SystemKey) ? Key.None : e.SystemKey) : (modifierKeys.Contains(e.Key) ? Key.None : e.Key));
            item.Alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            item.Control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            item.Shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            (sender as TextBox)!.Text = KeyEventItemToString(item);
        }

        private KeyEventItem GetKeyEventItemByEGlobalHotkey(List<KeyEventItem> lstKey, EGlobalHotkey eg)
        {
            return JsonUtils.DeepCopy(lstKey.Find((it) => it.EGlobalHotkey == eg) ?? new()
            {
                EGlobalHotkey = eg,
                Control = false,
                Alt = false,
                Shift = false,
                KeyCode = null
            });
        }

        private string KeyEventItemToString(KeyEventItem item)
        {
            var res = new StringBuilder();

            if (item.Control)
            {
                res.Append($"{ModifierKeys.Control}+");
            }

            if (item.Shift)
            {
                res.Append($"{ModifierKeys.Shift}+");
            }

            if (item.Alt)
            {
                res.Append($"{ModifierKeys.Alt}+");
            }

            if (item.KeyCode != null && (Key)item.KeyCode != Key.None)
            {
                res.Append($"{(Key)item.KeyCode}");
            }

            return res.ToString();
        }

        private void BindingData()
        {
            foreach (var item in _textBoxKeyEventItem)
            {
                if (item.Value.KeyCode != null && (Key)item.Value.KeyCode != Key.None)
                {
                    (item.Key as TextBox)!.Text = KeyEventItemToString(item.Value);
                }
                else
                {
                    (item.Key as TextBox)!.Text = string.Empty;
                }
            }
        }

        private async void btnSave_ClickAsync(object sender, RoutedEventArgs e)
        {
            _config.GlobalHotkeys = _textBoxKeyEventItem.Values.ToList();

            if (await ConfigHandler.SaveConfig(_config) == 0)
            {
                HotkeyHandler.Instance.ReLoad();
                this.DialogResult = true;
            }
            else
            {
                UI.Show(ResUI.OperationFailed);
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _textBoxKeyEventItem.Keys)
            {
                var item = _textBoxKeyEventItem[k];

                item.Alt = false;
                item.Control = false;
                item.Shift = false;
                item.KeyCode = (int)Key.None;
            }
            BindingData();
        }
    }
}
