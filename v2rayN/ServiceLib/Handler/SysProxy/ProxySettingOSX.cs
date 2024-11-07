namespace ServiceLib.Handler.SysProxy
{
    /// <summary>
    /// 开启代理
    /// networksetup -setsocksfirewallproxy "Ethernet" 127.0.0.1 10808
    /// networksetup -setsocksfirewallproxy "Wi-Fi" 127.0.0.1 10808
    /// networksetup -setsocksfirewallproxy "Thunderbolt Bridge" 127.0.0.1 10808
    /// 关闭代理
    /// networksetup -setsocksfirewallproxy "Ethernet" off
    /// networksetup -setsocksfirewallproxy "Wi-Fi" off
    /// networksetup -setsocksfirewallproxy "Thunderbolt Bridge" off
    ///
    /// 只测试过 x86 MacOS 13.7.1
    /// </summary>
    public class ProxySettingOSX
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port">此处传入的是HTTP端口，SOCKS端口需要-1</param>
        public static async Task SetProxy(string host, int port)
        {
            var lstCmd = GetSetCmds(host, port - 1);
            await ExecCmd(lstCmd);
        }


        public static async Task UnsetProxy()
        {
            var lstCmd = GetUnsetCmds();
            await ExecCmd(lstCmd);
        }


        private static async Task ExecCmd(List<CmdItem> lstCmd)
        {
            foreach (var cmd in lstCmd)
            {
                if (cmd is null || cmd.Cmd.IsNullOrEmpty() || cmd.Arguments is null)
                {
                    continue;
                }

                await Task.Delay(10);
                await Utils.GetCliWrapOutput(cmd.Cmd, cmd.Arguments);
            }
        }

        private static List<CmdItem> GetSetCmds(string host, int port)
        {
            List<string> lstType = ["Ethernet", "Wi-Fi", "Thunderbolt Bridge", "iPhone USB"];
            List<CmdItem> lstCmd = [];
            foreach (var type in lstType)
            {
                lstCmd.AddRange(GetSetCmd4Intel(host, port, type));
            }

            return lstCmd;
        }

        private static List<CmdItem> GetUnsetCmds()
        {
            List<string> lstType = ["Ethernet", "Wi-Fi", "Thunderbolt Bridge", "iPhone USB"];
            List<CmdItem> lstCmd = [];
            foreach (var type in lstType)
            {
                lstCmd.Add(new CmdItem()
                {
                    Cmd = "networksetup",
                    Arguments = ["-setsocksfirewallproxy", type, "off"]
                });
            }

            return lstCmd;
        }

        private static List<CmdItem> GetSetCmd4Intel(string host, int port, string type)
        {
            List<CmdItem> lstCmd = [];
            lstCmd.Add(new CmdItem()
            {
                Cmd = "networksetup",
                Arguments = ["-setsocksfirewallproxy", type, host, port.ToString()]
            });
            return lstCmd;
        }
    }
}