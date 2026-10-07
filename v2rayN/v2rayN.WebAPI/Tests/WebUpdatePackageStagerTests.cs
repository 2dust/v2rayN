using System.IO.Compression;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class WebUpdatePackageStagerTests
{
    [Test]
    public async Task LegacyProductIdentityIsRejectedWithManualReinstallInstruction()
    {
        var rejected = false;
        try
        {
            _ = WebUpdatePackageStager.ParseManifest("{\"product\":\"v2rayN.Web\"}");
        }
        catch (InvalidDataException exception)
        {
            rejected = exception.Message.Contains("manual installation of v2rayN.WebAPI", StringComparison.Ordinal);
        }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task OfficialVersionChannelHandlesOlderNewerEqualPrereleaseAndLegacyBuilds()
    {
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.2", "7.25.3", allowPrerelease: false)
            .Should().BeTrue();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.3", allowPrerelease: true)
            .Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.3", allowPrerelease: true,
            currentCommit: "old-build", candidateCommit: "release-build").Should().BeTrue();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.3", allowPrerelease: true,
            currentCommit: "same", candidateCommit: "same").Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.2", allowPrerelease: true)
            .Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("0.0.0-dev", "7.25.3", allowPrerelease: false)
            .Should().BeTrue();
        // Development identities are older than the official releases they track.
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3-dev.2", "7.25.3", allowPrerelease: true)
            .Should().BeTrue();
        // Migration from the retired web-v/-web.N channel: a legacy build upgrades to the official tag,
        // and an official build is never "updated" back to a legacy prerelease.
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.2-web.5", "7.25.3", allowPrerelease: false)
            .Should().BeTrue();
        await WebUpdatePackageStager.IsUpdateAvailable("7.25.3", "7.25.3-web.1", allowPrerelease: true)
            .Should().BeFalse();
        await WebUpdatePackageStager.IsUpdateAvailable("not-a-version", "7.25.3", allowPrerelease: true)
            .Should().BeFalse();
        await WebUpdatePackageStager.ShouldConsiderRelease(isPrerelease: false, allowPrerelease: false).Should().BeTrue();
        await WebUpdatePackageStager.ShouldConsiderRelease(isPrerelease: true, allowPrerelease: false).Should().BeFalse();
        await WebUpdatePackageStager.ShouldConsiderRelease(isPrerelease: true, allowPrerelease: true).Should().BeTrue();

        var build = WebBuildIdentity.Current;
        await WebUpdatePackageStager.IsValidVersion(build.Version).Should().BeTrue();
        await WebReleaseChannel.IsValidRepository(build.Repository).Should().BeTrue();
        await string.IsNullOrWhiteSpace(build.Rid).Should().BeFalse();
    }

    [Test]
    public async Task ManifestRequiresWebProductHttpsAssetsAndAnExactRid()
    {
        var manifest = new WebUpdateManifest("v2rayN.WebAPI", "7.25.3", "0123456789abcdef", "2026-09-28T00:00:00Z",
        [
            new WebUpdatePackage("linux-x64", "v2rayN-linux-64-web-update.zip",
                "https://github.com/2dust/v2rayN/releases/download/7.25.3/v2rayN-linux-64-web-update.zip",
                new string('a', 64), 1234),
            new WebUpdatePackage("linux-arm64", "v2rayN-linux-arm64-web-update.zip",
                "https://github.com/2dust/v2rayN/releases/download/7.25.3/v2rayN-linux-arm64-web-update.zip",
                new string('b', 64), 2345),
        ]);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var parsed = WebUpdatePackageStager.ParseManifest(json);
        await parsed.Version.Should().BeEqualTo("7.25.3");
        await WebUpdatePackageStager.RequirePackage(parsed, "linux-x64").Rid.Should().BeEqualTo("linux-x64");
        await WebUpdatePackageStager.RequirePackage(parsed, "linux-arm64").Rid.Should().BeEqualTo("linux-arm64");
        await WebReleaseChannel.IsTrustedAssetUrl(parsed.Packages[0].Url, "2dust/v2rayN", parsed.Version, parsed.Packages[0].Asset)
            .Should().BeTrue();

        var wrongRidRejected = false;
        try { _ = WebUpdatePackageStager.RequirePackage(parsed, "linux-riscv64"); }
        catch (InvalidDataException) { wrongRidRejected = true; }
        await wrongRidRejected.Should().BeTrue();

        var oversized = manifest with
        {
            Packages = [manifest.Packages[0] with { Size = WebUpdatePackageStager.MaximumArchiveBytes + 1 }],
        };
        var oversizedRejected = false;
        try
        {
            _ = WebUpdatePackageStager.ParseManifest(JsonSerializer.Serialize(oversized, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        catch (InvalidDataException) { oversizedRejected = true; }
        await oversizedRejected.Should().BeTrue();
    }

    [Test]
    public async Task AppOnlyZipPackageIsVerifiedAndExtractedForItsExactIdentity()
    {
        using var directory = new TemporaryDirectory();
        var archive = Path.Combine(directory.Path, "web-app.zip");
        var identity = new WebUpdatePackageIdentity("v2rayN.WebAPI", "7.25.3", "0123456789abcdef", "2026-09-28T01:00:00Z", "linux-x64");
        await WriteArchiveAsync(archive,
        [
            ("v2rayN.WebAPI", NativeExecutableFixture()),
            ("v2rayN.WebAPI.build.json", JsonSerializer.SerializeToUtf8Bytes(identity, new JsonSerializerOptions(JsonSerializerDefaults.Web))),
        ]);
        var manifest = CreateManifest(archive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        var package = manifest.Packages[0];

        var extracted = Path.Combine(directory.Path, "stage");
        var actualIdentity = await WebUpdatePackageStager.VerifyAndExtractAsync(
            archive, extracted, manifest, package, CancellationToken.None);
        await actualIdentity.Should().BeEqualTo(identity);
        await File.Exists(Path.Combine(extracted, "bin", "xray", "xray")).Should().BeFalse();
        await File.Exists(Path.Combine(extracted, "guiConfigs", "guiNConfig.json")).Should().BeFalse();
        await File.Exists(Path.Combine(extracted, "webData", "web-auth.json")).Should().BeFalse();
        await File.Exists(Path.Combine(extracted, "webui", "index.html")).Should().BeFalse();
        await File.Exists(Path.Combine(extracted, "wwwroot", "index.html")).Should().BeFalse();
    }

    [Test]
    public async Task ChecksumMismatchCorruptArchiveWrongRidAndMissingExecutableAreRejected()
    {
        using var directory = new TemporaryDirectory();
        var identity = new WebUpdatePackageIdentity("v2rayN.WebAPI", "7.25.4", "abcdef0123456789", "2026-09-28T02:00:00Z", "linux-x64");
        var archive = Path.Combine(directory.Path, "good.zip");
        await WriteArchiveAsync(archive,
        [
            ("v2rayN.WebAPI", NativeExecutableFixture()),
            ("v2rayN.WebAPI.build.json", JsonSerializer.SerializeToUtf8Bytes(identity, new JsonSerializerOptions(JsonSerializerDefaults.Web))),
        ]);
        var manifest = CreateManifest(archive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        var damaged = Path.Combine(directory.Path, "damaged.zip");
        var bytes = await File.ReadAllBytesAsync(archive);
        bytes[^1] ^= 0x01;
        await File.WriteAllBytesAsync(damaged, bytes);
        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            damaged, Path.Combine(directory.Path, "checksum-failure"), manifest, manifest.Packages[0], CancellationToken.None));

        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            archive, Path.Combine(directory.Path, "wrong-rid"), manifest,
            manifest.Packages[0] with { Rid = "linux-arm64" }, CancellationToken.None));

        var missingExecutableArchive = Path.Combine(directory.Path, "missing.zip");
        await WriteArchiveAsync(missingExecutableArchive,
        [
            ("v2rayN.WebAPI.build.json", JsonSerializer.SerializeToUtf8Bytes(identity, new JsonSerializerOptions(JsonSerializerDefaults.Web))),
        ]);
        var missingManifest = CreateManifest(missingExecutableArchive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            missingExecutableArchive, Path.Combine(directory.Path, "missing-executable"), missingManifest,
            missingManifest.Packages[0], CancellationToken.None));
    }

    [Test]
    public async Task ZipTraversalLinksDuplicatesAndUnexpectedEntriesAreRejected()
    {
        using var directory = new TemporaryDirectory();
        var identity = new WebUpdatePackageIdentity("v2rayN.WebAPI", "7.25.5", "0123abcdef", "2026-09-28T03:00:00Z", "linux-x64");
        var identityJson = JsonSerializer.SerializeToUtf8Bytes(identity, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        foreach (var entryName in new[]
        {
            "../escape", "/absolute", "webui/../escape",
            ".env", ".env.example", "webui/.env", "webui/.env.example",
        })
        {
            var archive = Path.Combine(directory.Path, Guid.NewGuid().ToString("N") + ".zip");
            await WriteArchiveAsync(archive, [(entryName, Encoding.UTF8.GetBytes("nope"))]);
            var manifest = CreateManifest(archive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
            await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
                archive, Path.Combine(directory.Path, Guid.NewGuid().ToString("N")), manifest,
                manifest.Packages[0], CancellationToken.None));
        }

        var symlinkArchive = Path.Combine(directory.Path, "symlink.zip");
        await WriteSymlinkAsync(symlinkArchive, "webui/index.html", "../../outside");
        var symlinkManifest = CreateManifest(symlinkArchive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            symlinkArchive, Path.Combine(directory.Path, "symlink-stage"), symlinkManifest,
            symlinkManifest.Packages[0], CancellationToken.None));

        var duplicateArchive = Path.Combine(directory.Path, "duplicate.zip");
        await WriteArchiveAsync(duplicateArchive,
        [
            ("v2rayN.WebAPI", NativeExecutableFixture()),
            ("v2rayN.WebAPI.build.json", identityJson),
            ("v2rayN.WebAPI.build.json", identityJson),
        ]);
        var duplicateManifest = CreateManifest(duplicateArchive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            duplicateArchive, Path.Combine(directory.Path, "duplicate-stage"), duplicateManifest,
            duplicateManifest.Packages[0], CancellationToken.None));

        var unexpectedArchive = Path.Combine(directory.Path, "unexpected.zip");
        await WriteArchiveAsync(unexpectedArchive,
        [
            ("v2rayN.WebAPI", NativeExecutableFixture()),
            ("v2rayN.WebAPI.build.json", identityJson),
            ("webui/index.html", Encoding.UTF8.GetBytes("THIRD_PARTY_UI_TEST")),
        ]);
        var unexpectedManifest = CreateManifest(unexpectedArchive, identity.Version, identity.Commit, "linux-x64", identity.BuildDate);
        await ExpectInvalidDataAsync(() => WebUpdatePackageStager.VerifyAndExtractAsync(
            unexpectedArchive, Path.Combine(directory.Path, "unexpected-stage"), unexpectedManifest,
            unexpectedManifest.Packages[0], CancellationToken.None));
    }

    [Test]
    public async Task ScriptsTruncatedImagesAndWrongArchitectureCannotReachProcessReplacement()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "candidate");
        foreach (var image in new[] { Encoding.UTF8.GetBytes("#!/bin/sh\nexit 1\n"), NativeExecutableFixture()[..64], NativeExecutableFixture(183) })
        {
            await File.WriteAllBytesAsync(path, image);
            var rejected = false;
            try { WebUpdatePackageStager.RequireNativeExecutable(path, "linux-x64"); }
            catch (InvalidDataException) { rejected = true; }
            await rejected.Should().BeTrue();
        }
        await File.WriteAllBytesAsync(path, NativeExecutableFixture());
        WebUpdatePackageStager.RequireNativeExecutable(path, "linux-x64");
    }

    private static byte[] NativeExecutableFixture(ushort machine = 62)
    {
        var image = new byte[120]; // Minimal ELF/program-table fixture, never executed.
        new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1 }.CopyTo(image, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(16), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(18), machine);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(32), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(52), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(54), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(56), 1);
        return image;
    }

    private static WebUpdateManifest CreateManifest(string archive, string version, string commit, string rid, string buildDate)
    {
        using var stream = File.OpenRead(archive);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var asset = WebUpdatePackageStager.AppOnlyAssetName(rid)
            ?? throw new InvalidOperationException($"No app-only asset for {rid}.");
        return new WebUpdateManifest("v2rayN.WebAPI", version, commit, buildDate,
        [new WebUpdatePackage(rid, asset,
            $"https://github.com/2dust/v2rayN/releases/download/{version}/{asset}",
            hash, new FileInfo(archive).Length)]);
    }

    private static async Task WriteArchiveAsync(string path, IReadOnlyList<(string Name, byte[] Content)> files)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in files)
        {
            var entry = archive.CreateEntry(item.Name, CompressionLevel.SmallestSize);
            await using var stream = entry.Open();
            await stream.WriteAsync(item.Content);
        }
    }

    private static async Task WriteSymlinkAsync(string path, string name, string target)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(name);
        entry.ExternalAttributes = (0xA000 | 0x1FF) << 16;
        await using var stream = entry.Open();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(target));
    }

    private static async Task ExpectInvalidDataAsync(Func<Task<WebUpdatePackageIdentity>> action)
    {
        var rejected = false;
        try { _ = await action(); }
        catch (InvalidDataException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-package-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
