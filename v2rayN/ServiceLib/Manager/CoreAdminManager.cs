using CliWrap;
using CliWrap.Buffered;

namespace ServiceLib.Manager;

public class CoreAdminManager
{
    private static readonly Lazy<CoreAdminManager> _instance = new(() => new());
    public static CoreAdminManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;
    private int _linuxSudoPid = -1;
    private const string _tag = "CoreAdminHandler";

    public async Task Init(Config config, Func<bool, string, Task> updateFunc)
    {
        if (_config != null)
        {
            return;
        }
        _config = config;
        _updateFunc = updateFunc;

        await Task.CompletedTask;
    }

    private async Task UpdateFunc(bool notify, string msg)
    {
        await _updateFunc?.Invoke(notify, msg);
    }

    public async Task<ProcessService?> RunProcessAsLinuxSudo(string fileName, CoreInfo coreInfo, string configPath)
    {
        var absoluteConfigPath = Utils.GetBinConfigPath(configPath);
        var manageMacOSTunDns = Utils.IsMacOS()
            && coreInfo.CoreType == ECoreType.sing_box
            && HasDnsHijackAction(absoluteConfigPath);

        StringBuilder sb = new();
        sb.AppendLine("#!/bin/bash");
        var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, absoluteConfigPath.AppendQuotes())}";
        sb.AppendLine($"exec sudo -S -- {cmdLine}");
        var shFilePath = await FileUtils.CreateLinuxShellFile("run_as_sudo.sh", sb.ToString(), true);

        var procService = new ProcessService(
            fileName: shFilePath,
            arguments: "",
            workingDirectory: Utils.GetBinConfigPath(),
            displayLog: true,
            redirectInput: true,
            environmentVars: null,
            updateFunc: _updateFunc
        );

        try
        {
            await procService.StartAsync(AppManager.Instance.LinuxSudoPwd);

            if (procService is null or { HasExited: true })
            {
                throw new Exception(ResUI.FailedToRunCore);
            }
            _linuxSudoPid = procService.Id;

            if (manageMacOSTunDns)
            {
                await RunMacOSTunDnsScript("set");
            }

            return procService;
        }
        catch
        {
            if (Utils.IsMacOS())
            {
                await RunMacOSTunDnsScript("restore");
            }
            throw;
        }
    }

    public async Task KillProcessAsLinuxSudo()
    {
        if (_linuxSudoPid < 0)
        {
            if (Utils.IsMacOS())
            {
                await RunMacOSTunDnsScript("restore");
            }
            return;
        }

        try
        {
            var shellFileName = Utils.IsMacOS() ? Global.KillAsSudoOSXShellFileName : Global.KillAsSudoLinuxShellFileName;
            var shFilePath = await FileUtils.CreateLinuxShellFile("kill_as_sudo.sh", EmbedUtils.GetEmbedText(shellFileName), true);
            if (shFilePath.Contains(' '))
            {
                shFilePath = shFilePath.AppendQuotes();
            }
            var arg = new List<string>() { "-c", $"sudo -S {shFilePath} {_linuxSudoPid}" };
            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(arg)
                .WithStandardInputPipe(PipeSource.FromString(AppManager.Instance.LinuxSudoPwd))
                .ExecuteBufferedAsync();

            await UpdateFunc(false, result.StandardOutput.ToString());
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        _linuxSudoPid = -1;
        if (Utils.IsMacOS())
        {
            await RunMacOSTunDnsScript("restore");
        }
    }

    private static bool HasDnsHijackAction(string configPath)
    {
        try
        {
            var rules = JsonNode.Parse(File.ReadAllText(configPath))?["route"]?["rules"]?.AsArray();
            return rules?.Any(rule =>
                string.Equals(rule?["action"]?.GetValue<string>(), "hijack-dns", StringComparison.Ordinal)) == true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunMacOSTunDnsScript(string action)
    {
        try
        {
            var scriptPath = await FileUtils.CreateLinuxShellFile(
                "tun_dns_osx.sh",
                EmbedUtils.GetEmbedText(Global.TunDnsOSXShellFileName),
                false);
            var statePath = Utils.GetConfigPath("macos_tun_dns_state");
            var result = await Cli.Wrap("/usr/bin/sudo")
                .WithArguments(["-S", "--", scriptPath, action, statePath])
                .WithStandardInputPipe(PipeSource.FromString(AppManager.Instance.LinuxSudoPwd + Environment.NewLine))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                Logging.SaveLog($"macOS TUN DNS script failed: {result.StandardError}");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
