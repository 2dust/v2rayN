using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ServiceLib.Manager;

namespace v2rayN.Manager;

public sealed class HotkeyManager
{
    private static readonly Lazy<HotkeyManager> _instance = new(() => new());
    public static HotkeyManager Instance = _instance.Value;
    private const int WmHotkey = 0x0312;
    private readonly Dictionary<int, List<EGlobalHotkey>> _hotkeyTriggerDic = new();

    public bool IsPause { get; set; } = false;

    public event Action<bool, string>? UpdateViewEvent;

    public event Action<EGlobalHotkey>? HotkeyTriggerEvent;

    public HotkeyManager()
    {
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreProcessMessage;
        Init();
    }

    private void Init()
    {
        _hotkeyTriggerDic.Clear();
        foreach (var item in AppManager.Instance.Config.GlobalHotkeys)
        {
            if (item.KeyCode != null && (Key)item.KeyCode != Key.None)
            {
                var key = KeyInterop.VirtualKeyFromKey((Key)item.KeyCode);
                var modifiers = KeyModifiers.None;
                if (item.Control)
                {
                    modifiers |= KeyModifiers.Ctrl;
                }

                if (item.Shift)
                {
                    modifiers |= KeyModifiers.Shift;
                }

                if (item.Alt)
                {
                    modifiers |= KeyModifiers.Alt;
                }

                key = key << 16 | (int)modifiers;
                if (!_hotkeyTriggerDic.ContainsKey(key))
                {
                    _hotkeyTriggerDic.Add(key, new() { item.EGlobalHotkey });
                }
                else
                {
                    if (!_hotkeyTriggerDic[key].Contains(item.EGlobalHotkey))
                    {
                        _hotkeyTriggerDic[key].Add(item.EGlobalHotkey);
                    }
                }
            }
        }
    }

    public void Load()
    {
        foreach (var _hotkeyCode in _hotkeyTriggerDic.Keys)
        {
            var hotkeyInfo = GetHotkeyInfo(_hotkeyCode);
            var isSuccess = false;
            var msg = string.Empty;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                isSuccess = RegisterHotKey(nint.Zero, _hotkeyCode, hotkeyInfo.fsModifiers, hotkeyInfo.vKey);
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
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UnregisterHotKey(nint.Zero, hotkey);
            });
        }
        Init();
        Load();
    }

    private (int fsModifiers, int vKey, string hotkeyStr, List<string> Names) GetHotkeyInfo(int hotkeyCode)
    {
        var fsModifiers = hotkeyCode & 0xffff;
        var vKey = hotkeyCode >> 16 & 0xffff;
        var hotkeyStr = new StringBuilder();
        var names = new List<string>();

        var modify = (KeyModifiers)fsModifiers;
        var key = KeyInterop.KeyFromVirtualKey(vKey);
        if ((modify & KeyModifiers.Ctrl) == KeyModifiers.Ctrl)
        {
            hotkeyStr.Append($"{KeyModifiers.Ctrl}+");
        }

        if ((modify & KeyModifiers.Alt) == KeyModifiers.Alt)
        {
            hotkeyStr.Append($"{KeyModifiers.Alt}+");
        }

        if ((modify & KeyModifiers.Shift) == KeyModifiers.Shift)
        {
            hotkeyStr.Append($"{KeyModifiers.Shift}+");
        }

        hotkeyStr.Append(key.ToString());

        foreach (var name in _hotkeyTriggerDic[hotkeyCode])
        {
            names.Add(name.ToString());
        }

        return (fsModifiers, vKey, hotkeyStr.ToString(), names);
    }

    private void OnThreadPreProcessMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey || !_hotkeyTriggerDic.ContainsKey((int)msg.lParam))
        {
            return;
        }
        handled = true;
        var hotKeyCode = (int)msg.lParam;
        if (IsPause)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Keyboard.FocusedElement is UIElement element)
                {
                    var keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, KeyInterop.KeyFromVirtualKey(GetHotkeyInfo(hotKeyCode).vKey))
                    {
                        RoutedEvent = UIElement.KeyDownEvent
                    };
                    element.RaiseEvent(keyEventArgs);
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
    private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vlc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

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
