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

        // Besides the elevated wrapper we know the pid of, also look for cores still running
        // from our bin folder: a core left over by a previous run was re-parented to init, so
        // it is not part of the tree of _linuxSudoPid any more but still holds the local port.
        var pids = new List<int>() { _linuxSudoPid };
        pids.AddRange(await Utils.GetPidsByCmdLine(Utils.GetCoreBinFolderPath()));
        _linuxSudoPid = -1;

        await KillProcessesAsLinuxSudo([.. pids.Distinct()]);
    }

    /// <summary>
    ///     Kills the given processes and all their descendants through the kill_as_sudo script.
    /// </summary>
    public async Task KillProcessesAsLinuxSudo(List<int> pids)
    {
        pids.RemoveAll(x => x <= 0);
        if (pids.Count == 0)
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
            var arg = new List<string>() { "-c", $"sudo -S {shFilePath} {string.Join(" ", pids)}" };
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await Cli.Wrap(Global.LinuxBash)
                .WithArguments(arg)
                .WithStandardInputPipe(PipeSource.FromString(AppManager.Instance.LinuxSudoPwd))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(timeoutCts.Token);

            await UpdateFunc(false, result.StandardOutput.ToString());

            if (result.ExitCode != 0)
            {
                // The elevated core may still be running, so keep the reason in the log
                Logging.SaveLog($"{_tag} Failed to kill processes {string.Join(",", pids)}, exit code {result.ExitCode}: {result.StandardError}");
            }

            await Task.Delay(1000); // Wait for a second to ensure the process is killed
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
