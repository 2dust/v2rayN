using System.Reflection;
using System.Text.Json;
using ServiceLib;
using ServiceLib.Common;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using v2rayN.Web.Contracts;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

[NotInParallel]
public class UpdatePreReleaseSettingsTests
{
    private const string WebTarget = "v2rayN.Web";

    [Test]
    [Arguments(true, null, true)]
    [Arguments(false, null, false)]
    [Arguments(true, true, true)]
    [Arguments(true, false, false)]
    [Arguments(false, true, true)]
    [Arguments(false, false, false)]
    public async Task WebPreReleaseUsesItsOwnSettingUnlessExplicitlyOverridden(
        bool configured, bool? preRelease, bool expected)
    {
        using var fixture = new RuntimeFixture();
        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = configured ? [WebTarget] : [];

        await fixture.Runtime.GetUpdatePreRelease(WebTarget, preRelease).Should().BeEqualTo(expected);
        // An override is request-scoped, not a change to persisted preferences.
        var settings = fixture.Runtime.GetCoreUpdateSettings();
        await settings.PreRelease.Should().BeEqualTo(configured);
        await settings.CheckPreReleaseCoreTypes.Contains(WebTarget).Should().BeEqualTo(configured);
    }

    [Test]
    public async Task MissingPreReleaseListDefaultsToStable()
    {
        using var fixture = new RuntimeFixture();

        await fixture.Runtime.GetUpdatePreRelease(WebTarget).Should().BeFalse();
        await fixture.Runtime.GetUpdatePreRelease("Xray").Should().BeFalse();
        await fixture.Runtime.GetCoreUpdateSettings().CheckPreReleaseCoreTypes.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task WebPreReleaseIsIndependentOfDesktopAndCoreTargets()
    {
        using var fixture = new RuntimeFixture();
        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = ["v2rayN", "Xray", "v2rayn.web"];

        await fixture.Runtime.GetUpdatePreRelease(WebTarget).Should().BeFalse();
        await fixture.Runtime.GetUpdatePreRelease("Xray").Should().BeTrue();
        var settings = fixture.Runtime.GetCoreUpdateSettings();
        await settings.PreRelease.Should().BeFalse();
        await settings.CheckPreReleaseCoreTypes.SequenceEqual(["Xray"]).Should().BeTrue();

        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = [WebTarget];
        await fixture.Runtime.GetUpdatePreRelease(WebTarget).Should().BeTrue();
        await fixture.Runtime.GetUpdatePreRelease("Xray").Should().BeFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task PerTargetSettingsSaveAddsOrRemovesWebAndPreservesHiddenPreferences(bool enableWeb)
    {
        using var fixture = new RuntimeFixture();
        fixture.Config.CheckUpdateItem.SelectedCoreTypes = ["v2rayN", "Xray", WebTarget];
        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = ["v2rayN", "OtherPlatform", "Xray", WebTarget];
        var input = new CoreUpdateSettingsInput(
            ["Xray"], PreRelease: !enableWeb, UseProxy: false,
            CheckPreReleaseCoreTypes: enableWeb ? [WebTarget, WebTarget] : []);

        var result = await fixture.Runtime.SaveCoreUpdateSettingsAsync(input);

        await result.Success.Should().BeTrue();
        var saved = fixture.Config.CheckUpdateItem;
        await saved.CheckPreReleaseCoreTypes.Contains(WebTarget).Should().BeEqualTo(enableWeb);
        await saved.CheckPreReleaseCoreTypes.Contains("Xray").Should().BeFalse();
        await saved.CheckPreReleaseCoreTypes.Contains("v2rayN").Should().BeTrue();
        await saved.CheckPreReleaseCoreTypes.Contains("OtherPlatform").Should().BeTrue();
        await saved.CheckPreReleaseCoreTypes.Count(name => name == WebTarget).Should().BeEqualTo(enableWeb ? 1 : 0);
        // Prerelease preferences are saved even for targets not selected for a batch.
        await saved.SelectedCoreTypes.Contains(WebTarget).Should().BeFalse();
        await saved.SelectedCoreTypes.Contains("v2rayN").Should().BeTrue();
        await saved.UpdateViaProxy.Should().BeFalse();

        var view = fixture.Runtime.GetCoreUpdateSettings();
        await view.PreRelease.Should().BeEqualTo(enableWeb);
        await view.CheckPreReleaseCoreTypes.SequenceEqual(enableWeb ? [WebTarget] : []).Should().BeTrue();
        var reloaded = await fixture.ReadSavedConfigAsync();
        await reloaded.CheckUpdateItem.CheckPreReleaseCoreTypes.SequenceEqual(saved.CheckPreReleaseCoreTypes).Should().BeTrue();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task LegacySettingsPayloadStillSavesSupportedTargetsIncludingWeb(bool preRelease)
    {
        using var fixture = new RuntimeFixture();
        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = ["v2rayN", "Xray", WebTarget];
        var json = JsonSerializer.Serialize(new
        {
            selectedCoreTypes = Array.Empty<string>(),
            preRelease,
            useProxy = true,
        });
        var input = JsonSerializer.Deserialize<CoreUpdateSettingsInput>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await (input.CheckPreReleaseCoreTypes is null).Should().BeTrue();
        var result = await fixture.Runtime.SaveCoreUpdateSettingsAsync(input);

        await result.Success.Should().BeTrue();
        await fixture.Runtime.GetUpdatePreRelease(WebTarget).Should().BeEqualTo(preRelease);
        await fixture.Runtime.GetUpdatePreRelease("Xray").Should().BeEqualTo(preRelease);
        await fixture.Runtime.GetUpdatePreRelease("mihomo").Should().BeFalse();
        await fixture.Runtime.GetUpdatePreRelease("sing_box").Should().BeFalse();
        await fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes.Contains("v2rayN").Should().BeTrue();
        var reloaded = await fixture.ReadSavedConfigAsync();
        await reloaded.CheckUpdateItem.CheckPreReleaseCoreTypes.Contains(WebTarget).Should().BeEqualTo(preRelease);
    }

    [Test]
    [Arguments("GeoFiles")]
    [Arguments("v2rayN")]
    [Arguments("mihomo")]
    public async Task InvalidPerTargetPreReleaseSettingsDoNotMutateConfig(string target)
    {
        using var fixture = new RuntimeFixture();
        fixture.Config.CheckUpdateItem.CheckPreReleaseCoreTypes = [WebTarget];
        var previous = JsonUtils.Serialize(fixture.Config);

        var result = await fixture.Runtime.SaveCoreUpdateSettingsAsync(
            new CoreUpdateSettingsInput([], false, false, [target]));

        await result.Success.Should().BeFalse();
        await result.Code.Should().BeEqualTo("core_update_settings_invalid");
        await JsonUtils.Serialize(fixture.Config).Should().BeEqualTo(previous);
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly FieldInfo _configField = typeof(AppManager)
            .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AppManager).FullName, "_config");
        private readonly object? _previousConfig;
        private readonly string? _previousLocalData;
        private readonly string _configPath;
        private readonly byte[]? _previousConfigFile;
        private readonly UnixFileMode? _previousConfigMode;

        public Config Config { get; } = new() { CheckUpdateItem = new CheckUpdateItem() };
        public V2rayRuntime Runtime { get; } = new(null!, null!, null!, null!, null!);

        public RuntimeFixture()
        {
            _previousConfig = _configField.GetValue(AppManager.Instance);
            _previousLocalData = Environment.GetEnvironmentVariable(Global.LocalAppData);
            // ConfigHandler writes only into the test build directory, never user data.
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guiConfigs", Global.ConfigFileName);
            if (File.Exists(_configPath))
            {
                _previousConfigFile = File.ReadAllBytes(_configPath);
                if (!OperatingSystem.IsWindows()) _previousConfigMode = File.GetUnixFileMode(_configPath);
            }
            Environment.SetEnvironmentVariable(Global.LocalAppData, "0");
            _configField.SetValue(AppManager.Instance, Config);
        }

        public async Task<Config> ReadSavedConfigAsync() =>
            JsonUtils.Deserialize<Config>(await File.ReadAllTextAsync(_configPath))
            ?? throw new InvalidDataException("The saved update settings could not be reloaded.");

        public void Dispose()
        {
            try
            {
                if (_previousConfigFile is null)
                {
                    if (File.Exists(_configPath)) File.Delete(_configPath);
                }
                else
                {
                    File.WriteAllBytes(_configPath, _previousConfigFile);
                    if (!OperatingSystem.IsWindows() && _previousConfigMode is { } mode)
                        File.SetUnixFileMode(_configPath, mode);
                }
            }
            finally
            {
                _configField.SetValue(AppManager.Instance, _previousConfig);
                Environment.SetEnvironmentVariable(Global.LocalAppData, _previousLocalData);
            }
        }
    }
}
