using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace v2rayUpgrade
{
    public partial class MainForm : Form
    {
        private string[] _args;
        private string _tempFileName = "v2rayUpgradeTemp.zip";


        public MainForm(string[] args)
        {
            InitializeComponent();
            _args = args;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (_args.Length <= 0)
            {
                return;
            }

            try
            {
                Process[] existing = Process.GetProcessesByName("v2rayN");
                foreach (Process p in existing)
                {
                    var path = p.MainModule.FileName;
                    if (path == GetPath("v2rayN.exe"))
                    {
                        p.Kill();
                        p.WaitForExit(100);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to close v2rayN(关闭v2rayN失败)." + ex.StackTrace);
                return;
            }

            var fileName = GetPath(_tempFileName);
            try
            {
                File.Copy(_args[0], fileName);
                if (!File.Exists(fileName))
                {
                    MessageBox.Show("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                    return;
                }

                var startKey = "v2rayN/";

                using (ZipArchive archive = ZipFile.OpenRead(fileName))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
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

                        string entryOuputPath = GetPath(fullName);

                        FileInfo fileInfo = new FileInfo(entryOuputPath);
                        fileInfo.Directory.Create();
                        entry.ExtractToFile(entryOuputPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Upgrade Failed(升级失败)." + ex.StackTrace);
                return;
            }
            finally
            {
                File.Delete(fileName);
            }

            MessageBox.Show("Upgrade  successed(升级成功)");

            try
            {
                Process.Start("v2rayN.exe");
            }
            catch
            {
            }
            Close();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static string GetExePath()
        {
            return Application.ExecutablePath;
        }

        public static string StartupPath()
        {
            try
            {
                string exePath = GetExePath();
                return exePath.Substring(0, exePath.LastIndexOf("\\", StringComparison.Ordinal));
            }
            catch
            {
                return Application.StartupPath;
            }
        }
        public static string GetPath(string fileName)
        {
            string startupPath = StartupPath();
            if (string.IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }
    }
}
