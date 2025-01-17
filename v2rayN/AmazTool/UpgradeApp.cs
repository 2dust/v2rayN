using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AmazTool
{
    internal class UpgradeApp
    {
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
            try
            {
                var thisAppOldFile = $"{Utils.GetExePath()}.tmp";
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

                        Console.WriteLine(entry.FullName);

                        var lst = entry.FullName.Split(splitKey);
                        if (lst.Length == 1) continue;
                        var fullName = string.Join(splitKey, lst[1..lst.Length]);

                        if (string.Equals(Utils.GetExePath(), Utils.GetPath(fullName), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(Utils.GetExePath(), thisAppOldFile);
                        }

                        var entryOutputPath = Utils.GetPath(fullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);
                        //In the bin folder, if the file already exists, it will be skipped
                        if (fullName.StartsWith("bin") && File.Exists(entryOutputPath))
                        {
                            continue;
                        }
                        entry.ExtractToFile(entryOutputPath, true);

                        Console.WriteLine(entryOutputPath);
                    }
                    catch (Exception ex)
                    {
                        sb.Append(ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(Resx.Resource.FailedUpgrade + ex.StackTrace);
                //return;
            }
            if (sb.Length > 0)
            {
                Console.WriteLine(Resx.Resource.FailedUpgrade + sb.ToString());
                //return;
            }

            Console.WriteLine(Resx.Resource.Restartv2rayN);
            Utils.Waiting(2);

            Utils.StartV2RayN();
        }
    }
}