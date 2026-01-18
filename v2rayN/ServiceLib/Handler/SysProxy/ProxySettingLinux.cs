namespace ServiceLib.Handler.SysProxy;

public static class ProxySettingLinux
{
    private static readonly string _proxySetFileName = $"{AppConfig.ProxySetLinuxShellFileName.Replace(AppConfig.NamespaceSample, "")}.sh";

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
        var customSystemProxyScriptPath = AppManager.Instance.Config.SystemProxyItem?.CustomSystemProxyScriptPath;
        var fileName = (customSystemProxyScriptPath.IsNotEmpty() && File.Exists(customSystemProxyScriptPath))
            ? customSystemProxyScriptPath
            : await FileUtils.CreateLinuxShellFile(_proxySetFileName, EmbedUtils.GetEmbedText(AppConfig.ProxySetLinuxShellFileName), false);

        // TODO: temporarily notify which script is being used
        NoticeManager.Instance.SendMessage(fileName);

        await Utils.GetCliWrapOutput(fileName, args);
    }
}
