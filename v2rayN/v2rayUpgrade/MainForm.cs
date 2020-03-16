using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace v2rayUpgrade
{
    public partial class MainForm : Form
    {
        private readonly string defaultFilename = "v2ray-windows.zip";
        private string fileName;

        public MainForm(string[] args)
        {
            InitializeComponent();
            if (args.Length > 0)
            {
                fileName = args[0];
            }
        }
        private void showWarn(string message)
        {
            MessageBox.Show(message, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            try
            {
                Process[] existing = Process.GetProcessesByName("v2rayN");
                foreach (Process p in existing)
                {
                    string path = p.MainModule.FileName;
                    if (path == GetPath("v2rayN.exe"))
                    {
                        p.Kill();
                        p.WaitForExit(100);
                    }
                }
            }
            catch (Exception ex)
            {
                showWarn("Failed to close v2rayN(关闭v2rayN失败)." + ex.StackTrace);
                return;
            }

            try
            {

                if (!File.Exists(fileName))
                {
                    if (File.Exists(defaultFilename))
                    {
                        fileName = defaultFilename;
                    }
                    else
                    {
                        showWarn("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                        return;
                    }
                }

                string startKey = "v2rayN/";

                using (ZipArchive archive = ZipFile.OpenRead(fileName))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Length == 0)
                        {
                            continue;
                        }
                        string fullName = entry.FullName;
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
                showWarn("Upgrade Failed(升级失败)." + ex.StackTrace);
                return;
            }

            Process.Start("v2rayN.exe");
            MessageBox.Show("Upgrade successed(升级成功)", "", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
            return Application.StartupPath;
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
