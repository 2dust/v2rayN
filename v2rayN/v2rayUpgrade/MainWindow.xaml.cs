using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;
using System.Windows;

namespace v2rayUpgrade;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultFilename = "v2rayN.zip_temp";
    private string _fileName;

    public MainWindow()
    {
        InitializeComponent();
        var args = Environment.GetCommandLineArgs();
        if (args.Length == 2)
        {
            _fileName = args[1];
            _fileName = HttpUtility.UrlDecode(_fileName);
        }
        else
        {
            _fileName = DefaultFilename;
        }
    }

    private static void ShowWarn(string message)
    {
        MessageBox.Show(message, "", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ButtonOK_OnClick(object sender, EventArgs e)
    {
        try
        {
            var existing = Process.GetProcessesByName("v2rayN");
            foreach (var p in existing)
            {
                var path = p.MainModule!.FileName;
                if (path != GetPath("v2rayN.exe")) continue;
                p.Kill();
                p.WaitForExit(100);
            }
        }
        catch (Exception ex)
        {
            // Access may be denied without admin right. The user may not be an administrator.
            ShowWarn(
                $"Failed to close v2rayN(关闭v2rayN失败).\nClose it manually, or the upgrade may fail.(请手动关闭正在运行的v2rayN，否则可能升级失败。\n\n{ex.StackTrace}");
        }

        var sb = new StringBuilder();
        try
        {
            if (!File.Exists(_fileName))
            {
                if (File.Exists(DefaultFilename))
                {
                    _fileName = DefaultFilename;
                }
                else
                {
                    ShowWarn("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                    return;
                }
            }

            var thisAppOldFile = ExePath() + ".tmp";
            File.Delete(thisAppOldFile);
            const string startKey = "v2rayN/";


            using var archive = ZipFile.OpenRead(_fileName);
            foreach (var entry in archive.Entries)
            {
                try
                {
                    if (entry.Length == 0)
                    {
                        continue;
                    }

                    var fullName = entry.FullName;
                    if (fullName.StartsWith(startKey))
                    {
                        fullName = fullName.Substring(startKey.Length, fullName.Length - startKey.Length);
                    }

                    if (string.Equals(ExePath(), GetPath(fullName), StringComparison.CurrentCultureIgnoreCase))
                    {
                        File.Move(ExePath(), thisAppOldFile);
                    }

                    var entryOutputPath = GetPath(fullName);

                    var fileInfo = new FileInfo(entryOutputPath);
                    fileInfo.Directory!.Create();
                    entry.ExtractToFile(entryOutputPath, true);
                }
                catch (Exception ex)
                {
                    sb.Append(ex.StackTrace);
                }
            }
        }
        catch (Exception ex)
        {
            ShowWarn("Upgrade Failed(升级失败)." + ex.StackTrace);
            return;
        }

        if (sb.Length > 0)
        {
            ShowWarn($"Upgrade Failed,Hold ctrl + c to copy to clipboard.\n(升级失败,按住ctrl+c可以复制到剪贴板).{sb}");
            return;
        }

        Process.Start("v2rayN.exe");
        MessageBox.Show("Upgrade succeeded(升级成功)", "", MessageBoxButton.OK, MessageBoxImage.Information);

        Close();
    }

    private void ButtonClose_OnClick(object sender, EventArgs e)
    {
        Close();
    }

    private static string ExePath()
    {
        return Process.GetCurrentProcess().MainModule.FileName;
    }

    private static string StartupPath()
    {
        return Path.GetDirectoryName(ExePath())!;
    }

    private static string GetPath(string fileName)
    {
        var startupPath = StartupPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return startupPath;
        }

        return Path.Combine(startupPath, fileName);
    }
}