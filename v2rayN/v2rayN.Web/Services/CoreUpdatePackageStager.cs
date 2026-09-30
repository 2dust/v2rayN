using System.Formats.Tar;
using System.IO.Compression;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Manager;

namespace v2rayN.Web.Services;

internal static class CoreUpdatePackageStager
{
    private const int MaxPackageEntries = 4096;
    private const long MaxExpandedBytes = 512L * 1024 * 1024;
    private const long MaxArchiveBytes = 512L * 1024 * 1024;

    public static bool SupportsCore(ECoreType coreType) => coreType is ECoreType.Xray or ECoreType.mihomo or ECoreType.sing_box;

    public static bool IsSupported(ECoreType coreType, string archivePath)
    {
        var name = archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            ? ".tar.gz"
            : Path.GetExtension(archivePath);
        return coreType switch
        {
            ECoreType.Xray => name.Equals(".zip", StringComparison.OrdinalIgnoreCase),
            ECoreType.mihomo => name.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".zip", StringComparison.OrdinalIgnoreCase),
            ECoreType.sing_box => name.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".zip", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public static async Task ExtractAsync(ECoreType coreType, string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        var archiveInfo = new FileInfo(archivePath);
        if (!archiveInfo.Exists || archiveInfo.Length is <= 0 or > MaxArchiveBytes)
        {
            throw new InvalidDataException("The core update archive is empty or exceeds the size limit.");
        }
        if (!IsSupported(coreType, archivePath))
        {
            throw new InvalidDataException($"The downloaded archive format is not supported for {coreType}.");
        }

        Directory.CreateDirectory(destinationDirectory);
        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractTarGzipAsync(archivePath, destinationDirectory, cancellationToken);
        }
        else if (Path.GetExtension(archivePath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractGzipBinaryAsync(coreType, archivePath, destinationDirectory, cancellationToken);
        }
        else
        {
            await ExtractZipAsync(coreType, archivePath, destinationDirectory, cancellationToken);
        }
    }

    private static async Task ExtractZipAsync(ECoreType coreType, string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        if (files.Length is 0 or > MaxPackageEntries || files.Sum(entry => entry.Length) > MaxExpandedBytes)
        {
            throw new InvalidDataException("The core update ZIP has an invalid file count or expanded size.");
        }

        var writtenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = GetSafeFlatFileName(entry.FullName, entry.ExternalAttributes);
            // The desktop updater deliberately leaves bundled Geo data untouched; GeoFiles
            // are updated independently and are not part of a Core binary replacement.
            if (coreType == ECoreType.Xray && fileName.StartsWith("geo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            EnsureUniqueName(writtenNames, fileName);
            await using var source = entry.Open();
            await using var target = new FileStream(
                Path.Combine(destinationDirectory, fileName),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await CopyWithLimitAsync(source, target, MaxExpandedBytes, cancellationToken);
        }

        if (writtenNames.Count == 0)
        {
            throw new InvalidDataException("The core update ZIP contains no applicable files.");
        }
    }

    private static async Task ExtractGzipBinaryAsync(
        ECoreType coreType,
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var executableName = CoreInfoManager.Instance.GetCoreInfo(coreType)?.CoreExes?.FirstOrDefault()
            ?? coreType.ToString();
        var destination = Path.Combine(destinationDirectory, Utils.GetExeName(executableName));
        await using var source = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await CopyWithLimitAsync(gzip, target, MaxExpandedBytes, cancellationToken);
    }

    private static async Task ExtractTarGzipAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var source = File.OpenRead(archivePath);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        var writtenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        var entryCount = 0;

        while (reader.GetNextEntry() is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entryCount++;
            if (entryCount > MaxPackageEntries)
            {
                throw new InvalidDataException("The core update tarball has too many entries.");
            }

            if (entry.EntryType == TarEntryType.Directory)
            {
                continue;
            }
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile))
            {
                throw new InvalidDataException("The core update tarball contains a link or unsupported entry type.");
            }
            if (entry.Length < 0 || entry.Length > MaxExpandedBytes)
            {
                throw new InvalidDataException("The core update tarball has an invalid file size.");
            }

            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > MaxExpandedBytes)
            {
                throw new InvalidDataException("The core update tarball exceeds the expanded size limit.");
            }

            var fileName = GetSafeFlatFileName(entry.Name, externalAttributes: 0);
            EnsureUniqueName(writtenNames, fileName);
            if (entry.DataStream is null)
            {
                throw new InvalidDataException("The core update tarball contains a file without data.");
            }

            await using var target = new FileStream(
                Path.Combine(destinationDirectory, fileName),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await CopyWithLimitAsync(entry.DataStream, target, MaxExpandedBytes, cancellationToken);
        }

        if (writtenNames.Count == 0)
        {
            throw new InvalidDataException("The core update tarball contains no regular files.");
        }
    }

    private static string GetSafeFlatFileName(string path, int externalAttributes)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(normalized)
            || segments.Length == 0
            || segments.Any(segment => segment is "." or "..")
            || normalized.Contains(':')
            || ((externalAttributes >> 16) & 0xF000) == 0xA000)
        {
            throw new InvalidDataException("The core update archive contains an unsafe path or symbolic link.");
        }

        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            throw new InvalidDataException("The core update archive contains an invalid file name.");
        }
        return fileName;
    }

    private static void EnsureUniqueName(HashSet<string> writtenNames, string fileName)
    {
        if (!writtenNames.Add(fileName))
        {
            throw new InvalidDataException("The core update archive contains duplicate file names after flattening.");
        }
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream target, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }
            total += read;
            if (total > limit)
            {
                throw new InvalidDataException("The core update archive expands beyond the size limit.");
            }
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
