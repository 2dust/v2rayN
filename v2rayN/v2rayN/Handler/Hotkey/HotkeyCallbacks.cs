using System;
using System.Reflection;


namespace v2rayN.Handler.Hotkey
{
    public class HotkeyCallbacks
    {

        public static void InitInstance(V2rayHandler v2rayHandler)
        {
            if (Instance != null)
            {
                return;
            }

            Instance = new HotkeyCallbacks(v2rayHandler);
        }

        /// <summary>
        /// Create hotkey callback handler delegate based on callback name
        /// </summary>
        /// <param name="methodname"></param>
        /// <returns></returns>
        public static Delegate GetCallback(string methodname)
        {
            if (string.IsNullOrEmpty(methodname)) throw new ArgumentException(nameof(methodname));
            MethodInfo dynMethod = typeof(HotkeyCallbacks).GetMethod(methodname,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return dynMethod == null ? null : Delegate.CreateDelegate(typeof(Hotkeys.HotKeyCallBackHandler), Instance, dynMethod);
        }

        #region Singleton

        private static HotkeyCallbacks Instance { get; set; }

        private readonly V2rayHandler _v2rayHandler;        

        private HotkeyCallbacks(V2rayHandler v2rayHandler)
        {
            _v2rayHandler = v2rayHandler;           
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// 停止系统代理服务
        /// </summary>
        private void StopProxyCallback()
        {            
            _v2rayHandler.StopProxy();
        }

        /// <summary>
        /// 开启使用全局代理服务模式
        /// </summary>
        private void GlobalProxyModeCallback()
        {
            _v2rayHandler.StartUseGlobalProxyMode();
        }

        /// <summary>
        /// 开启使用PAC代理服务模式
        /// </summary>
        private void PACProxyModeCallback()
        {
            _v2rayHandler.StartUsePACProxyMode(); 
        }

        /// <summary>
        /// 快速添加用户PAC规则
        /// </summary>
        private void AddUserPACCallback()
        {            
            _v2rayHandler.ShowAddUserPACForm();            
        }

        #endregion
    }
}