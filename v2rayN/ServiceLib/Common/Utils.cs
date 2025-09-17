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
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

namespace ServiceLib.Common;

public class Utils
{
    private static readonly string _tag = "Utils";

    #region 转换函数

    /// <summary>
    /// Convert to comma-separated string
    /// </summary>
    /// <param name="lst"></param>
    /// <param name="wrap"></param>
    /// <returns></returns>
    public static string List2String(List<string>? lst, bool wrap = false)
    {
        if (lst == null || lst.Count == 0)
        {
            return string.Empty;
        }

        var separator = wrap ? "," + Environment.NewLine : ",";

        try
        {
            return string.Join(separator, lst);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Comma-separated string
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static List<string>? String2List(string? str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return null;
        }

        try
        {
            str = str.Replace(Environment.NewLine, string.Empty);
            return new List<string>(str.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    /// <summary>
    /// Comma-separated string, sorted and then converted to List
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static List<string>? String2ListSorted(string str)
    {
        var lst = String2List(str);
        lst?.Sort();
        return lst;
    }

    /// <summary>
    /// Base64 Encode
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
    /// Base64 Decode
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    public static string Base64Decode(string? plainText)
    {
        try
        {
            if (plainText.IsNullOrEmpty())
            {
                return "";
            }

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

    public static string HumanFy(long amount)
    {
        if (amount <= 0)
        {
            return $"{amount:f1} B";
        }

        string[] units = ["KB", "MB", "GB", "TB", "PB"];
        var unitIndex = 0;
        double size = amount;

        // Loop and divide by 1024 until a suitable unit is found
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:f1} {units[unitIndex]}";
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
        if (query.IsNullOrEmpty())
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
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        try
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
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return string.Empty;
        }
    }

    public static string GetFileHash(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// idn to idc
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static string GetPunycode(string url)
    {
        if (url.IsNullOrEmpty())
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
        if (plainText.IsNullOrEmpty())
            return false;
        var buffer = new Span<byte>(new byte[plainText.Length]);
        return Convert.TryFromBase64String(plainText, buffer, out var _);
    }

    public static string Convert2Comma(string text)
    {
        if (text.IsNullOrEmpty())
        {
            return text;
        }

        return text.Replace("，", ",").Replace(Environment.NewLine, ",");
    }

    public static List<string> GetEnumNames<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<TEnum>()
            .Select(e => e.ToString())
            .ToList();
    }

    public static Dictionary<string, List<string>> ParseHostsToDictionary(string hostsContent)
    {
        var userHostsMap = hostsContent
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            // skip full-line comments
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            // strip inline comments (truncate at '#')
            .Select(line =>
            {
                var index = line.IndexOf('#');
                return index >= 0 ? line.Substring(0, index).Trim() : line;
            })
            // ensure line still contains valid parts
            .Where(line => !string.IsNullOrWhiteSpace(line) && line.Contains(' '))
            .Select(line => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .GroupBy(parts => parts[0])
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(parts => parts.Skip(1)).ToList()
            );

        return userHostsMap;
    }

    public static List<string> ParseCertSha256ToList(string certSha256Content)
    {
        return String2List(certSha256Content)
                .Select(s => s.Replace(":", "").Replace(" ", ""))
                .Where(s => s.Length == 64 && Regex.IsMatch(s, @"\A\b[0-9a-fA-F]+\b\Z"))
                .ToList();
    }

    #endregion 转换函数

    #region 数据检查

    /// <summary>
    /// Determine if the input is a number
    /// </summary>
    /// <param name="oText"></param>
    /// <returns></returns>
    public static bool IsNumeric(string oText)
    {
        return oText.All(char.IsNumber);
    }

    /// <summary>
    /// Validate if the domain address is valid
    /// </summary>
    /// <param name="domain"></param>
    public static bool IsDomain(string? domain)
    {
        if (domain.IsNullOrEmpty())
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
            // Loopback address check (127.0.0.1 for IPv4, ::1 for IPv6)
            if (IPAddress.IsLoopback(address))
                return true;

            var ipBytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                // IPv4 private address check
                if (ipBytes[0] == 10)
                    return true;
                if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31)
                    return true;
                if (ipBytes[0] == 192 && ipBytes[1] == 168)
                    return true;
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv6 private address check
                // Link-local address fe80::/10
                if (ipBytes[0] == 0xfe && (ipBytes[1] & 0xc0) == 0x80)
                    return true;

                // Unique local address fc00::/7 (typically fd00::/8)
                if ((ipBytes[0] & 0xfe) == 0xfc)
                    return true;

                // Private portion in IPv4-mapped addresses ::ffff:0:0/96
                if (address.IsIPv4MappedToIPv6)
                {
                    var ipv4Bytes = ipBytes.Skip(12).ToArray();
                    if (ipv4Bytes[0] == 10)
                        return true;
                    if (ipv4Bytes[0] == 172 && ipv4Bytes[1] >= 16 && ipv4Bytes[1] <= 31)
                        return true;
                    if (ipv4Bytes[0] == 192 && ipv4Bytes[1] == 168)
                        return true;
                }
            }
        }

        return false;
    }

    #endregion 数据检查

    #region 测速

    private static bool PortInUse(int port)
    {
        try
        {
            List<IPEndPoint> lstIpEndPoints = new();
            List<TcpConnectionInformation> lstTcpConns = new();

            lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
            lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners());
            lstTcpConns.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());

            if (lstIpEndPoints?.FindIndex(it => it.Port == port) >= 0)
            {
                return true;
            }

            if (lstTcpConns?.FindIndex(it => it.LocalEndPoint.Port == port) >= 0)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return false;
    }

    public static int GetFreePort(int defaultPort = 0)
    {
        try
        {
            if (!(defaultPort == 0 || Utils.PortInUse(defaultPort)))
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
    /// Get version
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
    /// GUID
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
                    if (host.StartsWith("#"))
                        continue;
                    var hostItem = host.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hostItem.Length < 2)
                        continue;
                    systemHosts.Add(hostItem[1], hostItem[0]);
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
                return result.StandardOutput ?? "";
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
            var basePath = GetBaseDirectory();
            //When this file exists, it is equivalent to having no permission to read and write
            if (File.Exists(Path.Combine(basePath, "NotStoreConfigHere.txt")))
            {
                return false;
            }

            //Check if it is installed by Windows WinGet
            if (IsWindows() && basePath.Contains("Users") && basePath.Contains("WinGet"))
            {
                return false;
            }

            var tempPath = Path.Combine(basePath, "guiTemps");
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
        if (fileName.IsNullOrEmpty())
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

        if (filename.IsNullOrEmpty())
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

        if (filename.IsNullOrEmpty())
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

        if (filename.IsNullOrEmpty())
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

        if (filename.IsNullOrEmpty())
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

        if (filename.IsNullOrEmpty())
        {
            return tempPath;
        }
        else
        {
            return Path.Combine(tempPath, filename);
        }
    }

    public static string GetBinConfigPath(string filename = "")
    {
        var tempPath = Path.Combine(StartupPath(), "binConfigs");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        if (filename.IsNullOrEmpty())
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
    }

    public static bool IsPackagedInstall()
    {
        try
        {
            if (IsWindows() || IsOSX())
            {
                return false;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")))
            {
                return true;
            }

            var exePath = GetExePath();
            var baseDir = string.IsNullOrEmpty(exePath) ? StartupPath() : Path.GetDirectoryName(exePath) ?? "";
            var p = baseDir.Replace('\\', '/');

            if (string.IsNullOrEmpty(p))
            {
                return false;
            }

            if (p.Contains("/.mount_", StringComparison.Ordinal))
            {
                return true;
            }

            if (p.StartsWith("/opt/v2rayN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (p.StartsWith("/usr/lib/v2rayN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (p.StartsWith("/usr/share/v2rayN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static async Task<string?> GetLinuxUserId()
    {
        var arg = new List<string>() { "-c", "id -u" };
        return await GetCliWrapOutput(Global.LinuxBash, arg);
    }

    public static async Task<string?> SetLinuxChmod(string? fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return null;
        }
        if (SetUnixFileMode(fileName))
        {
            Logging.SaveLog($"Successfully set the file execution permission, {fileName}");
            return "";
        }

        if (fileName.Contains(' '))
        {
            fileName = fileName.AppendQuotes();
        }
        var arg = new List<string>() { "-c", $"chmod +x {fileName}" };
        return await GetCliWrapOutput(Global.LinuxBash, arg);
    }

    public static bool SetUnixFileMode(string? fileName)
    {
        try
        {
            if (fileName.IsNullOrEmpty())
            {
                return false;
            }

            if (File.Exists(fileName))
            {
                var currentMode = File.GetUnixFileMode(fileName);
                File.SetUnixFileMode(fileName, currentMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SetUnixFileMode", ex);
        }
        return false;
    }

    public static async Task<string?> GetLinuxFontFamily(string lang)
    {
        // var arg = new List<string>() { "-c", $"fc-list :lang={lang} family" };
        var arg = new List<string>() { "-c", $"fc-list : family" };
        return await GetCliWrapOutput(Global.LinuxBash, arg);
    }

    public static string? GetHomePath()
    {
        return IsWindows()
            ? Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")
            : Environment.GetEnvironmentVariable("HOME");
    }

    #endregion Platform
}
