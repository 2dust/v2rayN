using System.IO.Compression;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServiceLib.Models.Dto;

namespace v2rayN.WebAPI.Services;

public sealed record WebUpdatePackage(string Rid, string Asset, string Url, string Sha256, long Size);

public sealed record WebUpdateManifest(
    string Product,
    string Version,
    string Commit,
    string BuildDate,
    IReadOnlyList<WebUpdatePackage> Packages);

public sealed record WebUpdatePackageIdentity(string Product, string Version, string Commit, string BuildDate, string Rid);

internal static partial class WebUpdatePackageStager
{
    internal const long MaximumArchiveBytes = 256L * 1024 * 1024;
    internal const long MaximumExpandedBytes = 1024L * 1024 * 1024;
    private const int MaximumEntries = 10000;
    private const string LinuxExecutableName = "v2rayN.WebAPI";
    private const string WindowsExecutableName = "v2rayN.WebAPI.exe";

    public static bool IsValidVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && VersionPattern().IsMatch(value);

    public static bool IsUpdateAvailable(
        string currentVersion,
        string candidateVersion,
        bool allowPrerelease,
        string? currentCommit = null,
        string? candidateCommit = null)
    {
        if (!IsValidVersion(currentVersion) || !IsValidVersion(candidateVersion)) return false;
        var current = new SemanticVersion(currentVersion);
        var candidate = new SemanticVersion(candidateVersion);
        var comparison = candidate.CompareTo(current);
        if (comparison > 0) return true;
        return comparison == 0
            && !string.IsNullOrWhiteSpace(candidateCommit)
            && !string.IsNullOrWhiteSpace(currentCommit)
            && !string.Equals(candidateCommit, currentCommit, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldConsiderRelease(bool isPrerelease, bool allowPrerelease) => !isPrerelease || allowPrerelease;

    public static WebUpdateManifest ParseManifest(string json)
    {
        var manifest = JsonSerializer.Deserialize<WebUpdateManifest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The Web release manifest is empty.");
        if (manifest.Product == "v2rayN.Web")
            throw new InvalidDataException("Legacy v2rayN.Web packages require a one-time manual installation of v2rayN.WebAPI.");
        if (manifest.Product != "v2rayN.WebAPI" || !IsValidVersion(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Commit) || manifest.Packages is null || manifest.Packages.Count is 0 or > 8)
        {
            throw new InvalidDataException("The Web release manifest has an invalid product identity.");
        }
        if (manifest.Packages.Select(package => package.Rid).Distinct(StringComparer.Ordinal).Count() != manifest.Packages.Count)
        {
            throw new InvalidDataException("The Web release manifest contains duplicate runtime identifiers.");
        }
        foreach (var package in manifest.Packages)
        {
            if (package.Rid is not ("linux-x64" or "linux-arm64" or "win-x64")
                || string.IsNullOrWhiteSpace(package.Asset)
                || !Uri.TryCreate(package.Url, UriKind.Absolute, out var assetUri)
                || assetUri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(package.Sha256)
                || !Regex.IsMatch(package.Sha256, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)
                || package.Size is <= 0 or > MaximumArchiveBytes)
            {
                throw new InvalidDataException("The Web release manifest contains an unsafe or unsupported package entry.");
            }
        }
        return manifest;
    }

    public static WebUpdatePackage RequirePackage(WebUpdateManifest manifest, string rid)
    {
        if (rid is not ("linux-x64" or "linux-arm64" or "win-x64"))
            throw new InvalidDataException($"The Web release does not support runtime identifier {rid}.");
        return manifest.Packages.FirstOrDefault(package => package.Rid == rid)
            ?? throw new InvalidDataException($"The Web release has no package for runtime identifier {rid}.");
    }

    /// <summary>
    /// Maps a supported .NET runtime identifier to the release archive architecture suffix
    /// (<c>linux-x64</c>/<c>win-x64</c> become <c>64</c>; <c>linux-arm64</c> stays <c>arm64</c>).
    /// </summary>
    public static string? ArtifactArch(string? rid) => WebReleaseAssets.ArtifactArch(rid);

    public static string? FullInstallAssetName(string? rid) => WebReleaseAssets.FullAssetName(rid);

    public static string? AppOnlyAssetName(string? rid) => WebReleaseAssets.UpdateAssetName(rid);

    public static async Task<WebUpdatePackageIdentity> VerifyAndExtractAsync(
        string archivePath,
        string destinationPath,
        WebUpdateManifest manifest,
        WebUpdatePackage package,
        CancellationToken cancellationToken)
    {
        var archive = new FileInfo(archivePath);
        if (!archive.Exists || archive.Length != package.Size || archive.Length is <= 0 or > MaximumArchiveBytes)
            throw new InvalidDataException("The downloaded Web package size does not match its manifest.");

        await using (var stream = File.OpenRead(archivePath))
        {
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            var expectedHash = Convert.FromHexString(package.Sha256);
            if (!CryptographicOperations.FixedTimeEquals(hash, expectedHash))
                throw new InvalidDataException("The downloaded Web package SHA-256 does not match its manifest.");
        }

        if (Directory.Exists(destinationPath) || File.Exists(destinationPath))
            throw new InvalidDataException("The Web package staging directory already exists.");
        Directory.CreateDirectory(destinationPath);
        var root = Path.GetFullPath(destinationPath);
        if (new DirectoryInfo(root).LinkTarget is not null)
            throw new InvalidDataException("The Web package staging root must not be a symbolic link.");
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        long expandedBytes = 0;
        var entryCount = 0;
        var extractedFiles = new HashSet<string>(StringComparer.Ordinal);
        using (var zip = ZipFile.OpenRead(archivePath))
        {
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entryCount > MaximumEntries)
                    throw new InvalidDataException("The Web package has too many archive entries.");
                var relative = NormalizeEntryName(entry.FullName);
                var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(rootPrefix, StringComparison.Ordinal))
                    throw new InvalidDataException("The Web package contains a path outside its staging directory.");
                if (IsEnvironmentConfigurationPath(relative))
                    throw new InvalidDataException("The Web package must not contain .env or .env.example files.");
                if (!IsAllowedPath(relative, package.Rid))
                    throw new InvalidDataException($"The Web package contains an unexpected file: {relative}.");

                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType is not (0 or 0x4000 or 0x8000))
                    throw new InvalidDataException("The Web package contains a link or special archive entry.");
                if (unixType == 0x4000 || entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                if (!extractedFiles.Add(relative))
                    throw new InvalidDataException("The Web package contains duplicate file paths.");
                if (entry.Length < 0 || entry.Length > MaximumExpandedBytes - expandedBytes)
                    throw new InvalidDataException("The expanded Web package exceeds the configured size limit.");
                expandedBytes += entry.Length;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
                await CopyWithLimitAsync(input, output, entry.Length, cancellationToken);
            }
        }

        var executableName = ExecutableNameForRid(package.Rid);
        var executable = Path.Combine(root, executableName);
        var identityPath = Path.Combine(root, "v2rayN.WebAPI.build.json");
        if (!File.Exists(executable) || new FileInfo(executable).Length == 0
            || !File.Exists(identityPath))
        {
            throw new InvalidDataException("The Web package is missing its executable or build identity.");
        }
        RequireNativeExecutable(executable, package.Rid);

        var identity = JsonSerializer.Deserialize<WebUpdatePackageIdentity>(
            await File.ReadAllTextAsync(identityPath, cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The Web package build identity is invalid.");
        if (identity.Product != "v2rayN.WebAPI" || identity.Version != manifest.Version
            || identity.Commit != manifest.Commit || identity.BuildDate != manifest.BuildDate || identity.Rid != package.Rid)
        {
            throw new InvalidDataException("The Web package identity does not match the verified release manifest.");
        }

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(executable,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        return identity;
    }

    private static string NormalizeEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains('\0') || name.StartsWith('/'))
            throw new InvalidDataException("The Web package contains an invalid archive path.");
        var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
            throw new InvalidDataException("The Web package contains a path traversal entry.");
        var normalized = string.Join('/', segments);
        if (normalized.Length == 0) throw new InvalidDataException("The Web package contains an empty archive path.");
        return normalized;
    }

    private static bool IsAllowedPath(string path, string rid) =>
        path == ExecutableNameForRid(rid) || path == "v2rayN.WebAPI.build.json";

    // JSON RID alone cannot prove executable identity. Reject wrong-architecture,
    // truncated and script payloads before shutdown or process replacement.
    internal static string ExecutableNameForRid(string rid) => rid switch
    {
        "linux-x64" or "linux-arm64" => LinuxExecutableName,
        "win-x64" => WindowsExecutableName,
        _ => throw new InvalidDataException($"Unsupported WebAPI runtime identifier: {rid}."),
    };

    internal static void RequireNativeExecutable(string path, string rid)
    {
        using var stream = File.OpenRead(path);
        if (rid == "win-x64")
        {
            RequireWindowsExecutable(stream);
            return;
        }
        if (rid is not ("linux-x64" or "linux-arm64"))
            throw new InvalidDataException($"Unsupported WebAPI runtime identifier: {rid}.");

        Span<byte> header = stackalloc byte[64];
        if (stream.Read(header) != header.Length
            || !header[..7].SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1 })
            || BinaryPrimitives.ReadUInt16LittleEndian(header[16..]) is not (2 or 3)
            || BinaryPrimitives.ReadUInt16LittleEndian(header[18..]) != (rid == "linux-x64" ? 62 : rid == "linux-arm64" ? 183 : 0)
            || BinaryPrimitives.ReadUInt32LittleEndian(header[20..]) != 1
            || BinaryPrimitives.ReadUInt16LittleEndian(header[52..]) != 64)
            throw new InvalidDataException("The WebAPI package executable must be a Linux ELF64 image for its declared runtime identifier.");
        var offset = BinaryPrimitives.ReadUInt64LittleEndian(header[32..]);
        var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(header[54..]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(header[56..]);
        if (offset < 64 || offset > (ulong)stream.Length || entrySize != 56 || count == 0
            || (ulong)count * entrySize > (ulong)stream.Length - offset)
            throw new InvalidDataException("The WebAPI package executable has a truncated or invalid ELF program table.");
    }

    private static void RequireWindowsExecutable(Stream stream)
    {
        Span<byte> dosHeader = stackalloc byte[64];
        if (stream.Length < dosHeader.Length || stream.Read(dosHeader) != dosHeader.Length
            || BinaryPrimitives.ReadUInt16LittleEndian(dosHeader) != 0x5A4D)
        {
            throw new InvalidDataException("The WebAPI package executable must be a Windows PE32+ image for x64.");
        }

        var peOffset = BinaryPrimitives.ReadUInt32LittleEndian(dosHeader[0x3C..]);
        if (peOffset < dosHeader.Length || peOffset > stream.Length - 26)
            throw new InvalidDataException("The WebAPI package executable has a truncated Windows PE header.");

        stream.Position = peOffset;
        Span<byte> peHeader = stackalloc byte[26];
        if (stream.Read(peHeader) != peHeader.Length
            || !peHeader[..4].SequenceEqual(new byte[] { (byte)'P', (byte)'E', 0, 0 })
            || BinaryPrimitives.ReadUInt16LittleEndian(peHeader[4..]) != 0x8664
            || BinaryPrimitives.ReadUInt16LittleEndian(peHeader[6..]) == 0
            || BinaryPrimitives.ReadUInt16LittleEndian(peHeader[20..]) < 2
            || (BinaryPrimitives.ReadUInt16LittleEndian(peHeader[22..]) & 0x0002) == 0)
        {
            throw new InvalidDataException("The WebAPI package executable must be a Windows PE32+ image for x64.");
        }

        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[20..]);
        if ((long)peOffset + 24 + optionalHeaderSize > stream.Length)
            throw new InvalidDataException("The WebAPI package executable has a truncated Windows optional header.");
        Span<byte> optionalHeaderMagic = stackalloc byte[2];
        stream.Position = (long)peOffset + 24;
        if (stream.Read(optionalHeaderMagic) != optionalHeaderMagic.Length
            || BinaryPrimitives.ReadUInt16LittleEndian(optionalHeaderMagic) != 0x20B)
        {
            throw new InvalidDataException("The WebAPI package executable must be a Windows PE32+ image for x64.");
        }
    }

    private static bool IsEnvironmentConfigurationPath(string path) =>
        path.Split('/').Any(segment => segment is ".env" or ".env.example");

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long expectedLength, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        long copied = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            copied += read;
            if (copied > expectedLength || copied > MaximumExpandedBytes)
                throw new InvalidDataException("A Web package archive entry exceeded its declared size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (copied != expectedLength) throw new InvalidDataException("A Web package archive entry was truncated.");
    }

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
