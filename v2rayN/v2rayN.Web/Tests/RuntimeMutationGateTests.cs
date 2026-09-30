using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class RuntimeMutationGateTests
{
    [Test]
    public async Task ConcurrentConfigWritesSerializeTheFixedTemporaryFile()
    {
        var gate = new RuntimeMutationGate();
        var configPath = Path.Combine(Path.GetTempPath(), $"v2rayn-web-config-{Guid.NewGuid():N}.json");
        var tempPath = $"{configPath}_temp";
        var sharedConfig = new List<int>();
        var activeWriters = 0;
        var maximumConcurrentWriters = 0;

        try
        {
            var writers = Enumerable.Range(0, 24).Select(value => gate.RunAsync(async () =>
            {
                var active = Interlocked.Increment(ref activeWriters);
                UpdateMaximum(ref maximumConcurrentWriters, active);
                try
                {
                    sharedConfig.Add(value);
                    await File.WriteAllTextAsync(tempPath, System.Text.Json.JsonSerializer.Serialize(sharedConfig));
                    await Task.Delay(2);
                    File.Move(tempPath, configPath, overwrite: true);
                }
                finally
                {
                    Interlocked.Decrement(ref activeWriters);
                }
            }));

            await Task.WhenAll(writers);

            var persisted = System.Text.Json.JsonSerializer.Deserialize<int[]>(await File.ReadAllTextAsync(configPath));
            await maximumConcurrentWriters.Should().BeEqualTo(1);
            await (persisted is not null).Should().BeTrue();
            await (persisted?.Order().SequenceEqual(Enumerable.Range(0, 24)) == true).Should().BeTrue();
            await File.Exists(tempPath).Should().BeFalse();
        }
        finally
        {
            File.Delete(configPath);
            File.Delete(tempPath);
        }
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (current >= value || Interlocked.CompareExchange(ref maximum, value, current) == current)
            {
                return;
            }
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public async Task SaveFailureCannotBeTreatedAsSuccess(int saveResult)
    {
        var failed = false;
        try
        {
            await V2rayRuntime.EnsureConfigSaveSucceededAsync(() => Task.FromResult(saveResult));
        }
        catch (IOException)
        {
            failed = true;
        }

        await failed.Should().BeTrue();
    }
}
