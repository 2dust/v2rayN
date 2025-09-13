using System.Diagnostics;
using System.Text;
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

    public async Task<Process?> RunProcessAsLinuxSudo(string fileName, CoreInfo coreInfo, string configPath)
    {
        StringBuilder sb = new();
        sb.AppendLine("#!/bin/bash");
        var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, Utils.GetBinConfigPath(configPath).AppendQuotes())}";
        sb.AppendLine($"sudo -S {cmdLine}");
        var shFilePath = await FileManager.CreateLinuxShellFile("run_as_sudo.sh", sb.ToString(), true);

        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = shFilePath,
                Arguments = "",
                WorkingDirectory = Utils.GetBinConfigPath(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            }
        };

        void dataHandler(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.IsNotEmpty())
            {
                _ = UpdateFunc(false, e.Data + Environment.NewLine);
            }
        }

        proc.OutputDataReceived += dataHandler;
        proc.ErrorDataReceived += dataHandler;

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await Task.Delay(10);
        await proc.StandardInput.WriteLineAsync(AppManager.Instance.LinuxSudoPwd);

        await Task.Delay(100);
        if (proc is null or { HasExited: true })
        {
            throw new Exception(ResUI.FailedToRunCore);
        }

        _linuxSudoPid = proc.Id;

        return proc;
    }

    public async Task KillProcessAsLinuxSudo()
    {
        if (_linuxSudoPid < 0)
        {
            return;
        }

        try
        {
            var shellFileName = Utils.IsOSX() ? Global.KillAsSudoOSXShellFileName : Global.KillAsSudoLinuxShellFileName;
            var shFilePath = await FileManager.CreateLinuxShellFile("kill_as_sudo.sh", EmbedUtils.GetEmbedText(shellFileName), true);
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
    }
}
