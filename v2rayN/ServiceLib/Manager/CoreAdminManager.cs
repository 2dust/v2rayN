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
        if (password.IsNullOrEmpty() || !Utils.IsLinux())
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

    public static bool IsCorePrivileged(string? fileName)
    {
        if (fileName.IsNullOrEmpty() || !File.Exists(fileName) || !Utils.IsLinux())
        {
            return false;
        }

        try
        {
            var mode = File.GetUnixFileMode(fileName);
            return mode.HasFlag(UnixFileMode.SetUser);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("IsCorePrivileged", ex);
            return false;
        }
    }

    public static async Task<bool> IsCorePrivilegedAsync(string? fileName)
    {
        if (fileName.IsNullOrEmpty() || !File.Exists(fileName) || !Utils.IsLinux())
        {
            return false;
        }

        try
        {
            var mode = File.GetUnixFileMode(fileName);
            if (!mode.HasFlag(UnixFileMode.SetUser))
            {
                return false;
            }

            var cmd = $"[ \"$(stat -c '%u' {fileName.AppendQuotes()} 2>/dev/null || stat -f '%u' {fileName.AppendQuotes()} 2>/dev/null)\" = \"0\" ]";
            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(["-c", cmd])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("IsCorePrivilegedAsync", ex);
            return false;
        }
    }

    public static List<string> GetAllCoreExecFiles()
    {
        var list = new List<string>();
        foreach (var coreInfo in CoreInfoManager.Instance.GetCoreInfo())
        {
            if (coreInfo?.CoreExes == null)
            {
                continue;
            }
            foreach (var name in coreInfo.CoreExes)
            {
                var file = coreInfo.AbsolutePath == true
                    ? Utils.GetBinPath(Utils.GetExeName(name))
                    : Utils.GetBinPath(Utils.GetExeName(name), coreInfo.CoreType.ToString());

                if (file.IsNotEmpty() && File.Exists(file) && !list.Contains(file))
                {
                    list.Add(file);
                }
            }
        }
        return list;
    }

    public static bool AreAllTunCoresPrivileged(string? currentCoreFile = null)
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        if (currentCoreFile.IsNotEmpty() && !IsCorePrivileged(currentCoreFile))
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        if (cores.Count == 0)
        {
            return false;
        }

        return cores.All(IsCorePrivileged);
    }

    public static async Task<bool> AreAllTunCoresPrivilegedAsync(string? currentCoreFile = null)
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        if (currentCoreFile.IsNotEmpty() && !await IsCorePrivilegedAsync(currentCoreFile))
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        if (cores.Count == 0)
        {
            return false;
        }

        foreach (var core in cores)
        {
            if (!await IsCorePrivilegedAsync(core))
            {
                return false;
            }
        }
        return true;
    }

    public static async Task<bool> GrantAllCoresPrivilege(string password)
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        if (cores.Count == 0)
        {
            return true;
        }

        try
        {
            var quotedFiles = string.Join(" ", cores.Select(f => f.AppendQuotes()));
            var inputPwd = password.EndsWith('\n') ? password : password + "\n";
            var cmd = $"sudo -S chown root:root {quotedFiles} && sudo -S chmod u+s {quotedFiles}";

            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(["-c", cmd])
                .WithStandardInputPipe(PipeSource.FromString(inputPwd))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("GrantAllCoresPrivilege", ex);
            return false;
        }
    }

    public static bool IsAnyCorePrivileged()
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        return cores.Any(IsCorePrivileged);
    }

    public static async Task<bool> IsAnyCorePrivilegedAsync()
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        foreach (var core in cores)
        {
            if (await IsCorePrivilegedAsync(core))
            {
                return true;
            }
        }
        return false;
    }

    public static async Task<bool> RevokeAllCoresPrivilege(string? password = null, Func<Task<string?>>? passwordPrompt = null, string? reason = null)
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        var cores = GetAllCoreExecFiles();
        if (cores.Count == 0)
        {
            return true;
        }

        try
        {
            var quotedFiles = string.Join(" ", cores.Select(f => f.AppendQuotes()));
            var revokeScript = $"sudo -n chmod u-s {quotedFiles} && sudo -n chown $(id -u):$(id -g) {quotedFiles} && sudo -n chmod 0755 {quotedFiles}";

            // 1. Try passwordless sudo first (e.g. cached credentials or NOPASSWD)
            var noPassResult = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(["-c", revokeScript])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (noPassResult.ExitCode == 0)
            {
                return true;
            }

            // 2. Obtain password: from argument, or LinuxSudoPwd in memory, or prompt callback
            var pwd = password;
            if (pwd.IsNullOrEmpty())
            {
                pwd = AppManager.Instance.LinuxSudoPwd;
            }
            if (pwd.IsNullOrEmpty() && passwordPrompt != null)
            {
                pwd = await passwordPrompt();
            }
            if (pwd.IsNullOrEmpty() && StatusBarViewModel.Instance != null)
            {
                pwd = await StatusBarViewModel.Instance.PasswordInputInteraction.HandleSafe(reason);
            }

            if (pwd.IsNullOrEmpty())
            {
                return false;
            }

            var inputPwd = pwd.EndsWith('\n') ? pwd : pwd + "\n";
            var sudoCmd = $"sudo -S chmod u-s {quotedFiles} && sudo -S chown $(id -u):$(id -g) {quotedFiles} && sudo -S chmod 0755 {quotedFiles}";

            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(["-c", sudoCmd])
                .WithStandardInputPipe(PipeSource.FromString(inputPwd))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode == 0)
            {
                if (AppManager.Instance.Config?.TunModeItem?.EnableTun == true || AppManager.Instance.Config?.TunModeItem?.PreserveCorePrivileges == true)
                {
                    AppManager.Instance.LinuxSudoPwd = pwd;
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("RevokeAllCoresPrivilege", ex);
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
        var preservePrivileges = _config.TunModeItem?.PreserveCorePrivileges ?? false;

        var isPrivileged = await IsCorePrivilegedAsync(fileName);

        if (!isPrivileged)
        {
            var password = AppManager.Instance.LinuxSudoPwd;
            if (password.IsNullOrEmpty())
            {
                // Core does not have root privileges or lost them, ask user for sudo password
                password = await StatusBarViewModel.Instance.PasswordInputInteraction.HandleSafe((string?)null);
                if (password.IsNullOrEmpty())
                {
                    if (_config?.TunModeItem != null)
                    {
                        _config.TunModeItem.EnableTun = false;
                        await ConfigHandler.SaveConfig(_config);
                    }
                    StatusBarViewModel.Instance.EnableTun = false;
                    throw new Exception(ResUI.SudoIncorrectPasswordTip);
                }

                preservePrivileges = _config.TunModeItem?.PreserveCorePrivileges ?? false;
            }

            if (preservePrivileges)
            {
                var grantSuccess = await GrantAllCoresPrivilege(password);
                if (!grantSuccess)
                {
                    await UpdateFunc(true, ResUI.SudoIncorrectPasswordTip);
                    throw new Exception(ResUI.SudoIncorrectPasswordTip);
                }
                isPrivileged = await IsCorePrivilegedAsync(fileName);
            }
            else
            {
                AppManager.Instance.LinuxSudoPwd = password;
            }
        }

        if (isPrivileged)
        {
            var configArg = coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath;
            var environmentVars = new Dictionary<string, string>();
            foreach (var kv in coreInfo.Environment)
            {
                environmentVars[kv.Key] = string.Format(kv.Value, configArg);
            }

            var procService = new ProcessService(
                fileName: fileName,
                arguments: string.Format(coreInfo.Arguments, configArg),
                workingDirectory: Utils.GetBinConfigPath(),
                displayLog: true,
                redirectInput: false,
                environmentVars: environmentVars,
                updateFunc: _updateFunc
            );

            await procService.StartAsync();

            if (procService is null or { HasExited: true })
            {
                throw new Exception(ResUI.FailedToRunCore);
            }
            _linuxSudoPid = procService.Id;

            return procService;
        }
        else
        {
            StringBuilder sb = new();
            sb.AppendLine("#!/bin/bash");
            var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, Utils.GetBinConfigPath(configPath).AppendQuotes())}";

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
                if (!preservePrivileges)
                {
                    AppManager.Instance.LinuxSudoPwd = null;
                }
                throw new Exception(ResUI.FailedToRunCore);
            }
            _linuxSudoPid = procService.Id;

            return procService;
        }
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

            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments([shFilePath, _linuxSudoPid.ToString()])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode != 0 && AppManager.Instance.LinuxSudoPwd.IsNotEmpty())
            {
                var inputPwd = AppManager.Instance.LinuxSudoPwd.EndsWith('\n') ? AppManager.Instance.LinuxSudoPwd : AppManager.Instance.LinuxSudoPwd + "\n";
                result = await Cli.Wrap(Global.LinuxBash)
                    .WithArguments(["-c", $"sudo -S {shFilePath.AppendQuotes()} {_linuxSudoPid}"])
                    .WithStandardInputPipe(PipeSource.FromString(inputPwd))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();
            }

            await UpdateFunc(false, result.StandardOutput.ToString());
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        _linuxSudoPid = -1;
    }
}
