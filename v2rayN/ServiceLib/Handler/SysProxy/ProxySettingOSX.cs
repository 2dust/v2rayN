namespace ServiceLib.Handler.SysProxy
{
    public class ProxySettingOSX
    {
        private static readonly string _proxySetFileName = $"{Global.ProxySetOSXShellFileName.Replace(Global.NamespaceSample, "")}.sh";

        public static async Task SetProxy(string host, int port, string exceptions)
        {
            List<string> args = ["set", host, port.ToString()];
            if (exceptions.IsNotEmpty())
            {
                args.AddRange(exceptions.Split(','));
            }

            await ExecCmd(args);
        }

        public static async Task UnsetProxy()
        {
            List<string> args = ["clear"];
            await ExecCmd(args);
        }

        private static async Task ExecCmd(List<string> args)
        {
            var fileName = Utils.GetBinConfigPath(_proxySetFileName);
            if (!File.Exists(fileName))
            {
                var contents = EmbedUtils.GetEmbedText(Global.ProxySetOSXShellFileName);
                await File.AppendAllTextAsync(fileName, contents);
            }

            await Utils.SetLinuxChmod(fileName);

            await Utils.GetCliWrapOutput(Global.LinuxBash, args);
        }
    }
}
