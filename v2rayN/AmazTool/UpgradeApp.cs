using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AmazTool;

internal class UpgradeApp
{
    /// <summary>解压条目数上限，防止高压缩比的小包撑爆目录树。</summary>
    private const int MaxEntryCount = 20000;

    /// <summary>单个条目的解压体积上限。</summary>
    private const long MaxSingleEntryBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    /// <summary>全部条目的解压总体积上限。</summary>
    private const long MaxTotalUncompressedBytes = 4L * 1024 * 1024 * 1024; // 4 GB

    public static void Upgrade(string fileName)
    {
        Console.WriteLine($"{Resx.Resource.StartUnzipping}\n{fileName}");

        Utils.Waiting(5);

        if (!File.Exists(fileName))
        {
            Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
            return;
        }

        Console.WriteLine(Resx.Resource.TryTerminateProcess);
        try
        {
            var existing = Process.GetProcessesByName(Utils.V2rayN);
            foreach (var pp in existing)
            {
                var path = pp.MainModule?.FileName ?? "";
                if (path.StartsWith(Utils.GetPath(Utils.V2rayN)))
                {
                    pp?.Kill();
                    pp?.WaitForExit(1000);
                }
            }
        }
        catch (Exception ex)
        {
            // Access may be denied without admin right. The user may not be an administrator.
            Console.WriteLine(Resx.Resource.FailedTerminateProcess + ex.StackTrace);
        }

        Console.WriteLine(Resx.Resource.StartUnzipping);
        StringBuilder sb = new();
        var thisAppOldFile = $"{Utils.GetExePath()}.tmp";
        var exeMoved = false;

        var entryCount = 0;
        long totalBytes = 0;
        var failed = new List<string>();
        string? fatal = null;

        try
        {
            File.Delete(thisAppOldFile);
            var splitKey = "/";

            using var archive = ZipFile.OpenRead(fileName);

            foreach (var entry in archive.Entries)
            {
                try
                {
                    if (entry.Length == 0)
                    {
                        continue;
                    }

                    // ---- 解压上限检查（越限即视为恶意包，中止） ----
                    entryCount++;
                    if (entryCount > MaxEntryCount)
                    {
                        fatal = $"too many entries (limit {MaxEntryCount})";
                        break;
                    }
                    if (entry.Length > MaxSingleEntryBytes)
                    {
                        fatal = $"entry too large: {entry.FullName} ({entry.Length} bytes)";
                        break;
                    }
                    totalBytes += entry.Length;
                    if (totalBytes > MaxTotalUncompressedBytes)
                    {
                        fatal = $"total uncompressed size exceeds limit ({MaxTotalUncompressedBytes} bytes)";
                        break;
                    }

                    Console.WriteLine(entry.FullName);

                    var lst = entry.FullName.Split(splitKey);
                    if (lst.Length == 1)
                    {
                        continue;
                    }

                    var fullName = string.Join(splitKey, lst[1..lst.Length]);

                    // ---- 路径边界校验 ----
                    // 条目名来自网络，不可信；越界即视为攻击，中止整个升级
                    if (!Utils.TryGetEntryPath(fullName, out var entryOutputPath, out var reason))
                    {
                        fatal = $"entry escapes install directory: {entry.FullName} ({reason})";
                        break;
                    }

                    if (string.Equals(Utils.GetExePath(), entryOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(Utils.GetExePath(), thisAppOldFile);
                        exeMoved = true;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);
                    //In the bin folder, if the file already exists, it will be skipped
                    if (fullName.StartsWith("bin") && File.Exists(entryOutputPath))
                    {
                        continue;
                    }

                    // ---- 返回值检查（原实现丢弃了返回值，失败被静默跳过） ----
                    if (!TryExtractToFile(entry, entryOutputPath))
                    {
                        failed.Add(entry.FullName);
                        Console.WriteLine($"FAILED: {entry.FullName}");
                        continue;
                    }

                    Console.WriteLine(entryOutputPath);
                }
                catch (Exception ex)
                {
                    failed.Add(entry.FullName);
                    sb.Append(ex.StackTrace);
                    Console.WriteLine($"FAILED: {entry.FullName} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // 包本身打不开等致命错误
            Console.WriteLine(Resx.Resource.FailedUpgrade + ex.StackTrace);
            RollBack(thisAppOldFile, ref exeMoved);
            Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
            return;
        }

        if (fatal != null)
        {
            Console.WriteLine(Resx.Resource.FailedUpgrade + fatal);
            RollBack(thisAppOldFile, ref exeMoved);
            Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
            return;
        }

        if (failed.Count > 0)
        {
            Console.WriteLine(Resx.Resource.FailedUpgrade + sb.ToString());
            Console.WriteLine($"Failed entries: {failed.Count}");
            // 有文件没解出来，主程序可能已被移走 —— 回滚，保证还能打开
            RollBack(thisAppOldFile, ref exeMoved);
            Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
            return;
        }

        // 全部成功：清掉备份
        try
        {
            File.Delete(thisAppOldFile);
        }
        catch
        {
            // 忽略
        }

        Console.WriteLine(Resx.Resource.Restartv2rayN);
        Utils.Waiting(2);

        Utils.StartV2RayN();
    }

    /// <summary>
    /// 主程序若已被移走，放回原位，避免"更新完反而打不开"。
    /// </summary>
    private static void RollBack(string thisAppOldFile, ref bool exeMoved)
    {
        if (!exeMoved || File.Exists(Utils.GetExePath()) || !File.Exists(thisAppOldFile))
        {
            return;
        }

        try
        {
            File.Move(thisAppOldFile, Utils.GetExePath());
            exeMoved = false;
            Console.WriteLine("Rolled back: the original executable has been restored.");
        }
        catch (Exception rex)
        {
            Console.WriteLine("Rollback failed: " + rex.Message);
            Console.WriteLine($"The original executable is kept at: {thisAppOldFile}");
        }
    }

    private static bool TryExtractToFile(ZipArchiveEntry entry, string outputPath)
    {
        var retryCount = 5;
        var delayMs = 1000;

        for (var i = 1; i <= retryCount; i++)
        {
            try
            {
                entry.ExtractToFile(outputPath, true);
                return true;
            }
            catch
            {
                Thread.Sleep(delayMs * i);
            }
        }
        return false;
    }
}
