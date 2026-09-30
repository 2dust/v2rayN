using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class LogBufferTests
{
    [Test]
    public async Task HighVolumeRetentionRemainsBoundedAndClearDropsAllEntries()
    {
        var logs = new LogBuffer();
        for (var index = 0; index < 50_000; index++)
        {
            logs.Add("core", $"line {index}");
        }

        var retained = logs.Recent(2000);
        await retained.Count.Should().BeEqualTo(2000);
        await retained[^1].Message.Should().BeEqualTo("line 49999");

        logs.Clear();
        await logs.Recent(2000).Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task OversizedLogMessagesAreTruncatedAtTheBufferBoundary()
    {
        var logs = new LogBuffer();
        var entry = logs.Add("core", new string('x', 100_000));

        await entry.Message.Length.Should().BeLessThanOrEqualTo(16 * 1024);
        await entry.Message.Should().Contain("log entry truncated");
    }

    [Test]
    public async Task CharacterRetentionIsBoundedIndependentlyOfEntryCount()
    {
        var logs = new LogBuffer();
        var message = new string('x', 10_000);
        for (var index = 0; index < 300; index++) logs.Add("core", message);

        var retained = logs.Recent(2000);
        await retained.Count.Should().BeLessThan(2000);
        await retained.Sum(item => item.Message.Length + item.Source.Length)
            .Should().BeLessThanOrEqualTo(1_000_000);
    }

    [Test]
    public async Task ClearingLogsAdvancesGenerationForRejectingStaleRealtimeEntries()
    {
        var logs = new LogBuffer();
        var previous = logs.Add("core", "old entry");
        var generation = logs.Clear();
        var current = logs.Add("core", "new entry");

        await previous.Generation.Should().BeEqualTo(0);
        await generation.Should().BeEqualTo(1);
        await current.Generation.Should().BeEqualTo(1);
    }
}
