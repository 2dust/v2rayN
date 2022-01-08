
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using v2rayN.Mode;
using v2rayN.Properties;
using v2rayN.Tool;

namespace v2rayN.HttpProxyHandler
{
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
        private static SysproxyConfig _userSettings = null;

        enum RET_ERRORS : int
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

        public static void SetIEProxy(bool enable, bool global, string strProxy)
        {
            //Read();

            //if (!_userSettings.UserSettingsRecorded)
            //{
            //    // record user settings
            //    ExecSysproxy("query");
            //    //ParseQueryStr(_queryStr);
            //}

            string arguments;
            if (enable)
            {
                arguments = global
                    ? $"global {strProxy} {Global.IEProxyExceptions}"
                    : $"pac {strProxy}";
            }
            else
            {
                // restore user settings
                string flags = _userSettings.Flags;
                string proxy_server = _userSettings.ProxyServer ?? "-";
                string bypass_list = _userSettings.BypassList ?? "-";
                string pac_url = _userSettings.PacUrl ?? "-";
                arguments = $"set {flags} {proxy_server} {bypass_list} {pac_url}";

                // have to get new settings
                _userSettings.UserSettingsRecorded = false;
            }

            //Save();
            ExecSysproxy(arguments);
        }


        public static void SetIEProxy(bool global, string strProxy, string strExceptions)
        {
            string arguments = global
                ? $"global {strProxy} {strExceptions}"
                : $"pac {strProxy}";

            ExecSysproxy(arguments);
        }

        // set system proxy to 1 (null) (null) (null)
        public static bool ResetIEProxy()
        {
            try
            {
                // clear user-wininet.json
                //_userSettings = new SysproxyConfig();
                //Save();
                // clear system setting
                ExecSysproxy("set 1 - - -");
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static void ExecSysproxy(string arguments)
        {
            // using event to avoid hanging when redirect standard output/error
            // ref: https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
            // and http://blog.csdn.net/zhangweixing0/article/details/7356841
            using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
            using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
            {
                using (Process process = new Process())
                {
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

                    StringBuilder output = new StringBuilder();
                    StringBuilder error = new StringBuilder();

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
        }


    }
}
