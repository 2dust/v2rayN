﻿using PacLib;
using System.Diagnostics;
using System.IO;
using System.Text;
using v2rayN.Mode;
using v2rayN.Properties;
using v2rayN.Tool;

namespace v2rayN.Handler;

public static class SysProxyHandle
{
    //private const string _userWininetConfigFile = "user-wininet.json";

    //private static string _queryStr;

    // In general, this won't change
    // format:
    //  <flags><CR-LF>
    //  <proxy-server><CR-LF>
    //  <bypass-list><CR-LF>
    //  <pac-url>
    private static SysproxyConfig? _userSettings = null;

    private enum RET_ERRORS : int
    {
        RET_NO_ERROR = 0,
        INVALID_FORMAT = 1,
        NO_PERMISSION = 2,
        SYSCALL_FAILED = 3,
        NO_MEMORY = 4,
        INVAILD_OPTION_COUNT = 5,
    };

    static SysProxyHandle()
    {
        try
        {
            FileManager.UncompressFile(Utils.GetTempPath("sysproxy.exe"),
                Environment.Is64BitOperatingSystem ? Resources.sysproxy64_exe : Resources.sysproxy_exe);
        }
        catch (IOException ex)
        {
            Utils.SaveLog(ex.Message, ex);
        }
    }

    public static bool UpdateSysProxy(Config config, bool forceDisable)
    {
        var type = config.sysProxyType;

        if (forceDisable && type != ESysProxyType.Unchanged)
        {
            type = ESysProxyType.ForcedClear;
        }

        try
        {
            int port = LazyConfig.Instance.GetLocalPort(Global.InboundHttp);
            int portSocks = LazyConfig.Instance.GetLocalPort(Global.InboundSocks);
            int portPac = LazyConfig.Instance.GetLocalPort(ESysProxyType.Pac.ToString());
            if (port <= 0)
            {
                return false;
            }
            if (type == ESysProxyType.ForcedChange)
            {
                var strExceptions = $"{config.constItem.defIEProxyExceptions};{config.systemProxyExceptions}";

                var strProxy = string.Empty;
                if (Utils.IsNullOrEmpty(config.systemProxyAdvancedProtocol))
                {
                    strProxy = $"{Global.Loopback}:{port}";
                }
                else
                {
                    strProxy = config.systemProxyAdvancedProtocol
                        .Replace("{ip}", Global.Loopback)
                        .Replace("{http_port}", port.ToString())
                        .Replace("{socks_port}", portSocks.ToString());
                }
                ProxySetting.SetProxy(strProxy, strExceptions, 2);
                SetIEProxy(true, strProxy, strExceptions);
            }
            else if (type == ESysProxyType.ForcedClear)
            {
                ProxySetting.UnsetProxy();
                ResetIEProxy();
            }
            else if (type == ESysProxyType.Unchanged)
            {
            }
            else if (type == ESysProxyType.Pac)
            {
                PacHandler.Start(Utils.GetConfigPath(), port, portPac);
                var strProxy = $"{Global.httpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
                ProxySetting.SetProxy(strProxy, "", 4);
                SetIEProxy(false, strProxy, "");
            }

            if (type != ESysProxyType.Pac)
            {
                PacHandler.Stop();
            }
        }
        catch (Exception ex)
        {
            Utils.SaveLog(ex.Message, ex);
        }
        return true;
    }

    public static void ResetIEProxy4WindowsShutDown()
    {
        try
        {
            //TODO To be verified
            Utils.RegWriteValue(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyEnable", 0);
        }
        catch
        {
        }
    }

    public static void SetIEProxy(bool global, string strProxy, string strExceptions)
    {
        string arguments = global
            ? $"global {strProxy} {strExceptions}"
            : $"pac {strProxy}";

        ExecSysproxy(arguments);
    }

    // set system proxy to 1 (null) (null) (null)
    public static void ResetIEProxy()
    {
        ExecSysproxy("set 1 - - -");
    }

    private static void ExecSysproxy(string arguments)
    {
        // using event to avoid hanging when redirect standard output/error
        // ref: https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
        // and http://blog.csdn.net/zhangweixing0/article/details/7356841
        using AutoResetEvent outputWaitHandle = new(false);
        using AutoResetEvent errorWaitHandle = new(false);
        using Process process = new();

        // Configure the process using the StartInfo properties.
        process.StartInfo.FileName = Utils.GetTempPath("sysproxy.exe");
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = Utils.GetTempPath();
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        // Need to provide encoding info, or output/error strings we got will be wrong.
        process.StartInfo.StandardOutputEncoding = Encoding.Unicode;
        process.StartInfo.StandardErrorEncoding = Encoding.Unicode;

        process.StartInfo.CreateNoWindow = true;

        StringBuilder output = new(1024);
        StringBuilder error = new(1024);

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data == null)
            {
                outputWaitHandle.Set();
            }
            else
            {
                output.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null)
            {
                errorWaitHandle.Set();
            }
            else
            {
                error.AppendLine(e.Data);
            }
        };
        try
        {
            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // log the arguments
            throw new Exception(process.StartInfo.Arguments);
        }
        string stderr = error.ToString();
        string stdout = output.ToString();

        int exitCode = process.ExitCode;
        if (exitCode != (int)RET_ERRORS.RET_NO_ERROR)
        {
            throw new Exception(stderr);
        }

        //if (arguments == "query")
        //{
        //    if (stdout.IsNullOrWhiteSpace() || stdout.IsNullOrEmpty())
        //    {
        //        throw new Exception("failed to query wininet settings");
        //    }
        //    _queryStr = stdout;
        //}
    }
}