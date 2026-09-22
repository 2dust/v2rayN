namespace ServiceLib.Tests.Services;

public class ProcessServiceTests
{
    [Test]
    public async Task StoppingTestProcessDeletesConfigAndReleasesPort()
    {
        var path = Path.Combine(Path.GetTempPath(), $"v2rayN-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{}");
        var released = 0;
        using var process = new ProcessService(
            fileName: OperatingSystem.IsWindows() ? "ping.exe" : "sleep",
            arguments: OperatingSystem.IsWindows() ? "-n 30 127.0.0.1" : "30",
            workingDirectory: Path.GetTempPath(),
            displayLog: false,
            redirectInput: false,
            environmentVars: null,
            updateFunc: null);

        try
        {
            await process.StartAsync();
            process.OwnTemporaryResources(path, () => Interlocked.Increment(ref released));

            await process.StopAsync();
            await process.HasExited.Should().BeTrue();
            await File.Exists(path).Should().BeFalse();
            await released.Should().BeEqualTo(1);

            await process.StopAsync();
            await released.Should().BeEqualTo(1);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
