using System;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Handler.Hotkey
{
    public static class HotkeyReg
    {        
        public static void RegAllHotkeys(HotkeyConfig hotkeyConfig)
        {
            if (hotkeyConfig == null || !hotkeyConfig.regHotkeyAtStartup)
                return;

            // if any of the hotkey reg fail, undo everything
            if (RegHotkeyFromString(hotkeyConfig.stopProxy, "StopProxyCallback")
                && RegHotkeyFromString(hotkeyConfig.globalProxyMode, "GlobalProxyModeCallback")
                && RegHotkeyFromString(hotkeyConfig.pacProxyMode, "PACProxyModeCallback")
                && RegHotkeyFromString(hotkeyConfig.addUserPAC, "AddUserPACCallback")
            )
            {
                // success
            }
            else
            {
                RegHotkeyFromString("", "StopProxyCallback");
                RegHotkeyFromString("", "GlobalProxyModeCallback");
                RegHotkeyFromString("", "PACProxyModeCallback");
                RegHotkeyFromString("", "AddUserPACCallback");
                UI.ShowWarning(UIRes.I18N("HotkeyRegFailed"));
            }
        }

        public static bool RegHotkeyFromString(string hotkeyStr, string callbackName, Action<RegResult> onComplete = null)
        {
            var _callback = HotkeyCallbacks.GetCallback(callbackName);
            if (_callback == null)
            {
                throw new Exception($"{callbackName} not found");
            }

            var callback = _callback as Hotkeys.HotKeyCallBackHandler;

            if (string.IsNullOrEmpty(hotkeyStr))
            {
                Hotkeys.UnregExistingHotkey(callback);
                onComplete?.Invoke(RegResult.UnregSuccess);
                return true;
            }
            else
            {
                var hotkey = Hotkeys.Str2HotKey(hotkeyStr);
                if (hotkey == null)
                {                   
                    onComplete?.Invoke(RegResult.ParseError);
                    return false;
                }
                else
                {
                    bool regResult = (Hotkeys.RegHotkey(hotkey, callback));
                    if (regResult)
                    {
                        onComplete?.Invoke(RegResult.RegSuccess);
                    }
                    else
                    {
                        onComplete?.Invoke(RegResult.RegFailure);
                    }
                    return regResult;
                }
            }
        }

        public enum RegResult
        {
            RegSuccess,
            RegFailure,
            ParseError,
            UnregSuccess,
            //UnregFailure
        }
    }
}
