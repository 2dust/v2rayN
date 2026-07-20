namespace ServiceLib.Services;

public class ProcessService : IDisposable
{
    private readonly Process _process;
    private readonly Func<bool, string, Task>? _updateFunc;
    private bool _isDisposed;

    public int Id => _process.Id;
    public IntPtr Handle => _process.Handle;
    public bool HasExited => _process.HasExited;

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
        int processId;
        try
        {
            processId = _process.Id;
        }
        catch
        {
            processId = 0;
        }

        var processName = Path.GetFileName(_process.StartInfo.FileName);

        if (_process.HasExited)
        {
            await LogLifecycleAsync($"Core stop: {processName}, PID={processId} has already exited.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await LogLifecycleAsync($"Core stop: stopping {processName}, PID={processId}.");

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
                else
                {
                    _process.Kill();
                }
                await LogLifecycleAsync($"Core stop: kill signal sent to PID={processId}.");
            }
            catch (Exception ex)
            {
                await LogLifecycleAsync($"Core stop: failed to send kill signal to PID={processId}: {ex.Message}");
            }

            if (!_process.HasExited)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    await LogLifecycleAsync($"Core stop: timeout after {stopwatch.ElapsedMilliseconds} ms waiting for PID={processId} to exit.");

                    try
                    {
                        if (!_process.HasExited)
                        {
                            _process.Kill();
                            await LogLifecycleAsync($"Core stop: repeated kill signal sent to PID={processId} after timeout.");
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogLifecycleAsync($"Core stop: repeated kill failed for PID={processId}: {ex.Message}");
                    }
                }
            }

            if (_process.HasExited)
            {
                await LogLifecycleAsync($"Core stop: PID={processId} exited after {stopwatch.ElapsedMilliseconds} ms, exit code={_process.ExitCode}.");
            }
            else
            {
                await LogLifecycleAsync($"Core stop: PID={processId} is still running after {stopwatch.ElapsedMilliseconds} ms; restart will continue.");
            }
        }
        catch (Exception ex)
        {
            await LogLifecycleAsync($"Core stop: exception while stopping PID={processId}: {ex.Message}");
            await _updateFunc?.Invoke(true, ex.Message);
        }
    }

    private async Task LogLifecycleAsync(string message)
    {
        Logging.SaveLog(message);
        if (_updateFunc != null)
        {
            await _updateFunc(false, message + Environment.NewLine);
        }
    }

    private void RegisterEventHandlers()
    {
        void dataHandler(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.IsNotEmpty())
            {
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
