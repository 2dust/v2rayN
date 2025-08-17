namespace ServiceLib.Handler.SysProxy;

public static class ProxySettingLinux
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
        var fileName = await FileManager.CreateLinuxShellFile(_proxySetFileName, EmbedUtils.GetEmbedText(Global.ProxySetLinuxShellFileName), false);

        await Utils.GetCliWrapOutput(fileName, args);
    }
}
