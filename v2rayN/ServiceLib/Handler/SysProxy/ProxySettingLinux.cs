namespace ServiceLib.Handler.SysProxy
{
	public class ProxySettingLinux
	{
		public static async Task SetProxy(string host, int port, string exceptions)
		{
			var lstCmd = GetSetCmds(host, port, exceptions);

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

		private static List<CmdItem> GetSetCmds(string host, int port, string exceptions)
		{
			var isKde = IsKde(out var configDir);
			List<string> lstType = ["", "http", "https", "socks", "ftp"];
			List<CmdItem> lstCmd = [];

			//GNOME
			foreach (var type in lstType)
			{
				lstCmd.AddRange(GetSetCmd4Gnome(type, host, port));
			}
			if (exceptions.IsNotEmpty())
			{
				lstCmd.AddRange(GetSetCmd4Gnome("exceptions", exceptions, 0));
			}

			if (isKde)
			{
				foreach (var type in lstType)
				{
					lstCmd.AddRange(GetSetCmd4Kde(type, host, port, configDir));
				}
				if (exceptions.IsNotEmpty())
				{
					lstCmd.AddRange(GetSetCmd4Kde("exceptions", exceptions, 0, configDir));
				}

				// Notify system to reload
				lstCmd.Add(new CmdItem()
				{
					Cmd = "dbus-send",
					Arguments = ["--type=signal", "/KIO/Scheduler", "org.kde.KIO.Scheduler.reparseSlaveConfiguration", "string:''"]
				});
			}
			return lstCmd;
		}

		private static List<CmdItem> GetUnsetCmds()
		{
			var isKde = IsKde(out var configDir);
			List<CmdItem> lstCmd = [];

			//GNOME
			lstCmd.Add(new CmdItem()
			{
				Cmd = "gsettings",
				Arguments = ["set", "org.gnome.system.proxy", "mode", "none"]
			});

			if (isKde)
			{
				lstCmd.Add(new CmdItem()
				{
					Cmd = GetKdeVersion(),
					Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", "ProxyType", "0"]
				});

				// Notify system to reload
				lstCmd.Add(new CmdItem()
				{
					Cmd = "dbus-send",
					Arguments = ["--type=signal", "/KIO/Scheduler", "org.kde.KIO.Scheduler.reparseSlaveConfiguration", "string:''"]
				});
			}
			return lstCmd;
		}

		private static List<CmdItem> GetSetCmd4Kde(string type, string host, int port, string configDir)
		{
			List<CmdItem> lstCmd = [];
			var cmd = GetKdeVersion();

			if (type.IsNullOrEmpty())
			{
				lstCmd.Add(new()
				{
					Cmd = cmd,
					Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", "ProxyType", "1"]
				});
			}
			else if (type == "exceptions")
			{
				lstCmd.Add(new()
				{
					Cmd = cmd,
					Arguments = ["--file", $"{configDir}/kioslaverc", "--group", "Proxy Settings", "--key", "NoProxyFor", host]
				});
			}
			else
			{
				var type2 = type.Equals("https") ? "http" : type;
				lstCmd.Add(new CmdItem()
				{
					Cmd = cmd,
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
			else if (type == "exceptions")
			{
				lstCmd.Add(new()
				{
					Cmd = "gsettings",
					Arguments = ["set", $"org.gnome.system.proxy", "ignore-hosts", JsonUtils.Serialize(host.Split(','), false)]
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
			var desktop2 = Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP");
			var isKde = string.Equals(desktop, "KDE", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(desktop, "plasma", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(desktop2, "KDE", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(desktop2, "plasma", StringComparison.OrdinalIgnoreCase);
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

		private static string GetKdeVersion()
		{
			var ver = Environment.GetEnvironmentVariable("KDE_SESSION_VERSION") ?? "0";
			return ver switch
			{
				"6" => "kwriteconfig6",
				_ => "kwriteconfig5"
			};
		}
	}
}
