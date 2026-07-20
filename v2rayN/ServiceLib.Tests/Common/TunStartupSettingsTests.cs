using ServiceLib.Common;
using Xunit;

namespace ServiceLib.Tests.Common;

public sealed class TunStartupSettingsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _settingsPath;

    public TunStartupSettingsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"v2rayN-tun-settings-{Guid.NewGuid():N}");
        _settingsPath = Path.Combine(_testDirectory, "finetunes.ini");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void LoadOrCreate_CreatesMissingFileWithDefaultValue()
    {
        var settings = TunStartupSettings.LoadOrCreate(_testDirectory);

        Assert.Equal(20, settings.ObservationSeconds);
        Assert.True(File.Exists(_settingsPath));
        Assert.Contains("TunStartObservationSeconds=20", File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void LoadOrCreate_AddsMissingSetting()
    {
        File.WriteAllText(_settingsPath, "; existing settings" + Environment.NewLine);

        var settings = TunStartupSettings.LoadOrCreate(_testDirectory);

        Assert.Equal(20, settings.ObservationSeconds);
        Assert.Contains("TunStartObservationSeconds=20", File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void LoadOrCreate_ReplacesValueBelowMinimum()
    {
        File.WriteAllText(_settingsPath, "TunStartObservationSeconds=6" + Environment.NewLine);

        var settings = TunStartupSettings.LoadOrCreate(_testDirectory);

        Assert.Equal(20, settings.ObservationSeconds);
        Assert.Equal(
            "TunStartObservationSeconds=20" + Environment.NewLine,
            File.ReadAllText(_settingsPath));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("301")]
    public void LoadOrCreate_ReplacesInvalidValue(string value)
    {
        File.WriteAllText(_settingsPath, $"TunStartObservationSeconds={value}{Environment.NewLine}");

        var settings = TunStartupSettings.LoadOrCreate(_testDirectory);

        Assert.Equal(20, settings.ObservationSeconds);
        Assert.Equal(
            "TunStartObservationSeconds=20" + Environment.NewLine,
            File.ReadAllText(_settingsPath));
    }

    [Fact]
    public void LoadOrCreate_UsesValidConfiguredValue()
    {
        File.WriteAllText(_settingsPath, "TunStartObservationSeconds=45" + Environment.NewLine);

        var settings = TunStartupSettings.LoadOrCreate(_testDirectory);

        Assert.Equal(45, settings.ObservationSeconds);
        Assert.Equal(
            "TunStartObservationSeconds=45" + Environment.NewLine,
            File.ReadAllText(_settingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
