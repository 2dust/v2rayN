using System.Collections;
using v2rayN.WebAPI.Launcher;

namespace v2rayN.WebAPI.Tests;

[NotInParallel]
public class LauncherEnvironmentTests
{
    [Test]
    [Arguments("same-value", "same-value")]
    [Arguments("upper-value", "lower-value")]
    public async Task CurrentEnvironmentPreservesPlatformVariableNameSemantics(string upperValue, string lowerValue)
    {
        var upperName = $"V2RAYN_ENVIRONMENT_TEST_{Guid.NewGuid():N}".ToUpperInvariant();
        var lowerName = upperName.ToLowerInvariant();
        using var environmentScope = new TemporaryEnvironment((upperName, upperValue), (lowerName, lowerValue));

        var environment = LauncherEnvironment.ReadCurrentEnvironment();

        await environment[upperName].Should().BeEqualTo(OperatingSystem.IsWindows() ? lowerValue : upperValue);
        await environment[lowerName].Should().BeEqualTo(lowerValue);
        await environment.Keys.Count(key => string.Equals(key, upperName, StringComparison.OrdinalIgnoreCase))
            .Should().BeEqualTo(OperatingSystem.IsWindows() ? 1 : 2);
    }

    [Test]
    [Arguments("V2RAYN_WEB_API_KEY")]
    [Arguments("LC_ALL")]
    public async Task CurrentEnvironmentRequiresCanonicalSettingNamesOnUnix(string canonicalName)
    {
        var lowerName = canonicalName.ToLowerInvariant();
        using var environmentScope = new TemporaryEnvironment((canonicalName, null), (lowerName, "wrong-case-value"));

        var environment = LauncherEnvironment.ReadCurrentEnvironment();

        await environment.GetValueOrDefault(canonicalName).Should()
            .BeEqualTo(OperatingSystem.IsWindows() ? "wrong-case-value" : null);
        await environment[lowerName].Should().BeEqualTo("wrong-case-value");
    }

    [Test]
    public async Task CurrentEnvironmentKeepsCanonicalManagementKeyDistinctOnUnix()
    {
        using var environmentScope = new TemporaryEnvironment(
            ("V2RAYN_WEB_API_KEY", "canonical-management-key-123456"),
            ("v2rayn_web_api_key", "other-management-key-123456"));

        var environment = LauncherEnvironment.ReadCurrentEnvironment();

        await environment.GetValueOrDefault("V2RAYN_WEB_API_KEY").Should()
            .BeEqualTo(OperatingSystem.IsWindows() ? "other-management-key-123456" : "canonical-management-key-123456");
        await environment["v2rayn_web_api_key"].Should().BeEqualTo("other-management-key-123456");
    }

    [Test]
    public async Task CurrentEnvironmentUsesCanonicalInteractiveLaunchMarkerOnUnix()
    {
        using var environmentScope = new TemporaryEnvironment(
            (LauncherEnvironment.InteractiveLaunchEnvironmentVariable, null),
            (LauncherEnvironment.InteractiveLaunchEnvironmentVariable.ToLowerInvariant(), "1"));

        var environment = LauncherEnvironment.ReadCurrentEnvironment();

        await LauncherEnvironment.IsInteractiveLaunchMarker(environment).Should().BeEqualTo(OperatingSystem.IsWindows());
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        private readonly string[] _names;
        private readonly (string Name, string? Value)[] _originalValues;

        public TemporaryEnvironment(params (string Name, string? Value)[] values)
        {
            _names = values.Select(value => value.Name).ToArray();
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            _originalValues = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Where(entry => _names.Contains((string)entry.Key, comparer))
                .Select(entry => ((string)entry.Key, entry.Value?.ToString()))
                .ToArray();

            foreach (var (name, value) in values)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var name in _names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            foreach (var (name, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
