using v2rayN.WebAPI.Launcher;
using System.Security.Cryptography;
using System.Text;
using System.IO.MemoryMappedFiles;

namespace v2rayN.WebAPI.Tests;

public class NativeWebUpdateFileSwapTests
{
    [Test]
    public async Task RollbackDoesNotModifyAnExecutableThatIsStillMemoryMapped()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var plan = await CreatePlanAsync(directory.Path, includeNewIdentity: true);
        NativeWebUpdateHelper.CopyCurrentAppToBackup(plan);
        NativeWebUpdateHelper.SwapCandidateAppIntoPlace(plan);
        using var executable = new FileStream(Path.Combine(plan.InstallDirectory, "v2rayN.WebAPI"),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var mapping = MemoryMappedFile.CreateFromFile(executable, null, 0,
            MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        await view.ReadByte(0).Should().BeEqualTo((byte)'n');

        NativeWebUpdateHelper.RestorePreviousApp(plan);

        await view.ReadByte(0).Should().BeEqualTo((byte)'n');
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "v2rayN.WebAPI"))).Should().BeEqualTo("old-web");
        await Directory.GetFiles(plan.InstallDirectory, ".v2rayn-web-restore-*").Length.Should().BeEqualTo(0);
    }

    [Test]
    public async Task ApiSelfUpdatePreservesThirdPartyWebUiByteForByte()
    {
        using var directory = new TemporaryDirectory();
        var plan = await CreatePlanAsync(directory.Path, includeNewIdentity: true);
        var uiHash = await HashDirectoryAsync(Path.Combine(plan.InstallDirectory, "webui"));
        NativeWebUpdateHelper.CopyCurrentAppToBackup(plan);
        NativeWebUpdateHelper.SwapCandidateAppIntoPlace(plan);

        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "v2rayN.WebAPI"))).Should().BeEqualTo("new-web");
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "webui", "index.html"))).Should().BeEqualTo("THIRD_PARTY_UI_TEST");
        await (await HashDirectoryAsync(Path.Combine(plan.InstallDirectory, "webui"))).Should().BeEqualTo(uiHash);
        await Directory.Exists(Path.Combine(plan.BackupDirectory, "webui")).Should().BeFalse();
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "bin", "xray", "xray"))).Should().BeEqualTo("updated-core");
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "guiConfigs", "guiNConfig.json"))).Should().BeEqualTo("user-config");
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, ".env"))).Should().BeEqualTo("V2RAYN_WEB_API_KEY=private-test-key\n");
        await File.Exists(Path.Combine(plan.BackupDirectory, ".env")).Should().BeFalse();
    }

    [Test]
    public async Task PartialFileReplacementFailureRestoresOldAppWithoutTouchingCoreFiles()
    {
        using var directory = new TemporaryDirectory();
        var plan = await CreatePlanAsync(directory.Path, includeNewIdentity: false);
        var uiHash = await HashDirectoryAsync(Path.Combine(plan.InstallDirectory, "webui"));
        NativeWebUpdateHelper.CopyCurrentAppToBackup(plan);

        var rollback = await NativeWebUpdateWorkflow.ApplyAsync(
            () => { NativeWebUpdateHelper.SwapCandidateAppIntoPlace(plan); return Task.CompletedTask; },
            () => Task.FromResult(false),
            () => { NativeWebUpdateHelper.RestorePreviousApp(plan); return Task.CompletedTask; },
            () => Task.FromResult(true),
            _ => { },
            () => Task.CompletedTask);

        await rollback.Success.Should().BeFalse();
        await rollback.RollbackSucceeded.Should().BeTrue();
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "v2rayN.WebAPI"))).Should().BeEqualTo("old-web");
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "webui", "index.html"))).Should().BeEqualTo("THIRD_PARTY_UI_TEST");
        await (await HashDirectoryAsync(Path.Combine(plan.InstallDirectory, "webui"))).Should().BeEqualTo(uiHash);
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, "bin", "xray", "xray"))).Should().BeEqualTo("updated-core");
        await (await File.ReadAllTextAsync(Path.Combine(plan.InstallDirectory, ".env"))).Should().BeEqualTo("V2RAYN_WEB_API_KEY=private-test-key\n");
        await File.Exists(Path.Combine(plan.BackupDirectory, ".env")).Should().BeFalse();
    }

    private static async Task<NativeWebUpdatePlan> CreatePlanAsync(string root, bool includeNewIdentity)
    {
        var install = Path.Combine(root, "native");
        var parent = Path.GetDirectoryName(install)!;
        var candidate = Path.Combine(parent, ".v2rayn-web-candidate-test");
        var backup = Path.Combine(parent, ".v2rayn-web-backup-test");
        Directory.CreateDirectory(Path.Combine(install, "webui", "assets"));
        Directory.CreateDirectory(Path.Combine(install, "bin", "xray"));
        Directory.CreateDirectory(Path.Combine(install, "guiConfigs"));
        Directory.CreateDirectory(candidate);
        await File.WriteAllTextAsync(Path.Combine(install, "v2rayN.WebAPI"), "old-web");
        await File.WriteAllTextAsync(Path.Combine(install, "v2rayN.WebAPI.build.json"), "old-identity");
        await File.WriteAllTextAsync(Path.Combine(install, "webui", "index.html"), "THIRD_PARTY_UI_TEST");
        await File.WriteAllTextAsync(Path.Combine(install, "webui", "assets", "test.js"), "window.thirdPartyUi = true;");
        await File.WriteAllTextAsync(Path.Combine(install, "bin", "xray", "xray"), "updated-core");
        await File.WriteAllTextAsync(Path.Combine(install, "guiConfigs", "guiNConfig.json"), "user-config");
        await File.WriteAllTextAsync(Path.Combine(install, ".env"), "V2RAYN_WEB_API_KEY=private-test-key\n");
        await File.WriteAllTextAsync(Path.Combine(candidate, "v2rayN.WebAPI"), "new-web");
        if (includeNewIdentity)
            await File.WriteAllTextAsync(Path.Combine(candidate, "v2rayN.WebAPI.build.json"), "new-identity");
        return new NativeWebUpdatePlan(
            install,
            candidate,
            backup,
            Path.Combine(root, "instance.lock"),
            "http://127.0.0.1:5080/api/health",
            [],
            "7.25.2-web.2",
            "new-commit",
            "linux-x64",
            "7.25.2-web.1",
            Path.Combine(root, "runtime-intent.json"),
            Path.Combine(root, "update-progress.json"));
    }

    private static async Task<string> HashDirectoryAsync(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(path, file).Replace(Path.DirectorySeparatorChar, '/')));
            hash.AppendData([0]);
            hash.AppendData(await File.ReadAllBytesAsync(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-swap-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
