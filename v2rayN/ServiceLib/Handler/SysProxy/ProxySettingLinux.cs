namespace ServiceLib.Handler.SysProxy
{
    public class ProxySettingLinux
    {
        private static readonly string _proxySetFileName = $"{Global.ProxySetLinuxShellFileName.Replace(Global.NamespaceSample, "")}.sh";

        public static async Task SetProxy(string host, int port, string exceptions)
        {
            List<string> args = ["manual", host, port.ToString(), exceptions];
            await ExecCmd(args);
        }

        public static async Task UnsetProxy()
        {
            List<string> args = ["none"];
            await ExecCmd(args);
        }

        private static async Task ExecCmd(List<string> args)
        {
            var fileName = Utils.GetBinConfigPath(_proxySetFileName);
            if (!File.Exists(fileName))
            {
                var contents = EmbedUtils.GetEmbedText(Global.ProxySetLinuxShellFileName);
                await File.AppendAllTextAsync(fileName, contents);

                await Utils.SetLinuxChmod(fileName);
            }

            await Utils.GetCliWrapOutput(fileName, args);
        }
    }
}
