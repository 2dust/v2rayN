using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace v2rayN
{
    internal class Utils
    {
        #region 资源Json操作

        /// <summary>
        /// 获取嵌入文本资源
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static string GetEmbedText(string res)
        {
            string result = string.Empty;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream(res);
                ArgumentNullException.ThrowIfNull(stream);
                using StreamReader reader = new(stream);
                result = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return result;
        }

        /// <summary>
        /// 取得存储资源
        /// </summary>
        /// <returns></returns>
        public static string? LoadResource(string res)
        {
            try
            {
                if (!File.Exists(res)) return null;
                return File.ReadAllText(res);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return null;
        }

        #endregion 资源Json操作

        #region 转换函数

        /// <summary>
        /// List<string>转逗号分隔的字符串
        /// </summary>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static string List2String(List<string> lst, bool wrap = false)
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
                Logging.SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 逗号分隔的字符串,转List<string>
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static List<string> String2List(string str)
        {
            try
            {
                str = str.Replace(Environment.NewLine, "");
                return new List<string>(str.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// 逗号分隔的字符串,先排序后转List<string>
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static List<string> String2ListSorted(string str)
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
                Logging.SaveLog(ex.Message, ex);
                return new List<string>();
            }
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
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                return Convert.ToBase64String(plainTextBytes);
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Base64Encode", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Decode(string plainText)
        {
            try
            {
                plainText = plainText.Trim()
                  .Replace(Environment.NewLine, "")
                  .Replace("\n", "")
                  .Replace("\r", "")
                  .Replace('_', '/')
                  .Replace('-', '+')
                  .Replace(" ", "");

                if (plainText.Length % 4 > 0)
                {
                    plainText = plainText.PadRight(plainText.Length + 4 - plainText.Length % 4, '=');
                }

                byte[] data = Convert.FromBase64String(plainText);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Base64Decode", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 转Int
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int ToInt(object? obj)
        {
            try
            {
                return Convert.ToInt32(obj ?? string.Empty);
            }
            catch //(Exception ex)
            {
                //SaveLog(ex.Message, ex);
                return 0;
            }
        }

        public static bool ToBool(object obj)
        {
            try
            {
                return Convert.ToBoolean(obj);
            }
            catch //(Exception ex)
            {
                //SaveLog(ex.Message, ex);
                return false;
            }
        }

        public static string ToString(object obj)
        {
            try
            {
                return obj?.ToString() ?? string.Empty;
            }
            catch// (Exception ex)
            {
                //SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// byte 转成 有两位小数点的 方便阅读的数据
        ///     比如 2.50 MB
        /// </summary>
        /// <param name="amount">bytes</param>
        /// <param name="result">转换之后的数据</param>
        /// <param name="unit">单位</param>
        public static void ToHumanReadable(long amount, out double result, out string unit)
        {
            uint factor = 1024u;
            //long KBs = amount / factor;
            long KBs = amount;
            if (KBs > 0)
            {
                // multi KB
                long MBs = KBs / factor;
                if (MBs > 0)
                {
                    // multi MB
                    long GBs = MBs / factor;
                    if (GBs > 0)
                    {
                        // multi GB
                        long TBs = GBs / factor;
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
            ToHumanReadable(amount, out double result, out string unit);
            return $"{string.Format("{0:f1}", result)} {unit}";
        }

        public static string UrlEncode(string url)
        {
            return Uri.EscapeDataString(url);
            //return  HttpUtility.UrlEncode(url);
        }

        public static string UrlDecode(string url)
        {
            return Uri.UnescapeDataString(url);
            //return HttpUtility.UrlDecode(url);
        }

        public static NameValueCollection ParseQueryString(string query)
        {
            var result = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (IsNullOrEmpty(query))
            {
                return result;
            }

            var parts = query[1..].Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split(['=']);
                if (keyValue.Length != 2)
                {
                    continue;
                }
                var key = Uri.UnescapeDataString(keyValue[0]);
                var val = Uri.UnescapeDataString(keyValue[1]);

                result.Add(key, val);
            }

            return result;
        }

        public static string GetMD5(string str)
        {
            byte[] byteOld = Encoding.UTF8.GetBytes(str);
            byte[] byteNew = MD5.HashData(byteOld);
            StringBuilder sb = new(32);
            foreach (byte b in byteNew)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static ImageSource IconToImageSource(Icon icon)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                new System.Windows.Int32Rect(0, 0, icon.Width, icon.Height),
                BitmapSizeOptions.FromEmptyOptions());
        }

        /// <summary>
        /// idn to idc
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetPunycode(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
            try
            {
                Uri uri = new(url);
                if (uri.Host == uri.IdnHost)
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

        public static bool IsBase64String(string plainText)
        {
            var buffer = new Span<byte>(new byte[plainText.Length]);
            return Convert.TryFromBase64String(plainText, buffer, out int _);
        }

        public static string Convert2Comma(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
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
        public static bool IsNumberic(string oText)
        {
            try
            {
                int var1 = ToInt(oText);
                return true;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 文本
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            if (text == "null")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 验证IP地址是否合法
        /// </summary>
        /// <param name="ip"></param>
        public static bool IsIP(string ip)
        {
            //如果为空
            if (IsNullOrEmpty(ip))
            {
                return false;
            }

            //清除要验证字符串中的空格
            //ip = ip.TrimEx();
            //可能是CIDR
            if (ip.IndexOf(@"/") > 0)
            {
                string[] cidr = ip.Split('/');
                if (cidr.Length == 2)
                {
                    if (!IsNumberic(cidr[0]))
                    {
                        return false;
                    }
                    ip = cidr[0];
                }
            }

            //模式字符串
            string pattern = @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$";

            //验证
            return IsMatch(ip, pattern);
        }

        /// <summary>
        /// 验证Domain地址是否合法
        /// </summary>
        /// <param name="domain"></param>
        public static bool IsDomain(string domain)
        {
            //如果为空
            if (IsNullOrEmpty(domain))
            {
                return false;
            }

            return Uri.CheckHostName(domain) == UriHostNameType.Dns;
        }

        /// <summary>
        /// 验证输入字符串是否与模式字符串匹配，匹配返回true
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="pattern">模式字符串</param>
        public static bool IsMatch(string input, string pattern)
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsIpv6(string ip)
        {
            if (IPAddress.TryParse(ip, out IPAddress? address))
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

        #endregion 数据检查

        #region 测速

        /// <summary>
        /// 取得本机 IP Address
        /// </summary>
        /// <returns></returns>
        //public static List<string> GetHostIPAddress()
        //{
        //    List<string> lstIPAddress = new List<string>();
        //    try
        //    {
        //        IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
        //        foreach (IPAddress ipa in IpEntry.AddressList)
        //        {
        //            if (ipa.AddressFamily == AddressFamily.InterNetwork)
        //                lstIPAddress.Add(ipa.ToString());
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        SaveLog(ex.Message, ex);
        //    }
        //    return lstIPAddress;
        //}

        public static void SetSecurityProtocol(bool enableSecurityProtocolTls13)
        {
            if (enableSecurityProtocolTls13)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            }
            else
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            ServicePointManager.DefaultConnectionLimit = 256;
        }

        public static bool PortInUse(int port)
        {
            bool inUse = false;
            try
            {
                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

                var lstIpEndPoints = new List<IPEndPoint>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());

                foreach (IPEndPoint endPoint in ipEndPoints)
                {
                    if (endPoint.Port == port)
                    {
                        inUse = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return inUse;
        }

        public static int GetFreePort()
        {
            try
            {
                int defaultPort = 9090;
                if (!Utils.PortInUse(defaultPort))
                {
                    return defaultPort;
                }

                TcpListener l = new(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
            }
            return 69090;
        }

        #endregion 测速

        #region 杂项

        /// <summary>
        /// 取得版本
        /// </summary>
        /// <returns></returns>
        public static string GetVersion(bool blFull = true)
        {
            try
            {
                string location = GetExePath();
                if (blFull)
                {
                    return string.Format("v2rayN - V{0} - {1}",
                            FileVersionInfo.GetVersionInfo(location).FileVersion?.ToString(),
                            File.GetLastWriteTime(location).ToString("yyyy/MM/dd"));
                }
                else
                {
                    return string.Format("v2rayN/{0}",
                        FileVersionInfo.GetVersionInfo(location).FileVersion?.ToString());
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取剪贴板数
        /// </summary>
        /// <returns></returns>
        public static string? GetClipboardData()
        {
            string? strData = string.Empty;
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                if (data.GetDataPresent(DataFormats.UnicodeText))
                {
                    strData = data.GetData(DataFormats.UnicodeText)?.ToString();
                }
                return strData;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return strData;
        }

        /// <summary>
        /// 拷贝至剪贴板
        /// </summary>
        /// <returns></returns>
        public static void SetClipboardData(string strData)
        {
            try
            {
                Clipboard.SetText(strData);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 取得GUID
        /// </summary>
        /// <returns></returns>
        public static string GetGUID(bool full = true)
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
                Logging.SaveLog(ex.Message, ex);
            }
            return string.Empty;
        }

        /// <summary>
        /// IsAdministrator
        /// </summary>
        /// <returns></returns>
        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
                //WindowsBuiltInRole可以枚举出很多权限，例如系统用户、User、Guest等等
                return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return false;
            }
        }

        public static string GetDownloadFileName(string url)
        {
            var fileName = Path.GetFileName(url);
            fileName += "_temp";

            return fileName;
        }

        public static IPAddress? GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .Where(a => a != null)
                // .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                // .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                .FirstOrDefault();
        }

        public static bool IsGuidByParse(string strSrc)
        {
            return Guid.TryParse(strSrc, out Guid g);
        }

        public static void ProcessStart(string fileName)
        {
            try
            {
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public static void SetDarkBorder(System.Windows.Window window, bool dark)
        {
            // Make sure the handle is created before the window is shown
            IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            int attribute = dark ? 1 : 0;
            uint attributeSize = (uint)Marshal.SizeOf(attribute);
            DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
            DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
        }

        public static bool IsLightTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }

        /// <summary>
        /// 获取系统hosts
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetSystemHosts()
        {
            var systemHosts = new Dictionary<string, string>();
            var hostfile = @"C:\Windows\System32\drivers\etc\hosts";
            try
            {
                if (File.Exists(hostfile))
                {
                    var hosts = File.ReadAllText(hostfile).Replace("\r", "");
                    var hostsList = hosts.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var host in hostsList)
                    {
                        if (host.StartsWith("#")) continue;
                        var hostItem = host.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (hostItem.Length < 2) continue;
                        systemHosts.Add(hostItem[1], hostItem[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return systemHosts;
        }

        #endregion 杂项

        #region TempPath

        /// <summary>
        /// 获取启动了应用程序的可执行文件的路径
        /// </summary>
        /// <returns></returns>
        public static string GetPath(string fileName)
        {
            string startupPath = StartupPath();
            if (IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }

        /// <summary>
        /// 获取启动了应用程序的可执行文件的路径及文件名
        /// </summary>
        /// <returns></returns>
        public static string GetExePath()
        {
            return Environment.ProcessPath ?? string.Empty;
        }

        public static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetTempPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiTemps");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string UnGzip(byte[] buf)
        {
            using MemoryStream sb = new();
            using GZipStream input = new(new MemoryStream(buf), CompressionMode.Decompress, false);
            input.CopyTo(sb);
            sb.Position = 0;
            return new StreamReader(sb, Encoding.UTF8).ReadToEnd();
        }

        public static string GetBackupPath(string filename)
        {
            string _tempPath = Path.Combine(StartupPath(), "guiBackups");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            return Path.Combine(_tempPath, filename);
        }

        public static string GetConfigPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiConfigs");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetBinPath(string filename, string? coreType = null)
        {
            string _tempPath = Path.Combine(StartupPath(), "bin");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (coreType != null)
            {
                _tempPath = Path.Combine(_tempPath, coreType.ToString()!);
                if (!Directory.Exists(_tempPath))
                {
                    Directory.CreateDirectory(_tempPath);
                }
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetLogPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiLogs");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetFontsPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiFonts");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        #endregion TempPath

        #region scan screen

        public static string ScanScreen(float dpiX, float dpiY)
        {
            try
            {
                var left = (int)(SystemParameters.WorkArea.Left);
                var top = (int)(SystemParameters.WorkArea.Top);
                var width = (int)(SystemParameters.WorkArea.Width / dpiX);
                var height = (int)(SystemParameters.WorkArea.Height / dpiY);

                using Bitmap fullImage = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(fullImage))
                {
                    g.CopyFromScreen(left, top, 0, 0, fullImage.Size, CopyPixelOperation.SourceCopy);
                }
                int maxTry = 10;
                for (int i = 0; i < maxTry; i++)
                {
                    int marginLeft = (int)((double)fullImage.Width * i / 2.5 / maxTry);
                    int marginTop = (int)((double)fullImage.Height * i / 2.5 / maxTry);
                    Rectangle cropRect = new(marginLeft, marginTop, fullImage.Width - marginLeft * 2, fullImage.Height - marginTop * 2);
                    Bitmap target = new(width, height);

                    double imageScale = (double)width / (double)cropRect.Width;
                    using (Graphics g = Graphics.FromImage(target))
                    {
                        g.DrawImage(fullImage, new Rectangle(0, 0, target.Width, target.Height),
                                        cropRect,
                                        GraphicsUnit.Pixel);
                    }

                    BitmapLuminanceSource source = new(target);
                    BinaryBitmap bitmap = new(new HybridBinarizer(source));
                    QRCodeReader reader = new();
                    Result result = reader.decode(bitmap);
                    if (result != null)
                    {
                        string ret = result.Text;
                        return ret;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return string.Empty;
        }

        public static Tuple<float, float> GetDpiXY(Window window)
        {
            IntPtr hWnd = new WindowInteropHelper(window).EnsureHandle();
            Graphics g = Graphics.FromHwnd(hWnd);

            return new(96 / g.DpiX, 96 / g.DpiY);
        }

        #endregion scan screen

        #region 开机自动启动等

        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(string AutoRunRegPath, string AutoRunName, bool run)
        {
            try
            {
                var autoRunName = $"{AutoRunName}_{GetMD5(StartupPath())}";

                //delete first
                RegWriteValue(AutoRunRegPath, autoRunName, "");
                if (IsAdministrator())
                {
                    AutoStart(autoRunName, "", "");
                }

                if (run)
                {
                    string exePath = $"\"{GetExePath()}\"";
                    if (IsAdministrator())
                    {
                        AutoStart(autoRunName, exePath, "");
                    }
                    else
                    {
                        RegWriteValue(AutoRunRegPath, autoRunName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public static string? RegReadValue(string path, string name, string def)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string? value = regKey?.GetValue(name) as string;
                if (IsNullOrEmpty(value))
                {
                    return def;
                }
                else
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
            return def;
        }

        public static void RegWriteValue(string path, string name, object value)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.CreateSubKey(path);
                if (IsNullOrEmpty(value.ToString()))
                {
                    regKey?.DeleteValue(name, false);
                }
                else
                {
                    regKey?.SetValue(name, value);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
        }

        /// <summary>
        /// Auto Start via TaskService
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="fileName"></param>
        /// <param name="description"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AutoStart(string taskName, string fileName, string description)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return;
            }
            string TaskName = taskName;
            var logonUser = WindowsIdentity.GetCurrent().Name;
            string taskDescription = description;
            string deamonFileName = fileName;

            using var taskService = new TaskService();
            var tasks = taskService.RootFolder.GetTasks(new Regex(TaskName));
            foreach (var t in tasks)
            {
                taskService.RootFolder.DeleteTask(t.Name);
            }
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var task = taskService.NewTask();
            task.RegistrationInfo.Description = taskDescription;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.RunOnlyIfIdle = false;
            task.Settings.IdleSettings.StopOnIdleEnd = false;
            task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            task.Triggers.Add(new LogonTrigger { UserId = logonUser, Delay = TimeSpan.FromSeconds(10) });
            task.Principal.RunLevel = TaskRunLevel.Highest;
            task.Actions.Add(new ExecAction(deamonFileName));

            taskService.RootFolder.RegisterTaskDefinition(TaskName, task);
        }

        public static void RemoveTunDevice()
        {
            try
            {
                string pnputilPath = @"C:\Windows\System32\pnputil.exe";
                string arg = $" /remove-device /deviceid \"wintun\"";

                // Try to remove the device
                Process proc = new()
                {
                    StartInfo = new()
                    {
                        FileName = pnputilPath,
                        Arguments = arg,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                proc.Start();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
            }
            catch
            {
            }
        }

        #endregion 开机自动启动等

        #region Windows API

        [Flags]
        public enum DWMWINDOWATTRIBUTE : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int attributeValue, uint attributeSize);

        #endregion Windows API
    }
}