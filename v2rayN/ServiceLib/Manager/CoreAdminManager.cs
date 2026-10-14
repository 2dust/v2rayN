using CliWrap;
using CliWrap.Buffered;
using ServiceLib.ViewModels;

namespace ServiceLib.Manager;

public class CoreAdminManager
{
    private static readonly Lazy<CoreAdminManager> _instance = new(() => new());
    public static CoreAdminManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;
    private int _linuxSudoPid = -1;
    private const string _tag = "CoreAdminHandler";

    public static async Task<bool> CheckLinuxSudoPassword(string? password)
    {
        if (password.IsNullOrEmpty() || Utils.IsWindows())
        {
            return false;
        }

        try
        {
            var inputPwd = password.EndsWith('\n') ? password : password + "\n";
            var arg = new List<string>() { "-c", "sudo -S echo SUDO_CHECK" };
            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(arg)
                .WithStandardInputPipe(PipeSource.FromString(inputPwd))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("CheckSudoPassword", ex);
            return false;
        }
    }

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
        _config = AppManager.Instance.Config;

        var isPassValid = AppManager.Instance.LinuxSudoPwd.IsNotEmpty()
            && await CheckLinuxSudoPassword(AppManager.Instance.LinuxSudoPwd);

        if (!isPassValid)
        {
            if (AppManager.Instance.LinuxSudoPwd.IsNotEmpty())
            {
                await UpdateFunc(true, ResUI.SudoIncorrectPasswordTip);
            }
            AppManager.Instance.LinuxSudoPwd = string.Empty;

            var newPwd = await StatusBarViewModel.Instance.PasswordInputInteraction.HandleSafe(RxVoid.Default);
            if (newPwd.IsNullOrEmpty())
            {
                if (_config?.TunModeItem != null)
                {
                    _config.TunModeItem.LinuxSudoPwd = null;
                    _config.TunModeItem.EnableTun = false;
                    await ConfigHandler.SaveConfig(_config);
                }
                StatusBarViewModel.Instance.EnableTun = false;
                throw new Exception(ResUI.SudoIncorrectPasswordTip);
            }
        }

        StringBuilder sb = new();
        sb.AppendLine("#!/bin/bash");
        var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, Utils.GetBinConfigPath(configPath).AppendQuotes())}";

        // Passing environment variables to the sudo command, here it only xray or sing-box.
        if (coreInfo.Environment.Count > 0)
        {
            var envArgs = string.Join(" ", coreInfo.Environment.Where(kv => kv.Value.IsNotEmpty()).Select(kv => $"{kv.Key}={kv.Value.AppendQuotes()}"));
            sb.AppendLine($"exec sudo -S -- env {envArgs} {cmdLine}");
        }
        else
        {
            sb.AppendLine($"exec sudo -S -- {cmdLine}");
        }

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

        await procService.StartAsync(AppManager.Instance.LinuxSudoPwd);

        if (procService is null or { HasExited: true })
        {
            if (!await CheckLinuxSudoPassword(AppManager.Instance.LinuxSudoPwd))
            {
                AppManager.Instance.LinuxSudoPwd = string.Empty;
                if (_config?.TunModeItem != null)
                {
                    _config.TunModeItem.LinuxSudoPwd = null;
                    await ConfigHandler.SaveConfig(_config);
                }
                await UpdateFunc(true, ResUI.SudoIncorrectPasswordTip);
            }
            throw new Exception(ResUI.FailedToRunCore);
        }
        _linuxSudoPid = procService.Id;

        return procService;
    }

    public async Task KillProcessAsLinuxSudo()
    {
        if (_linuxSudoPid < 0)
        {
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

            await Task.Delay(1000); // Wait for a second to ensure the process is killed
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        _linuxSudoPid = -1;
    }
}
