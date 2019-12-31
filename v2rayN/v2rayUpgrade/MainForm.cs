﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace v2rayUpgrade
{
    public partial class MainForm : Form
    {
        private string[] _args;


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
                var fileName = _args[0];
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
