using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using v2rayN.Models;
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

        public bool IsPause { get; set; } = false;

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
            foreach (var item in _config.globalHotkeys)
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
                        _hotkeyTriggerDic.Add(key, new() { item.eGlobalHotkey });
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
            foreach (var _hotkeyCode in _hotkeyTriggerDic.Keys)
            {
                var hotkeyInfo = GetHotkeyInfo(_hotkeyCode);
                bool isSuccess = false;
                string msg;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    isSuccess = RegisterHotKey(IntPtr.Zero, _hotkeyCode, hotkeyInfo.fsModifiers, hotkeyInfo.vKey);
                });
                foreach (var name in hotkeyInfo.Names)
                {
                    if (isSuccess)
                    {
                        msg = string.Format(ResUI.RegisterGlobalHotkeySuccessfully, $"{name}({hotkeyInfo.hotkeyStr})");
                    }
                    else
                    {
                        var errInfo = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        msg = string.Format(ResUI.RegisterGlobalHotkeyFailed, $"{name}({hotkeyInfo.hotkeyStr})", errInfo);
                    }
                    UpdateViewEvent?.Invoke(false, msg);
                }
            }
        }

        public void ReLoad()
        {
            foreach (var hotkey in _hotkeyTriggerDic.Keys)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UnregisterHotKey(IntPtr.Zero, hotkey);
                });
            }
            Init();
            Load();
        }

        private (int fsModifiers, int vKey, string hotkeyStr, List<string> Names) GetHotkeyInfo(int hotkeycode)
        {
            var _fsModifiers = hotkeycode & 0xffff;
            var _vkey = (hotkeycode >> 16) & 0xffff;
            var _hotkeyStr = new StringBuilder();
            var _names = new List<string>();

            var mdif = (KeyModifiers)_fsModifiers;
            var key = KeyInterop.KeyFromVirtualKey(_vkey);
            if ((mdif & KeyModifiers.Ctrl) == KeyModifiers.Ctrl) _hotkeyStr.Append($"{KeyModifiers.Ctrl}+");
            if ((mdif & KeyModifiers.Alt) == KeyModifiers.Alt) _hotkeyStr.Append($"{KeyModifiers.Alt}+");
            if ((mdif & KeyModifiers.Shift) == KeyModifiers.Shift) _hotkeyStr.Append($"{KeyModifiers.Shift}+");
            _hotkeyStr.Append(key.ToString());

            foreach (var name in _hotkeyTriggerDic[hotkeycode])
            {
                _names.Add(name.ToString());
            }

            return (_fsModifiers, _vkey, _hotkeyStr.ToString(), _names);
        }

        private void OnThreadPreProcessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message != WmHotkey || !_hotkeyTriggerDic.ContainsKey((int)msg.lParam))
            {
                return;
            }
            handled = true;
            var _hotKeyCode = (int)msg.lParam;
            if (IsPause)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UIElement? element = Keyboard.FocusedElement as UIElement;
                    if (element != null)
                    {
                        var _keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice,
                            PresentationSource.FromVisual(element), 0,
                            KeyInterop.KeyFromVirtualKey(GetHotkeyInfo(_hotKeyCode).vKey))
                        {
                            RoutedEvent = UIElement.KeyDownEvent
                        };
                        element.RaiseEvent(_keyEventArgs);
                    }
                });
            }
            else
            {
                foreach (var keyEvent in _hotkeyTriggerDic[(int)msg.lParam])
                {
                    HotkeyTriggerEvent?.Invoke(keyEvent);
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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