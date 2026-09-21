using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class LifecycleTests
{
    [Test]
    public async Task EnabledSettingSurvivesShutdownAndRestoresAfterSerialization()
    {
        var config = new Config();
        var runtime = new Runtime();
        string persisted = "";
        await AppRoutingLifecycle.SetEnabledAsync(config, runtime, c => { persisted = JsonUtils.Serialize(c); return Task.FromResult(0); }, true);
        await runtime.StopAsync(); // Ordinary app shutdown only stops the runtime.
        await config.AppRouting.Enabled.Should().BeTrue();
        var reloaded = JsonUtils.Deserialize<Config>(persisted)!;
        var nextRuntime = new Runtime();
        await AppRoutingLifecycle.RestoreAsync(reloaded, nextRuntime);
        await nextRuntime.IsEnabled.Should().BeTrue();
        await nextRuntime.Starts.Should().BeEqualTo(1);
        await AppRoutingLifecycle.RestoreAsync(reloaded, nextRuntime);
        await nextRuntime.Starts.Should().BeEqualTo(1);
    }

    [Test]
    public async Task ExplicitDisablePersistsAndPreventsStartup()
    {
        var config = new Config { AppRouting = new() { Enabled = true } };
        var runtime = new Runtime();
        string persisted = "";
        await AppRoutingLifecycle.SetEnabledAsync(config, runtime, c => { persisted = JsonUtils.Serialize(c); return Task.FromResult(0); }, false);
        var reloaded = JsonUtils.Deserialize<Config>(persisted)!;
        await reloaded.AppRouting.Enabled.Should().BeFalse();
        await AppRoutingLifecycle.RestoreAsync(reloaded, runtime);
        await runtime.Starts.Should().BeEqualTo(0);
    }

    [Test]
    public async Task StartupFailureRetainsPreferenceForRetry()
    {
        var config = new Config { AppRouting = new() { Enabled = true } };
        var runtime = new Runtime { FailStart = true };
        var failed = false;
        try
        {
            await AppRoutingLifecycle.RestoreAsync(config, runtime);
        }
        catch (IOException) { failed = true; }
        await failed.Should().BeTrue();
        await config.AppRouting.Enabled.Should().BeTrue();
        runtime.FailStart = false;
        await AppRoutingLifecycle.RestoreAsync(config, runtime);
        await runtime.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task SaveFailureDoesNotStartRoutingOrChangePreference()
    {
        var config = new Config();
        var runtime = new Runtime();
        var failed = false;
        try
        {
            await AppRoutingLifecycle.SetEnabledAsync(config, runtime, _ => Task.FromResult(-1), true);
        }
        catch (IOException) { failed = true; }
        await failed.Should().BeTrue();
        await config.AppRouting.Enabled.Should().BeFalse();
        await runtime.Starts.Should().BeEqualTo(0);
    }

    [Test]
    public async Task OldConfigurationsDefaultToOffWithoutChildInheritance()
    {
        var item = JsonUtils.Deserialize<AppRoutingItem>("{\"Rules\":[{\"ExecutablePath\":\"app.exe\",\"MatchByName\":true}]}")!;
        await item.Enabled.Should().BeFalse();
        await item.Rules.Single().IncludeChildProcesses.Should().BeFalse();
    }

    [Test]
    public async Task FailedManualEnableRestoresPersistedOffSetting()
    {
        var config = new Config();
        var runtime = new Runtime { FailStart = true };
        string persisted = "";
        try
        {
            await AppRoutingLifecycle.SetEnabledAsync(config, runtime,
                c => { persisted = JsonUtils.Serialize(c); return Task.FromResult(0); }, true);
        }
        catch (IOException) { }
        await runtime.Starts.Should().BeEqualTo(1);
        await JsonUtils.Deserialize<Config>(persisted)!.AppRouting.Enabled.Should().BeFalse();
    }

    [Test]
    public async Task ThrowingSaveRestoresPreferenceWithoutStartingRuntime()
    {
        var config = new Config();
        var runtime = new Runtime();
        var failed = false;
        try
        {
            await AppRoutingLifecycle.SetEnabledAsync(config, runtime,
                _ => Task.FromException<int>(new IOException("Fixture save failure")), true);
        }
        catch (IOException) { failed = true; }
        await failed.Should().BeTrue();
        await config.AppRouting.Enabled.Should().BeFalse();
        await runtime.Starts.Should().BeEqualTo(0);
    }

    private sealed class Runtime : IAppRoutingRuntime
    {
        public bool IsEnabled
        {
            get; private set;
        }

        public int Starts
        {
            get; private set;
        }
        public bool FailStart
        {
            get; set;
        }

        public Task StartAsync(Config config)
        {
            Starts++;
            if (FailStart)
            {
                throw new IOException("Fixture startup failure");
            }

            IsEnabled = true;
            return Task.CompletedTask;
        }
        public Task StopAsync()
        {
            IsEnabled = false;
            return Task.CompletedTask;
        }
    }
}
