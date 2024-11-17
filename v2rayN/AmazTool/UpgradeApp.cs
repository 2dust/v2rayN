﻿/**
 * 该程序使用JSON文件对C#应用程序进行本地化。
 * 程序根据系统当前的语言加载相应的语言文件。
 * 如果当前语言不被支持，则默认使用英语。
 * 
 * 库:
 *  - System.Collections.Generic
 *  - System.Globalization
 *  - System.IO
 *  - System.Text.Json
 * 
 * 用法:
 *  - 为每种支持的语言创建JSON文件（例如，en-US.json，zh-CN.json）。
 *  - 将JSON文件放置程序同目录中。
 *  - 运行程序，它将根据系统当前的语言加载翻译。
 *  - 调用方式： localization.Translate("Try_Terminate_Process") //返回一个 string 字符串
 * 示例JSON文件（en.json）：
 * {
 *     "Restart_v2rayN": "Start v2rayN, please wait...",
 *     "Guidelines": "Please run it from the main application."
 * }
 * 
 * 示例JSON文件（zh.json）：
 * {
 *     "Restart_v2rayN": "正在重启，请等待...",
 *     "Guidelines": "请从主应用运行！"
 * }
 */

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace AmazTool
{
    internal class UpgradeApp
    {
        // 定义常量
        private static readonly string V2rayN = "v2rayN";
        private static readonly string SplitKey = "/";

        public static void Upgrade(string fileName)
        {
            var localization = new Localization();

            Console.WriteLine(fileName);
            Thread.Sleep(9000);

            if (!File.Exists(fileName))
            {
                // 如果文件不存在，输出相应的本地化信息
                Console.WriteLine(localization.Translate("Upgrade_File_Not_Found"));
                return;
            }

            // 尝试终止进程
            TerminateProcess(localization);

            // 解压缩更新包
            ExtractUpdatePackage(fileName, localization);

            // 重启进程
            Console.WriteLine(localization.Translate("Restart_v2rayN"));
            Thread.Sleep(9000);
            RestartProcess();
        }

        private static void TerminateProcess(Localization localization)
        {
            Console.WriteLine(localization.Translate("Try_Terminate_Process"));
            try
            {
                var processes = Process.GetProcessesByName(V2rayN);
                foreach (var process in processes)
                {
                    process?.Kill();
                    process?.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                // 如果无法终止进程，输出相应的本地化信息和错误堆栈
                Console.WriteLine(localization.Translate("Failed_Terminate_Process") + ex.StackTrace);
            }
        }

        private static void ExtractUpdatePackage(string fileName, Localization localization)
        {
            Console.WriteLine(localization.Translate("Start_Unzipping"));
            StringBuilder errorLog = new();

            try
            {
                string backupFilePath = $"{GetExePath()}.tmp";
                File.Delete(backupFilePath);

                using ZipArchive archive = ZipFile.OpenRead(fileName);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    try
                    {
                        if (entry.Length == 0)
                            continue;

                        Console.WriteLine(entry.FullName);

                        string fullPath = GetEntryFullPath(entry.FullName);
                        BackupExistingFile(fullPath, backupFilePath);

                        string entryOutputPath = GetPath(fullPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);
                        entry.ExtractToFile(entryOutputPath, true);

                        Console.WriteLine(entryOutputPath);
                    }
                    catch (Exception ex)
                    {
                        errorLog.Append(ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果解压失败，输出相应的本地化信息和错误堆栈
                Console.WriteLine(localization.Translate("Failed_Upgrade") + ex.StackTrace);
            }

            if (errorLog.Length > 0)
            {
                // 如果有任何错误记录，输出相应的本地化信息和错误日志
                Console.WriteLine(localization.Translate("Failed_Upgrade") + errorLog.ToString());
            }
        }

        private static void BackupExistingFile(string fullPath, string backupFilePath)
        {
            if (string.Equals(GetExePath(), fullPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(GetExePath(), backupFilePath);
            }
        }

        private static string GetEntryFullPath(string entryName)
        {
            var parts = entryName.Split(SplitKey);
            return parts.Length > 1 ? string.Join(SplitKey, parts[1..]) : entryName;
        }

        private static void RestartProcess()
        {
            Process process = new()
            {
                StartInfo = new()
                {
                    UseShellExecute = true,
                    FileName = V2rayN,
                    WorkingDirectory = StartupPath()
                }
            };
            process.Start();
        }

        private static string GetExePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }

        private static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetPath(string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? StartupPath() : Path.Combine(StartupPath(), fileName);
        }
    }
}
