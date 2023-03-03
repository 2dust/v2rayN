using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    public sealed class HotkeyHandler
    {
        private static readonly Lazy<HotkeyHandler> _instance = new(() => new());
        public static HotkeyHandler Instance = _instance.Value;

        private const int WmHotkey = 0x0312;
        private Config _config
        {
            get => LazyConfig.Instance.GetConfig();
        }
        private Dictionary<int, List<EGlobalHotkey>> _hotkeyTriggerDic;

        public bool IsPause { get; private set; } = false;
        public event Action<bool, string>? UpdateViewEvent;
        public event Action<EGlobalHotkey>? HotkeyTriggerEvent;
        public HotkeyHandler()
        {
            _hotkeyTriggerDic = new();            
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreProcessMessage;
            Init();
        }

        private void Init()
        {
            _hotkeyTriggerDic.Clear();
            if (_config.globalHotkeys == null) return;
            foreach(var item in _config.globalHotkeys)
            {
                if (item.KeyCode != null && item.KeyCode != Key.None)
                {
                    int key = KeyInterop.VirtualKeyFromKey((Key)item.KeyCode);
                    KeyModifiers modifiers = KeyModifiers.None;
                    if (item.Control) modifiers |= KeyModifiers.Ctrl;
                    if (item.Shift) modifiers |= KeyModifiers.Shift;
                    if (item.Alt) modifiers |= KeyModifiers.Alt;
                    key = (key << 16) | (int)modifiers;
                    if (!_hotkeyTriggerDic.ContainsKey(key))
                    {
                        _hotkeyTriggerDic.Add(key,  new() { item.eGlobalHotkey });
                    }
                    else
                    {
                        if (!_hotkeyTriggerDic[key].Contains(item.eGlobalHotkey))
                            _hotkeyTriggerDic[key].Add(item.eGlobalHotkey);
                    }
                }
            }
        }
        public void Load()
        {
            foreach(var hotkey in _hotkeyTriggerDic.Keys)
            {
                var _fsModifiers = hotkey & 0xffff;
                var _vkey = (hotkey >> 16) & 0xffff;
                var hotkeyStr = HotkeyToString(_fsModifiers, _vkey);
                bool isSuccess = false;
                string msg;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    isSuccess = RegisterHotKey(IntPtr.Zero, hotkey, _fsModifiers, _vkey); 
                });                                
                if (isSuccess)
                {
                    msg = string.Format(ResUI.RegisterGlobalHotkeySuccessfully, $"{hotkeyStr}");
                }
                else
                {
                    var errInfo = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    msg = string.Format(ResUI.RegisterGlobalHotkeyFailed, $"{hotkeyStr}", errInfo);
                }
                UpdateViewEvent?.Invoke(false, msg);
            }
        }

        public void ReLoad()
        {
            foreach(var hotkey in _hotkeyTriggerDic.Keys)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UnregisterHotKey(IntPtr.Zero, hotkey);
                });
            }
            Init();
            Load();
        }
        
        private void OnThreadPreProcessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message != WmHotkey || IsPause || !_hotkeyTriggerDic.Keys.Contains((int)msg.lParam))
                return;
            handled = true;
            foreach (var keyEvent in _hotkeyTriggerDic[(int)msg.lParam])
            {
                HotkeyTriggerEvent?.Invoke(keyEvent);
            }
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private static string HotkeyToString(int fsModifiers,int vk)
        {
            var sb = new StringBuilder();
            var mdif = (KeyModifiers)fsModifiers;
            var key = KeyInterop.KeyFromVirtualKey(vk);
            if ((mdif | KeyModifiers.Ctrl) == KeyModifiers.Ctrl) sb.Append($"{KeyModifiers.Ctrl}+");
            if ((mdif | KeyModifiers.Alt) == KeyModifiers.Alt) sb.Append($"{KeyModifiers.Alt}+");
            if ((mdif | KeyModifiers.Shift) == KeyModifiers.Shift) sb.Append($"{KeyModifiers.Shift}+");
            sb.Append(key.ToString());
            return sb.ToString();
        }
        [Flags]
        private enum KeyModifiers
        {
            None = 0x0000,
            Alt = 0x0001,
            Ctrl = 0x0002,
            Shift = 0x0004,
            Win = 0x0008,
            NoRepeat = 0x4000
        }
    }
}
