namespace ServiceLib.Services;

public class ProcessService : IDisposable
{
    private readonly Process _process;
    private readonly Func<bool, string, Task>? _updateFunc;
    private bool _isDisposed;
    private string? _temporaryConfigPath;
    private Action? _releaseTemporaryPort;

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

    public void OwnTemporaryResources(string configPath, Action? releasePort = null)
    {
        _temporaryConfigPath = configPath;
        _releaseTemporaryPort = releasePort;
    }

    public async Task StopAsync()
    {
        var isTestProcess = _temporaryConfigPath is not null;
        try
        {
            if (_process.HasExited)
            {
                return;
            }
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
                if (isTestProcess || Utils.IsNonWindows())
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

            if (isTestProcess)
            {
                using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _process.WaitForExitAsync(exitCts.Token);
            }
            else
            {
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(ProcessService), ex);
            if (_updateFunc is not null)
            {
                await _updateFunc(true, ex.Message);
            }
        }
        finally
        {
            CleanupTemporaryResources();
        }
    }

    private void CleanupTemporaryResources()
    {
        var path = Interlocked.Exchange(ref _temporaryConfigPath, null);
        var release = Interlocked.Exchange(ref _releaseTemporaryPort, null);
        try
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(ProcessService), ex);
        }
        finally
        {
            release?.Invoke();
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
        finally
        {
            CleanupTemporaryResources();
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
