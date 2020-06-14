using GlobalHotKey;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using v2rayN.Mode;

namespace v2rayN.Handler.Hotkey
{
    public static class Hotkeys
    {
        private static HotKeyManager _hotKeyManager;

        public delegate void HotKeyCallBackHandler();
        // map key and corresponding handler function
        private static Dictionary<HotKey, HotKeyCallBackHandler> _keymap = new Dictionary<HotKey, HotKeyCallBackHandler>();

        public static void Init(V2rayHandler v2rayHandler)
        {
            _hotKeyManager = new HotKeyManager();
            _hotKeyManager.KeyPressed += HotKeyManagerPressed;

            HotkeyCallbacks.InitInstance(v2rayHandler);
        }

        public static void Destroy()
        {
            _hotKeyManager.KeyPressed -= HotKeyManagerPressed;
            _hotKeyManager.Dispose();
        }

        private static void HotKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            var hotkey = e.HotKey;
            HotKeyCallBackHandler callback;
            if (_keymap.TryGetValue(hotkey, out callback))
                callback();
        }

        public static bool RegHotkey(HotKey hotkey, HotKeyCallBackHandler callback)
        {
            UnregExistingHotkey(callback);
            return Register(hotkey, callback);
        }

        public static bool UnregExistingHotkey(Hotkeys.HotKeyCallBackHandler cb)
        {
            HotKey existingHotKey;
            if (IsCallbackExists(cb, out existingHotKey))
            {
                // unregister existing one
                Unregister(existingHotKey);
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsHotkeyExists(HotKey hotKey)
        {
            if (hotKey == null) throw new ArgumentNullException(nameof(hotKey));
            return _keymap.Any(v => v.Key.Equals(hotKey));
        }

        public static bool IsCallbackExists(HotKeyCallBackHandler cb, out HotKey hotkey)
        {
            if (cb == null) throw new ArgumentNullException(nameof(cb));
            if (_keymap.Any(v => v.Value == cb))
            {
                hotkey = _keymap.First(v => v.Value == cb).Key;
                return true;
            }
            else
            {
                hotkey = null;
                return false;
            }
        }

        #region Converters

        public static string HotKey2Str(HotKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return HotKey2Str(key.Key, key.Modifiers);
        }

        public static string HotKey2Str(Key key, ModifierKeys modifier)
        {
            if (!Enum.IsDefined(typeof(Key), key))
                throw new InvalidEnumArgumentException(nameof(key), (int)key, typeof(Key));
            try
            {
                ModifierKeysConverter mkc = new ModifierKeysConverter();
                var keyStr = Enum.GetName(typeof(Key), key);
                var modifierStr = mkc.ConvertToInvariantString(modifier);

                return $"{modifierStr}+{keyStr}";
            }
            catch (NotSupportedException)
            {
                // converter exception
                return null;
            }
        }

        public static HotKey Str2HotKey(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return null;
                int offset = s.LastIndexOf("+", StringComparison.OrdinalIgnoreCase);
                if (offset <= 0) return null;
                string modifierStr = s.Substring(0, offset).Trim();
                string keyStr = s.Substring(offset + 1).Trim();

                KeyConverter kc = new KeyConverter();
                ModifierKeysConverter mkc = new ModifierKeysConverter();
                Key key = (Key)kc.ConvertFrom(keyStr.ToUpper());
                ModifierKeys modifier = (ModifierKeys)mkc.ConvertFrom(modifierStr.ToUpper());

                return new HotKey(key, modifier);
            }
            catch (NotSupportedException)
            {
                // converter exception
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        #endregion

        private static bool Register(HotKey key, HotKeyCallBackHandler callBack)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (callBack == null)
                throw new ArgumentNullException(nameof(callBack));
            try
            {
                _hotKeyManager.Register(key);
                _keymap[key] = callBack;
                return true;
            }
            catch (ArgumentException)
            {
                // already called this method with the specific hotkey
                // return success silently
                return true;
            }
            catch (Win32Exception)
            {
                // this hotkey already registered by other programs
                // notify user to change key
                return false;
            }
        }

        private static void Unregister(HotKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            _hotKeyManager.Unregister(key);
            if (_keymap.ContainsKey(key))
                _keymap.Remove(key);
        }
    }
}