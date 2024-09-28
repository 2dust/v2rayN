using CliWrap;
using CliWrap.Buffered;

namespace v2rayN.Desktop.Common
{
    public class ProxySettingLinux
    {
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
                if (cmd is null || cmd.Cmd.IsNullOrEmpty() || cmd.Arguments.IsNullOrEmpty())
                { continue; }

                await Task.Delay(10);
                var result = await Cli.Wrap(cmd.Cmd)
                       .WithArguments(cmd.Arguments)
                       .ExecuteBufferedAsync();

                if (result.ExitCode != 0)
                {
                    //Logging.SaveLog($"Command failed {cmd.Cmd},{cmd.Arguments}");
                    Logging.SaveLog(result.ToString() ?? "");
                }
            }
        }

        private static List<CmdItem> GetSetCmds(string host, int port)
        {
            //TODO KDE     //XDG_CURRENT_DESKTOP
            List<string> lstType = ["http", "https", "socks", "ftp"];
            List<CmdItem> lstCmd = [];

            lstCmd.Add(new CmdItem()
            {
                Cmd = "gsettings",
                Arguments = "set org.gnome.system.proxy mode manual"
            });

            foreach (string type in lstType)
            {
                lstCmd.AddRange(GetSetCmdByType(type, host, port));
            }

            return lstCmd;
        }

        private static List<CmdItem> GetSetCmdByType(string type, string host, int port)
        {
            List<CmdItem> lstCmd = [];
            lstCmd.Add(new()
            {
                Cmd = "gsettings",
                Arguments = $"set org.gnome.system.proxy.{type} host {host}",
            });

            lstCmd.Add(new()
            {
                Cmd = "gsettings",
                Arguments = $"set org.gnome.system.proxy.{type} port {port}",
            });

            return lstCmd;
        }

        private static List<CmdItem> GetUnsetCmds()
        {
            //TODO KDE
            List<CmdItem> lstCmd = [];

            lstCmd.Add(new CmdItem()
            {
                Cmd = "gsettings",
                Arguments = "set org.gnome.system.proxy mode none"
            });

            return lstCmd;
        }
    }
}