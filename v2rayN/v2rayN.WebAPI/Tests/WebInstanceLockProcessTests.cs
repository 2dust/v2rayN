using System.Diagnostics;
using v2rayN.WebAPI.Launcher;

namespace v2rayN.WebAPI.Tests;

public class WebInstanceLockProcessTests
{
    [Test]
    public async Task OwnerReadsAcrossProcessesAndInsideTheOwnerPreserveExclusiveOwnership()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "owner file-å-锁.lock");
        await using var owner = LockProcess.Start("hold", path);
        await (await owner.ReadLineAsync()).Should().BeEqualTo($"locked:{owner.Id}");
        await AssertAcquisition(path, acquired: false);

        await using (var reader = LockProcess.Start("read-owner", path))
        {
            await (await reader.ReadLineAsync()).Should().BeEqualTo($"owner:{owner.Id}");
            await (await reader.WaitForExitAsync()).Should().BeEqualTo(0);
        }
        for (var i = 0; i < 10; i++)
        {
            await WebInstanceLock.ReadOwnerProcessId(path).Should().BeEqualTo(owner.Id);
        }
        await AssertAcquisition(path, acquired: false);

        await owner.SendAsync("read-owner");
        await (await owner.ReadLineAsync()).Should().BeEqualTo($"owner:{owner.Id}");
        await AssertAcquisition(path, acquired: false);
        await WebInstanceLock.ReadOwnerProcessId(path).Should().BeEqualTo(owner.Id);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LockCanBeReacquiredAfterOwnerExit(bool terminateOwner)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "instance.lock");
        await using var owner = LockProcess.Start("hold", path);
        await (await owner.ReadLineAsync()).Should().BeEqualTo($"locked:{owner.Id}");
        await AssertAcquisition(path, acquired: false);
        if (terminateOwner)
        {
            await owner.KillAsync();
        }
        else
        {
            await owner.SendAsync("release");
            await (await owner.WaitForExitAsync()).Should().BeEqualTo(0);
        }

        await WebInstanceLock.IsHeld(path).Should().BeFalse();
        await AssertAcquisition(path, acquired: true);
        await using var replacement = LockProcess.Start("hold", path);
        await (await replacement.ReadLineAsync()).Should().BeEqualTo($"locked:{replacement.Id}");
        await WebInstanceLock.ReadOwnerProcessId(path).Should().BeEqualTo(replacement.Id);
        await AssertAcquisition(path, acquired: false);
    }

    [Test]
    public async Task ReadingAMissingOwnerDoesNotCreateALockFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "missing.lock");
        await WebInstanceLock.ReadOwnerProcessId(path).Should().BeNull();
        await File.Exists(path).Should().BeFalse();
    }

    [Test]
    [Arguments("not-a-pid")]
    [Arguments("0")]
    [Arguments("-1")]
    [Arguments("2147483648")]
    public async Task ReadingAnInvalidOwnerReturnsNoProcessId(string contents)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "instance.lock");
        await File.WriteAllTextAsync(path, contents);
        await WebInstanceLock.ReadOwnerProcessId(path).Should().BeNull();
    }

    private static async Task AssertAcquisition(string path, bool acquired)
    {
        await using var contender = LockProcess.Start("try-acquire", path);
        await (await contender.ReadLineAsync()).Should().BeEqualTo(acquired ? "acquired" : "busy");
        await (await contender.WaitForExitAsync()).Should().BeEqualTo(acquired ? 0 : 2);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("v2rayn-instance-lock-tests-");
        public string Path => _directory.FullName;
        public void Dispose() => _directory.Delete(recursive: true);
    }
    private sealed class LockProcess : IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
        private readonly Process _process;
        private readonly Task<string> _standardError;

        private LockProcess(Process process)
        {
            _process = process;
            _standardError = process.StandardError.ReadToEndAsync();
        }

        public int Id => _process.Id;

        public static LockProcess Start(string mode, string path)
        {
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            var start = new ProcessStartInfo(string.IsNullOrWhiteSpace(host) ? "dotnet" : host)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add("--runtimeconfig");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, typeof(WebInstanceLockProcessTests).Assembly.GetName().Name + ".runtimeconfig.json"));
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ProcessHelper", "InstanceLockProcessHelper.dll"));
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(path);
            return new LockProcess(Process.Start(start) ?? throw new InvalidOperationException("Unable to start the instance-lock helper."));
        }

        public async Task<string> ReadLineAsync()
        {
            var line = await _process.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
            return line ?? throw new InvalidOperationException($"Instance-lock helper exited without a reply: {await _standardError.WaitAsync(Timeout)}");
        }

        public async Task SendAsync(string command)
        {
            await _process.StandardInput.WriteLineAsync(command);
            await _process.StandardInput.FlushAsync();
        }

        public async Task<int> WaitForExitAsync()
        {
            await _process.WaitForExitAsync().WaitAsync(Timeout);
            return _process.ExitCode;
        }

        public async Task KillAsync()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The helper may exit between the check and Kill.
            }
            await _process.WaitForExitAsync().WaitAsync(Timeout);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await KillAsync();
                await _standardError.WaitAsync(Timeout);
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
