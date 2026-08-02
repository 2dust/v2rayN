using CliWrap;
using CliWrap.Buffered;

namespace ServiceLib.Manager;

public class CoreAdminManager
{
    private static readonly Lazy<CoreAdminManager> _instance = new(() => new());
    public static CoreAdminManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;
    private readonly List<int> _linuxSudoPids = new();
    private readonly object _linuxSudoPidsLock = new();
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
        TrackSudoPid(procService.Id);

        return procService;
    }

    /// <summary>
    ///     Remembers a root-owned launcher PID so it can be terminated later.
    ///     A TUN launch elevates the main core and then the pre core, so this runs more than once
    ///     per launch and every PID must be kept: the app itself runs unelevated and can only reach
    ///     these processes through the sudo helper script.
    /// </summary>
    public void TrackSudoPid(int pid)
    {
        if (pid < 0)
        {
            return;
        }

        lock (_linuxSudoPidsLock)
        {
            _linuxSudoPids.Add(pid);
        }
    }

    /// <summary>
    ///     Returns every tracked PID and clears the list, most recently started first so the pre
    ///     core is torn down before the main core it depends on.
    /// </summary>
    public IReadOnlyList<int> DrainSudoPids()
    {
        lock (_linuxSudoPidsLock)
        {
            var pids = new List<int>(_linuxSudoPids);
            pids.Reverse();
            _linuxSudoPids.Clear();
            return pids;
        }
    }

    public async Task KillProcessAsLinuxSudo()
    {
        var pids = DrainSudoPids();
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

            foreach (var pid in pids)
            {
                // Each PID is terminated independently: one failure must not strand the others,
                // since a surviving root-owned core keeps holding the TUN device and its routes.
                try
                {
                    var arg = new List<string>() { "-c", $"sudo -S {shFilePath} {pid}" };
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
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
