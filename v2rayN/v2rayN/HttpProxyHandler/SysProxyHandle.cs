using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using v2rayN.Mode;
using v2rayN.Properties;
using v2rayN.Tool;

namespace v2rayN.HttpProxyHandler
{
    class SysProxyHandle
    {
        private const string _userWininetConfigFile = "user-wininet.json";

        private static string _queryStr;

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

        public static void SetIEProxy(bool enable, bool global, string proxyServer, string pacURL)
        {
            //Read();

            //if (!_userSettings.UserSettingsRecorded)
            //{
            //    // record user settings
            //    ExecSysproxy("query");
            //    ParseQueryStr(_queryStr);
            //}

            string arguments;
            if (enable)
            {
                arguments = global
                    ? string.Format(
                        //"global {0} <local>;localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*;192.168.*",
                        "global {0} <local>;localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;172.32.*",
                        proxyServer)
                    : string.Format("pac {0}", pacURL);
            }
            else
            {
                // restore user settings
                //var flags = _userSettings.Flags;
                //var proxy_server = _userSettings.ProxyServer ?? "-";
                //var bypass_list = _userSettings.BypassList ?? "-";
                //var pac_url = _userSettings.PacUrl ?? "-";
                ////arguments = string.Format("set {0} {1} {2} {3}", flags, proxy_server, bypass_list, pac_url);
                //set null settings
                arguments = string.Format("set {0} {1} {2} {3}", 1, "-", "<local>", @"http://127.0.0.1");

                // have to get new settings
                //_userSettings.UserSettingsRecorded = false;
            }

            //Save();
            ExecSysproxy(arguments);
        }

        private static void ExecSysproxy(string arguments)
        {
            using (var process = new Process())
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
                process.Start();

                var stderr = process.StandardError.ReadToEnd();
                var stdout = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                var exitCode = process.ExitCode;
                if (exitCode != (int)RET_ERRORS.RET_NO_ERROR)
                {
                    throw new Exception(stderr);
                }

                if (arguments == "query")
                {
                    if (stdout.IsNullOrWhiteSpace() || stdout.IsNullOrEmpty())
                    {
                        // we cannot get user settings
                        throw new Exception("failed to query wininet settings");
                    }
                    _queryStr = stdout;
                }
            }
        }

        private static void Save()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(File.Open(Utils.GetPath(_userWininetConfigFile), FileMode.Create)))
                {
                    string jsonString = JsonConvert.SerializeObject(_userSettings, Formatting.Indented);
                    sw.Write(jsonString);
                    sw.Flush();
                }
            }
            catch (IOException ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private static void Read()
        {
            try
            {
                string configContent = File.ReadAllText(Utils.GetPath(_userWininetConfigFile));
                _userSettings = JsonConvert.DeserializeObject<SysproxyConfig>(configContent);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                // Suppress all exceptions. finally block will initialize new user config settings.
            }
            finally
            {
                if (_userSettings == null) _userSettings = new SysproxyConfig();
            }
        }

        private static void ParseQueryStr(string str)
        {
            string[] userSettingsArr = str.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            _userSettings.Flags = userSettingsArr[0];

            // handle output from WinINET
            if (userSettingsArr[1] == "(null)") _userSettings.ProxyServer = null;
            else _userSettings.ProxyServer = userSettingsArr[1];
            if (userSettingsArr[2] == "(null)") _userSettings.BypassList = null;
            else _userSettings.BypassList = userSettingsArr[2];
            if (userSettingsArr[3] == "(null)") _userSettings.PacUrl = null;
            else _userSettings.PacUrl = userSettingsArr[3];

            _userSettings.UserSettingsRecorded = true;
        }
    }
}
