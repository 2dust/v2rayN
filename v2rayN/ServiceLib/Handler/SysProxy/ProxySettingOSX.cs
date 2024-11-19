﻿namespace ServiceLib.Handler.SysProxy
{
    public class ProxySettingOSX
    {
        /*
         * 仅测试了，MacOS 13.7.1 x86 版本，其他版本有待确认
         */

        /// <summary>
        /// 应用接口类型
        /// </summary>
        private static readonly List<string> LstInterface = ["Ethernet", "Wi-Fi", "Thunderbolt Bridge"];

        /// <summary>
        /// 代理类型，对应 http,https,socks
        /// </summary>
        private static readonly List<string> LstTypes = ["setwebproxy", "setsecurewebproxy", "setsocksfirewallproxy"];

        public static async Task SetProxy(string host, int port)
        {
            var lstCmd = GetSetCmds(host, port);
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
            List<CmdItem> lstCmd = [];
            foreach (var interf in LstInterface)
            {
                foreach (var type in LstTypes)
                {
                    lstCmd.Add(new CmdItem()
                    {
                        Cmd = "networksetup",
                        Arguments = [$"-{type}", interf, host, (type.Contains("socks") ? (port - 1) : port).ToString()]
                    });
                }
            }

            return lstCmd;
        }

        private static List<CmdItem> GetUnsetCmds()
        {
            List<CmdItem> lstCmd = [];
            foreach (var interf in LstInterface)
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
    }
}