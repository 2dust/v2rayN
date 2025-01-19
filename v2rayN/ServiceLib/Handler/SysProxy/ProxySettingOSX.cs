namespace ServiceLib.Handler.SysProxy
{
    public class ProxySettingOSX
    {
        /// <summary>
        /// 应用接口类型
        /// </summary>
        private static readonly List<string> LstInterface = ["Ethernet", "Wi-Fi", "Thunderbolt Bridge", "USB 10/100/1000 LAN"];

        /// <summary>
        /// 代理类型，对应 http,https,socks
        /// </summary>
        private static readonly List<string> LstTypes = ["setwebproxy", "setsecurewebproxy", "setsocksfirewallproxy"];

        public static async Task SetProxy(string host, int port, string exceptions)
        {
            var lstInterface = await GetListNetworkServices();
            var lstCmd = GetSetCmds(lstInterface, host, port, exceptions);
            await ExecCmd(lstCmd);
        }

        public static async Task UnsetProxy()
        {
            var lstInterface = await GetListNetworkServices();
            var lstCmd = GetUnsetCmds(lstInterface);
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

        private static List<CmdItem> GetSetCmds(List<string> lstInterface, string host, int port, string exceptions)
        {
            List<CmdItem> lstCmd = [];
            foreach (var interf in lstInterface)
            {
                foreach (var type in LstTypes)
                {
                    lstCmd.Add(new CmdItem()
                    {
                        Cmd = "networksetup",
                        Arguments = [$"-{type}", interf, host, port.ToString()]
                    });
                }
                if (exceptions.IsNotEmpty())
                {
                    List<string> args = [$"-setproxybypassdomains", interf];
                    args.AddRange(exceptions.Split(','));
                    lstCmd.Add(new CmdItem()
                    {
                        Cmd = "networksetup",
                        Arguments = args
                    });
                }
            }

            return lstCmd;
        }

        private static List<CmdItem> GetUnsetCmds(List<string> lstInterface)
        {
            List<CmdItem> lstCmd = [];
            foreach (var interf in lstInterface)
            {
                foreach (var type in LstTypes)
                {
                    lstCmd.Add(new CmdItem()
                    {
                        Cmd = "networksetup",
                        Arguments = [$"-{type}state", interf, "off"]
                    });
                }
            }

            return lstCmd;
        }

        public static async Task<List<string>> GetListNetworkServices()
        {
            var services = await Utils.GetListNetworkServices();
            if (services.IsNullOrEmpty())
            {
                return LstInterface;
            }

            var lst = services.Split(Environment.NewLine).Where(t => t.Length > 0 && t.Contains('*') == false);
            return lst.ToList();
        }
    }
}