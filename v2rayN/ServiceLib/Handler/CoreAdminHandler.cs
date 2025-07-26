using System.Diagnostics;
using System.Text;
using CliWrap;

namespace ServiceLib.Handler;

public class CoreAdminHandler
{
    private static readonly Lazy<CoreAdminHandler> _instance = new(() => new());
    public static CoreAdminHandler Instance => _instance.Value;
    private Config _config;
    private Action<bool, string>? _updateFunc;
    private int _linuxSudoPid = -1;

    public async Task Init(Config config, Action<bool, string> updateFunc)
    {
        if (_config != null)
        {
            return;
        }
        _config = config;
        _updateFunc = updateFunc;
    }

    private void UpdateFunc(bool notify, string msg)
    {
        _updateFunc?.Invoke(notify, msg);
    }

    public async Task<Process?> RunProcessAsLinuxSudo(string fileName, CoreInfo coreInfo, string configPath)
    {
        var cmdLine = $"{fileName.AppendQuotes()} {string.Format(coreInfo.Arguments, Utils.GetBinConfigPath(configPath).AppendQuotes())}";
        var shFilePath = await CreateLinuxShellFile(cmdLine, "run_as_sudo.sh");

        Process process = new()
        {
            StartInfo = new()
            {
                FileName = shFilePath,
                WorkingDirectory = Utils.GetBinConfigPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data.IsNotEmpty())
            {
                UpdateFunc(false, e.Data + Environment.NewLine);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data.IsNotEmpty())
            {
                UpdateFunc(false, e.Data + Environment.NewLine);
            }
        };

        process.Start();

        process.StandardInput.WriteLine(AppHandler.Instance.LinuxSudoPwd);
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _linuxSudoPid = process.Id;

        try
        {
            await Task.Delay(5000); // Sudo exit on wrong password takes 2-4 sec.
            if (process.HasExited)
                throw new InvalidOperationException("Process ended too early, likely due to an incorrect sudo password.");
        }
        catch (Exception ex)
        {
            _linuxSudoPid = -1;
            AppHandler.Instance.LinuxSudoPwd = "";
            throw new Exception(ResUI.FailedToRunCore, ex);
        }

        return process;
    }

    public async Task KillProcessAsLinuxSudo()
    {
        if (_linuxSudoPid < 0)
        {
            return;
        }

        var cmdLine = $"pkill -P {_linuxSudoPid} ; kill {_linuxSudoPid}";
        var shFilePath = await CreateLinuxShellFile(cmdLine, "kill_as_sudo.sh");

        await Cli.Wrap(shFilePath)
           .WithStandardInputPipe(PipeSource.FromString(AppHandler.Instance.LinuxSudoPwd))
           .ExecuteAsync();

        _linuxSudoPid = -1;
    }

    private async Task<string> CreateLinuxShellFile(string cmdLine, string fileName)
    {
        var shFilePath = Utils.GetBinConfigPath(fileName);
        File.Delete(shFilePath);

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/sh");
        if (Utils.IsAdministrator())
        {
            sb.AppendLine($"{cmdLine}");
        }
        else
        {
            sb.AppendLine($"sudo -S -k -p '' -- {cmdLine}");
        }

        await File.WriteAllTextAsync(shFilePath, sb.ToString());
        await Utils.SetLinuxChmod(shFilePath);

        return shFilePath;
    }
}
