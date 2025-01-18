using CliWrap;
using CliWrap.Buffered;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ServiceLib.Common
{
    public class Utils
    {
        private static readonly string _tag = "Utils";

        #region 资源操作

        /// <summary>
        /// 获取嵌入文本资源
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static string GetEmbedText(string res)
        {
            var result = string.Empty;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(res);
                ArgumentNullException.ThrowIfNull(stream);
                using StreamReader reader = new(stream);
                result = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return result;
        }

        /// <summary>
        /// 取得存储资源
        /// </summary>
        /// <returns></returns>
        public static string? LoadResource(string? res)
        {
            try
            {
                if (File.Exists(res))
                {
                    return File.ReadAllText(res);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return null;
        }

        #endregion 资源操作

        #region 转换函数

        /// <summary>
        /// 转逗号分隔的字符串
        /// </summary>
        /// <param name="lst"></param>
        /// <param name="wrap"></param>
        /// <returns></returns>
        public static string List2String(List<string>? lst, bool wrap = false)
        {
            try
            {
                if (lst == null)
                {
                    return string.Empty;
                }
                if (wrap)
                {
                    return string.Join("," + Environment.NewLine, lst);
                }
                else
                {
                    return string.Join(",", lst);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return string.Empty;
        }

        /// <summary>
        /// 逗号分隔的字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static List<string>? String2List(string? str)
        {
            try
            {
                if (str == null)
                {
                    return null;
                }

                str = str.Replace(Environment.NewLine, "");
                return new List<string>(str.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return null;
        }

        /// <summary>
        /// 逗号分隔的字符串,先排序后转List
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static List<string>? String2ListSorted(string str)
        {
            try
            {
                str = str.Replace(Environment.NewLine, "");
                List<string> list = new(str.Split(',', StringSplitOptions.RemoveEmptyEntries));
                list.Sort();
                return list;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return null;
        }

        /// <summary>
        /// Base64编码
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            try
            {
                var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                return Convert.ToBase64String(plainTextBytes);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return string.Empty;
        }

        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Decode(string? plainText)
        {
            try
            {
                if (plainText.IsNullOrEmpty()) return "";
                plainText = plainText.Trim()
                    .Replace(Environment.NewLine, "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Replace('_', '/')
                    .Replace('-', '+')
                    .Replace(" ", "");

                if (plainText.Length % 4 > 0)
                {
                    plainText = plainText.PadRight(plainText.Length + 4 - (plainText.Length % 4), '=');
                }

                var data = Convert.FromBase64String(plainText);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return string.Empty;
        }

        public static int ToInt(object? obj)
        {
            try
            {
                return Convert.ToInt32(obj ?? string.Empty);
            }
            catch
            {
                return 0;
            }
        }

        public static bool ToBool(object obj)
        {
            try
            {
                return Convert.ToBoolean(obj);
            }
            catch
            {
                return false;
            }
        }

        public static string ToString(object? obj)
        {
            try
            {
                return obj?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void ToHumanReadable(long amount, out double result, out string unit)
        {
            var factor = 1024u;
            //long KBs = amount / factor;
            var KBs = amount;
            if (KBs > 0)
            {
                // multi KB
                var MBs = KBs / factor;
                if (MBs > 0)
                {
                    // multi MB
                    var GBs = MBs / factor;
                    if (GBs > 0)
                    {
                        // multi GB
                        var TBs = GBs / factor;
                        if (TBs > 0)
                        {
                            result = TBs + ((GBs % factor) / (factor + 0.0));
                            unit = "TB";
                            return;
                        }

                        result = GBs + ((MBs % factor) / (factor + 0.0));
                        unit = "GB";
                        return;
                    }

                    result = MBs + ((KBs % factor) / (factor + 0.0));
                    unit = "MB";
                    return;
                }

                result = KBs + ((amount % factor) / (factor + 0.0));
                unit = "KB";
                return;
            }
            else
            {
                result = amount;
                unit = "B";
            }
        }

        public static string HumanFy(long amount)
        {
            ToHumanReadable(amount, out var result, out var unit);
            return $"{result:f1} {unit}";
        }

        public static string UrlEncode(string url)
        {
            return Uri.EscapeDataString(url);
        }

        public static string UrlDecode(string url)
        {
            return Uri.UnescapeDataString(url);
        }

        public static NameValueCollection ParseQueryString(string query)
        {
            var result = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (IsNullOrEmpty(query))
            {
                return result;
            }

            var parts = query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(keyValue.First());
                var val = Uri.UnescapeDataString(keyValue.Last());

                if (result[key] is null)
                {
                    result.Add(key, val);
                }
            }

            return result;
        }

        public static string GetMd5(string str)
        {
            var byteOld = Encoding.UTF8.GetBytes(str);
            var byteNew = MD5.HashData(byteOld);
            StringBuilder sb = new(32);
            foreach (var b in byteNew)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// idn to idc
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetPunycode(string url)
        {
            if (Utils.IsNullOrEmpty(url))
            {
                return url;
            }

            try
            {
                Uri uri = new(url);
                if (uri.Host == uri.IdnHost || uri.Host == $"[{uri.IdnHost}]")
                {
                    return url;
                }
                else
                {
                    return url.Replace(uri.Host, uri.IdnHost);
                }
            }
            catch
            {
                return url;
            }
        }

        public static bool IsBase64String(string? plainText)
        {
            if (plainText.IsNullOrEmpty()) return false;
            var buffer = new Span<byte>(new byte[plainText.Length]);
            return Convert.TryFromBase64String(plainText, buffer, out var _);
        }

        public static string Convert2Comma(string text)
        {
            if (IsNullOrEmpty(text))
            {
                return text;
            }

            return text.Replace("，", ",").Replace(Environment.NewLine, ",");
        }

        #endregion 转换函数

        #region 数据检查

        /// <summary>
        /// 判断输入的是否是数字
        /// </summary>
        /// <param name="oText"></param>
        /// <returns></returns>
        public static bool IsNumeric(string oText)
        {
            return oText.All(char.IsNumber);
        }

        public static bool IsNullOrEmpty(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            return text == "null";
        }

        public static bool IsNotEmpty(string? text)
        {
            return !string.IsNullOrEmpty(text);
        }

        /// <summary>
        /// 验证Domain地址是否合法
        /// </summary>
        /// <param name="domain"></param>
        public static bool IsDomain(string? domain)
        {
            if (IsNullOrEmpty(domain))
            {
                return false;
            }

            return Uri.CheckHostName(domain) == UriHostNameType.Dns;
        }

        public static bool IsIpv6(string ip)
        {
            if (IPAddress.TryParse(ip, out var address))
            {
                return address.AddressFamily switch
                {
                    AddressFamily.InterNetwork => false,
                    AddressFamily.InterNetworkV6 => true,
                    _ => false,
                };
            }

            return false;
        }

        public static Uri? TryUri(string url)
        {
            try
            {
                return new Uri(url);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        public static bool IsPrivateNetwork(string ip)
        {
            if (IPAddress.TryParse(ip, out var address))
            {
                var ipBytes = address.GetAddressBytes();
                if (ipBytes[0] == 10) return true;
                if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) return true;
                if (ipBytes[0] == 192 && ipBytes[1] == 168) return true;
            }

            return false;
        }

        #endregion 数据检查

        #region 测速

        private static bool PortInUse(int port)
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var ipEndPoints = ipProperties.GetActiveTcpListeners();
                //var lstIpEndPoints = new List<IPEndPoint>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
                return ipEndPoints.Any(endPoint => endPoint.Port == port);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return false;
        }

        public static int GetFreePort(int defaultPort = 9090)
        {
            try
            {
                if (!Utils.PortInUse(defaultPort))
                {
                    return defaultPort;
                }

                TcpListener l = new(IPAddress.Loopback, 0);
                l.Start();
                var port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
            }

            return 59090;
        }

        #endregion 测速

        #region 杂项

        public static bool UpgradeAppExists(out string upgradeFileName)
        {
            upgradeFileName = Path.Combine(GetBaseDirectory(), GetExeName("AmazTool"));
            return File.Exists(upgradeFileName);
        }

        /// <summary>
        /// 取得版本
        /// </summary>
        /// <returns></returns>
        public static string GetVersion(bool blFull = true)
        {
            try
            {
                return blFull
                    ? $"{Global.AppName} - V{GetVersionInfo()} - {RuntimeInformation.ProcessArchitecture}"
                    : $"{Global.AppName}/{GetVersionInfo()}";
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return Global.AppName;
        }

        public static string GetVersionInfo()
        {
            try
            {
                return Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3) ?? "0.0";
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                return "0.0";
            }
        }

        public static string GetRuntimeInfo()
        {
            return $"{Utils.GetVersion()} | {Utils.StartupPath()} | {Utils.GetExePath()} | {Environment.OSVersion}";
        }

        /// <summary>
        /// 取得GUID
        /// </summary>
        /// <returns></returns>
        public static string GetGuid(bool full = true)
        {
            try
            {
                if (full)
                {
                    return Guid.NewGuid().ToString("D");
                }
                else
                {
                    return BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0).ToString();
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return string.Empty;
        }

        public static bool IsGuidByParse(string strSrc)
        {
            return Guid.TryParse(strSrc, out _);
        }

        public static Dictionary<string, string> GetSystemHosts()
        {
            var systemHosts = new Dictionary<string, string>();
            var hostFile = @"C:\Windows\System32\drivers\etc\hosts";
            try
            {
                if (File.Exists(hostFile))
                {
                    var hosts = File.ReadAllText(hostFile).Replace("\r", "");
                    var hostsList = hosts.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var host in hostsList)
                    {
                        if (host.StartsWith("#")) continue;
                        var hostItem = host.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (hostItem.Length != 2) continue;
                        systemHosts.Add(hostItem.Last(), hostItem.First());
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            return systemHosts;
        }

        public static async Task<string?> GetCliWrapOutput(string filePath, string? arg)
        {
            return await GetCliWrapOutput(filePath, arg != null ? new List<string>() { arg } : null);
        }

        public static async Task<string?> GetCliWrapOutput(string filePath, IEnumerable<string>? args)
        {
            try
            {
                var cmd = Cli.Wrap(filePath);
                if (args != null)
                {
                    if (args.Count() == 1)
                    {
                        cmd = cmd.WithArguments(args.First());
                    }
                    else
                    {
                        cmd = cmd.WithArguments(args);
                    }
                }

                var result = await cmd.ExecuteBufferedAsync();
                if (result.IsSuccess)
                {
                    return result.StandardOutput.ToString();
                }

                Logging.SaveLog(result.ToString() ?? "");
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GetCliWrapOutput", ex);
            }

            return null;
        }

        #endregion 杂项

        #region TempPath

        public static bool HasWritePermission()
        {
            try
            {
                //When this file exists, it is equivalent to having no permission to read and write
                if (File.Exists(Path.Combine(GetBaseDirectory(), "NotStoreConfigHere.txt")))
                {
                    return false;
                }

                var tempPath = Path.Combine(GetBaseDirectory(), "guiTemps");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }
                var fileName = Path.Combine(tempPath, GetGuid());
                File.Create(fileName).Close();
                File.Delete(fileName);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public static string GetPath(string fileName)
        {
            var startupPath = StartupPath();
            if (IsNullOrEmpty(fileName))
            {
                return startupPath;
            }

            return Path.Combine(startupPath, fileName);
        }

        public static string GetBaseDirectory(string fileName = "")
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        public static string GetExePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }

        public static string StartupPath()
        {
            if (Environment.GetEnvironmentVariable(Global.LocalAppData) == "1")
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "v2rayN");
            }

            return GetBaseDirectory();
        }

        public static string GetTempPath(string filename = "")
        {
            var tempPath = Path.Combine(StartupPath(), "guiTemps");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (IsNullOrEmpty(filename))
            {
                return tempPath;
            }
            else
            {
                return Path.Combine(tempPath, filename);
            }
        }

        public static string GetBackupPath(string filename)
        {
            var tempPath = Path.Combine(StartupPath(), "guiBackups");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            return Path.Combine(tempPath, filename);
        }

        public static string GetConfigPath(string filename = "")
        {
            var tempPath = Path.Combine(StartupPath(), "guiConfigs");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (Utils.IsNullOrEmpty(filename))
            {
                return tempPath;
            }
            else
            {
                return Path.Combine(tempPath, filename);
            }
        }

        public static string GetBinPath(string filename, string? coreType = null)
        {
            var tempPath = Path.Combine(StartupPath(), "bin");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (coreType != null)
            {
                tempPath = Path.Combine(tempPath, coreType.ToLower().ToString());
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }
            }

            if (IsNullOrEmpty(filename))
            {
                return tempPath;
            }
            else
            {
                return Path.Combine(tempPath, filename);
            }
        }

        public static string GetLogPath(string filename = "")
        {
            var tempPath = Path.Combine(StartupPath(), "guiLogs");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (Utils.IsNullOrEmpty(filename))
            {
                return tempPath;
            }
            else
            {
                return Path.Combine(tempPath, filename);
            }
        }

        public static string GetFontsPath(string filename = "")
        {
            var tempPath = Path.Combine(StartupPath(), "guiFonts");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (Utils.IsNullOrEmpty(filename))
            {
                return tempPath;
            }
            else
            {
                return Path.Combine(tempPath, filename);
            }
        }

        #endregion TempPath

        #region Platform

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static bool IsOSX() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsNonWindows() => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static string GetExeName(string name)
        {
            return IsWindows() ? $"{name}.exe" : name;
        }

        public static bool IsAdministrator()
        {
            if (IsWindows())
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            return false;
            //else
            //{
            //    var id = GetLinuxUserId().Result ?? "1000";
            //    if (int.TryParse(id, out var userId))
            //    {
            //        return userId == 0;
            //    }
            //    else
            //    {
            //        return false;
            //    }
            //}
        }

        private static async Task<string?> GetLinuxUserId()
        {
            var arg = new List<string>() { "-c", "id -u" };
            return await GetCliWrapOutput("/bin/bash", arg);
        }

        public static async Task<string?> SetLinuxChmod(string? fileName)
        {
            if (fileName.IsNullOrEmpty()) return null;
            if (fileName.Contains(' ')) fileName = fileName.AppendQuotes();
            //File.SetUnixFileMode(fileName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var arg = new List<string>() { "-c", $"chmod +x {fileName}" };
            return await GetCliWrapOutput("/bin/bash", arg);
        }

        public static async Task<string?> GetLinuxFontFamily(string lang)
        {
            // var arg = new List<string>() { "-c", $"fc-list :lang={lang} family" };
            var arg = new List<string>() { "-c", $"fc-list : family" };
            return await GetCliWrapOutput("/bin/bash", arg);
        }

        public static string? GetHomePath()
        {
            return IsWindows()
                ? Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")
                : Environment.GetEnvironmentVariable("HOME");
        }

        public static async Task<string?> GetListNetworkServices()
        {
            var arg = new List<string>() { "-c", $"networksetup -listallnetworkservices" };
            return await GetCliWrapOutput("/bin/bash", arg);
        }

        #endregion Platform
    }
}