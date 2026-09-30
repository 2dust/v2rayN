using System.Text.Json;
using v2rayN.Web.Security;

namespace v2rayN.Web.Tests;

public class WebAuthServiceTests
{
    private const string ManagementKey = "test-management-key-2026";

    [Test]
    public async Task MissingEnvironmentAndPersistedKeyRequiresSetup()
    {
        using var directory = new TemporaryDirectory();
        var auth = new WebAuthService(Path.Combine(directory.Path, "guiConfigs", "web-auth.json"), null);

        await auth.SetupRequired.Should().BeTrue();
        await auth.EnvironmentKeyConfigured.Should().BeFalse();
        await auth.ValidateManagementKey(ManagementKey).Should().BeFalse();
    }

    [Test]
    public async Task EnvironmentKeyTakesPrecedenceWithoutPersistingIt()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        var auth = new WebAuthService(path, ManagementKey);

        await auth.SetupRequired.Should().BeFalse();
        await auth.EnvironmentKeyConfigured.Should().BeTrue();
        await auth.ValidateManagementKey(ManagementKey).Should().BeTrue();
        await auth.ValidateManagementKey("a-different-management-key").Should().BeFalse();
        await File.Exists(path).Should().BeFalse();
    }

    [Test]
    public async Task SetupPersistsOnlySaltAndVerifierAndVerifiesTheKeyImmediately()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        var auth = new WebAuthService(path, null);

        var result = await auth.SetupAsync(ManagementKey, ManagementKey, setupAccessAllowed: true);

        await (result == WebSetupResult.Created).Should().BeTrue();
        await auth.SetupRequired.Should().BeFalse();
        await auth.ValidateManagementKey(ManagementKey).Should().BeTrue();
        await auth.ValidateManagementKey("not-the-management-key").Should().BeFalse();

        var persisted = await File.ReadAllTextAsync(path);
        await persisted.Contains(ManagementKey, StringComparison.Ordinal).Should().BeFalse();
        using var document = JsonDocument.Parse(persisted);
        await (document.RootElement.GetProperty("version").GetInt32() == 1).Should().BeTrue();
        await (!string.IsNullOrWhiteSpace(document.RootElement.GetProperty("salt").GetString())).Should().BeTrue();
        await (!string.IsNullOrWhiteSpace(document.RootElement.GetProperty("keyVerifier").GetString())).Should().BeTrue();
        if (OperatingSystem.IsLinux())
        {
            await (File.GetUnixFileMode(path) == (UnixFileMode.UserRead | UnixFileMode.UserWrite)).Should().BeTrue();
        }
    }

    [Test]
    public async Task SetupRejectsShortOrMismatchedKeys()
    {
        using var directory = new TemporaryDirectory();
        var auth = new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), null);

        await ((await auth.SetupAsync("short", "short", setupAccessAllowed: true)) == WebSetupResult.KeyTooShort).Should().BeTrue();
        await ((await auth.SetupAsync(new string('x', WebAuthService.MaximumKeyLength + 1), new string('x', WebAuthService.MaximumKeyLength + 1), setupAccessAllowed: true)) == WebSetupResult.KeyTooLong).Should().BeTrue();
        await ((await auth.SetupAsync(ManagementKey, "a-different-key", setupAccessAllowed: true)) == WebSetupResult.KeysDoNotMatch).Should().BeTrue();
        await auth.SetupRequired.Should().BeTrue();
    }

    [Test]
    public async Task SetupCanOnlyBeCompletedOnce()
    {
        using var directory = new TemporaryDirectory();
        var auth = new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), null);

        await ((await auth.SetupAsync(ManagementKey, ManagementKey, setupAccessAllowed: true)) == WebSetupResult.Created).Should().BeTrue();
        await ((await auth.SetupAsync("a-second-management-key", "a-second-management-key", setupAccessAllowed: true)) == WebSetupResult.AlreadyConfigured).Should().BeTrue();
        await auth.ValidateManagementKey(ManagementKey).Should().BeTrue();
        await auth.ValidateManagementKey("a-second-management-key").Should().BeFalse();
    }

    [Test]
    public async Task SetupRejectsWhenRequestAccessPolicyDeniesIt()
    {
        using var directory = new TemporaryDirectory();
        var auth = new WebAuthService(Path.Combine(directory.Path, "web-auth.json"), null);

        await ((await auth.SetupAsync(ManagementKey, ManagementKey, setupAccessAllowed: false)) == WebSetupResult.Forbidden).Should().BeTrue();
        await auth.SetupRequired.Should().BeTrue();
    }

    [Test]
    public async Task PersistedVerifierSurvivesServiceRecreation()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        var firstInstance = new WebAuthService(path, null);
        await ((await firstInstance.SetupAsync(ManagementKey, ManagementKey, setupAccessAllowed: true)) == WebSetupResult.Created).Should().BeTrue();

        var restartedInstance = new WebAuthService(path, null);
        await restartedInstance.SetupRequired.Should().BeFalse();
        await restartedInstance.ValidateManagementKey(ManagementKey).Should().BeTrue();
    }

    [Test]
    public async Task WebAuthStorageMovesLegacyConfigCredentialOutsideTheBackupDirectory()
    {
        using var directory = new TemporaryDirectory();
        var legacyPath = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(legacyPath, "local-verifier");

        var authPath = WebAuthStorage.MigrateAndGetPath(directory.Path, legacyPath);

        await authPath.Should().BeEqualTo(Path.Combine(directory.Path, "webData", "web-auth.json"));
        await (await File.ReadAllTextAsync(authPath)).Should().BeEqualTo("local-verifier");
        await File.Exists(legacyPath).Should().BeFalse();
        if (OperatingSystem.IsLinux())
        {
            await (File.GetUnixFileMode(Path.GetDirectoryName(authPath)!)
                == (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)).Should().BeTrue();
        }
    }

    [Test]
    public async Task ExistingPrivateWebAuthCredentialWinsOverAStaleLegacyCopy()
    {
        using var directory = new TemporaryDirectory();
        var privatePath = Path.Combine(directory.Path, "webData", "web-auth.json");
        var legacyPath = Path.Combine(directory.Path, "guiConfigs", "web-auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(privatePath, "private-verifier");
        await File.WriteAllTextAsync(legacyPath, "stale-verifier");

        var authPath = WebAuthStorage.MigrateAndGetPath(directory.Path, legacyPath);

        await authPath.Should().BeEqualTo(privatePath);
        await (await File.ReadAllTextAsync(authPath)).Should().BeEqualTo("private-verifier");
        await File.Exists(legacyPath).Should().BeFalse();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-auth-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
