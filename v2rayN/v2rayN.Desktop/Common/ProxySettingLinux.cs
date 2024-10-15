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
            var isKde = IsKde(out var configDir);
            List<string> lstType = ["", "http", "https", "socks", "ftp"];
            List<CmdItem> lstCmd = [];

            if (isKde)
            {
                foreach (var type in lstType)
                {
                    lstCmd.AddRange(GetSetCmd4Kde(type, host, port, configDir));
                }
            }
            else
            {
                foreach (var type in lstType)
                {
                    lstCmd.AddRange(GetSetCmd4Gnome(type, host, port));
                }
            }
            return lstCmd;
        }

        private static List<CmdItem> GetUnsetCmds()
        {
            var isKde = IsKde(out var configDir);
            List<CmdItem> lstCmd = [];

            if (isKde)
            {
                lstCmd.Add(new CmdItem()
                {
                    Cmd = "kwriteconfig5",
                    Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", "ProxyType", "0"]
                });
            }
            else
            {
                lstCmd.Add(new CmdItem()
                {
                    Cmd = "gsettings",
                    Arguments = ["set", "org.gnome.system.proxy", "mode", "none"]
                });
            }

            return lstCmd;
        }

        private static List<CmdItem> GetSetCmd4Kde(string type, string host, int port, string configDir)
        {
            List<CmdItem> lstCmd = [];

            if (type.IsNullOrEmpty())
            {
                lstCmd.Add(new()
                {
                    Cmd = "kwriteconfig5",
                    Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", "ProxyType", "1"]
                });
            }
            else
            {
                var type2 = type.Equals("https") ? "http" : type;
                lstCmd.Add(new CmdItem()
                {
                    Cmd = "kwriteconfig5",
                    Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", $"{type}Proxy", $"{type2}://{host}:{port}"]
                });
            }

            return lstCmd;
        }

        private static List<CmdItem> GetSetCmd4Gnome(string type, string host, int port)
        {
            List<CmdItem> lstCmd = [];

            if (type.IsNullOrEmpty())
            {
                lstCmd.Add(new()
                {
                    Cmd = "gsettings",
                    Arguments = ["set", "org.gnome.system.proxy", "mode", "manual"]
                });
            }
            else
            {
                lstCmd.Add(new()
                {
                    Cmd = "gsettings",
                    Arguments = ["set", $"org.gnome.system.proxy.{type}", "host", host]
                });

                lstCmd.Add(new()
                {
                    Cmd = "gsettings",
                    Arguments = ["set", $"org.gnome.system.proxy.{type}", "port", $"{port}"]
                });
            }

            return lstCmd;
        }

        private static bool IsKde(out string configDir)
        {
            configDir = "/home";
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            var isKde = string.Equals(desktop, "KDE", StringComparison.OrdinalIgnoreCase);
            if (isKde)
            {
                var homeDir = Environment.GetEnvironmentVariable("HOME");
                if (homeDir != null)
                {
                    configDir = Path.Combine(homeDir, ".config");
                }
            }

            return isKde;
        }
    }
}