using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Win32.Input;
using GlobalHotKeys;

namespace v2rayN.Desktop.Handler
{
    public sealed class HotkeyHandler
    {
        private static readonly Lazy<HotkeyHandler> _instance = new(() => new());
        public static HotkeyHandler Instance = _instance.Value;
        private readonly Dictionary<int, EGlobalHotkey> _hotkeyTriggerDic = new();
        private HotKeyManager? _hotKeyManager;

        private Config? _config;

        private event Action<EGlobalHotkey>? _updateFunc;

        public bool IsPause { get; set; } = false;

        public void Init(Config config, Action<EGlobalHotkey> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;

            Register();
        }

        public void Dispose()
        {
            _hotKeyManager?.Dispose();
        }

        private void Register()
        {
            if (_config.GlobalHotkeys.Any(t => t.KeyCode > 0) == false)
            {
                return;
            }
            _hotKeyManager ??= new GlobalHotKeys.HotKeyManager();
            _hotkeyTriggerDic.Clear();

            foreach (var item in _config.GlobalHotkeys)
            {
                if (item.KeyCode is null or 0)
                {
                    continue;
                }

                var vKey = KeyInterop.VirtualKeyFromKey((Key)item.KeyCode);
                var modifiers = Modifiers.NoRepeat;
                if (item.Control)
                {
                    modifiers |= Modifiers.Control;
                }
                if (item.Shift)
                {
                    modifiers |= Modifiers.Shift;
                }
                if (item.Alt)
                {
                    modifiers |= Modifiers.Alt;
                }

                var result = _hotKeyManager?.Register((VirtualKeyCode)vKey, modifiers);
                if (result?.IsSuccessful == true)
                {
                    _hotkeyTriggerDic.Add(result.Id, item.EGlobalHotkey);
                }
            }

            _hotKeyManager?.HotKeyPressed
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(OnNext);
        }

        private void OnNext(HotKey key)
        {
            if (_updateFunc == null || IsPause)
            {
                return;
            }

            if (_hotkeyTriggerDic.TryGetValue(key.Id, out var value))
            {
                _updateFunc?.Invoke(value);
            }
        }
    }
}
