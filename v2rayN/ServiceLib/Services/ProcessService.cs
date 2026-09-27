namespace ServiceLib.Services;

public class ProcessService : IDisposable
{
    private const int RecentOutputLimit = 20;
    private readonly Process _process;
    private readonly Func<bool, string, Task>? _updateFunc;
    private readonly Queue<string> _recentOutput = new();
    private bool _isDisposed;

    public int Id => _process.Id;
    public IntPtr Handle => _process.Handle;
    public bool HasExited => _process.HasExited;

    /// <summary>
    ///     The last lines written by the process. The output is only shown in the UI, so this is
    ///     what tells why a core refused to start after the log file has been read.
    /// </summary>
    public string GetRecentOutput()
    {
        lock (_recentOutput)
        {
            return string.Join(Environment.NewLine, _recentOutput);
        }
    }

    public ProcessService(
        string fileName,
        string arguments,
        string workingDirectory,
        bool displayLog,
        bool redirectInput,
        Dictionary<string, string>? environmentVars,
        Func<bool, string, Task>? updateFunc)
    {
        _updateFunc = updateFunc;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = redirectInput,
                RedirectStandardOutput = displayLog,
                RedirectStandardError = displayLog,
                CreateNoWindow = true,
                StandardOutputEncoding = displayLog ? Encoding.UTF8 : null,
                StandardErrorEncoding = displayLog ? Encoding.UTF8 : null,
            },
            EnableRaisingEvents = true
        };

        if (environmentVars != null)
        {
            foreach (var kv in environmentVars)
            {
                _process.StartInfo.Environment[kv.Key] = kv.Value;
            }
        }

        if (displayLog)
        {
            RegisterEventHandlers();
        }
    }

    public async Task StartAsync(string pwd = null)
    {
        _process.Start();

        if (_process.StartInfo.RedirectStandardOutput)
        {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        if (_process.StartInfo.RedirectStandardInput)
        {
            await Task.Delay(10);
            await _process.StandardInput.WriteLineAsync(pwd);
        }
    }

    public async Task StopAsync()
    {
        if (_process.HasExited)
        {
            return;
        }

        try
        {
            if (_process.StartInfo.RedirectStandardOutput)
            {
                try
                {
                    _process.CancelOutputRead();
                }
                catch { }
                try
                {
                    _process.CancelErrorRead();
                }
                catch { }
            }

            try
            {
                if (Utils.IsNonWindows())
                {
                    _process.Kill(true);
                }
            }
            catch { }

            try
            {
                _process.Kill();
            }
            catch { }

            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            await _updateFunc?.Invoke(true, ex.Message);
        }
    }

    private void RegisterEventHandlers()
    {
        void dataHandler(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.IsNotEmpty())
            {
                lock (_recentOutput)
                {
                    _recentOutput.Enqueue(e.Data);
                    while (_recentOutput.Count > RecentOutputLimit)
                    {
                        _recentOutput.Dequeue();
                    }
                }
                _ = _updateFunc?.Invoke(false, e.Data + Environment.NewLine);
            }
        }

        _process.OutputDataReceived += dataHandler;
        _process.ErrorDataReceived += dataHandler;

        _process.Exited += (s, e) =>
        {
            try
            {
                _process.OutputDataReceived -= dataHandler;
                _process.ErrorDataReceived -= dataHandler;
            }
            catch
            {
            }
        };
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.CancelOutputRead();
                }
                catch { }
                try
                {
                    _process.CancelErrorRead();
                }
                catch { }

                _process.Kill();
            }

            _process.Dispose();
        }
        catch (Exception ex)
        {
            _updateFunc?.Invoke(true, ex.Message);
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
