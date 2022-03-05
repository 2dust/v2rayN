using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using System.Security.Principal;
using v2rayN.Base;
using Newtonsoft.Json.Linq;
using System.Web;
using log4net;

namespace v2rayN
{
    class Utils
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
                using (Stream stream = assembly.GetManifestResourceStream(res))
                using (StreamReader reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return result;
        }


        /// <summary>
        /// 取得存储资源
        /// </summary>
        /// <returns></returns>
        public static string LoadResource(string res)
        {
            string result = string.Empty;

            try
            {
                using (StreamReader reader = new StreamReader(res))
                {
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return result;
        }

        /// <summary>
        /// 反序列化成对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strJson"></param>
        /// <returns></returns>
        public static T FromJson<T>(string strJson)
        {
            try
            {
                T obj = JsonConvert.DeserializeObject<T>(strJson);
                return obj;
            }
            catch
            {
                return JsonConvert.DeserializeObject<T>("");
            }
        }

        /// <summary>
        /// 序列化成Json
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ToJson(Object obj)
        {
            string result = string.Empty;
            try
            {
                result = JsonConvert.SerializeObject(obj,
                                           Formatting.Indented,
                                           new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return result;
        }

        /// <summary>
        /// 保存成json文件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static int ToJsonFile(Object obj, string filePath, bool nullValue = true)
        {
            int result;
            try
            {
                using (StreamWriter file = File.CreateText(filePath))
                {
                    JsonSerializer serializer;
                    if (nullValue)
                    {
                        serializer = new JsonSerializer() { Formatting = Formatting.Indented };
                    }
                    else
                    {
                        serializer = new JsonSerializer() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                    }

                    serializer.Serialize(file, obj);
                }
                result = 0;
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
                result = -1;
            }
            return result;
        }

        public static JObject ParseJson(string strJson)
        {
            try
            {
                JObject obj = JObject.Parse(strJson);
                return obj;
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);

                return null;
            }
        }
        #endregion

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
                    return string.Join("," + Environment.NewLine, lst.ToArray());
                }
                else
                {
                    return string.Join(",", lst.ToArray());
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
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
                return new List<string>(str.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
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
                SaveLog("Base64Encode", ex);
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
                plainText = plainText.TrimEx()
                  .Replace(Environment.NewLine, "")
                  .Replace("\n", "")
                  .Replace("\r", "")
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
                SaveLog("Base64Decode", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 转Int
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int ToInt(object obj)
        {
            try
            {
                return Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
                return 0;
            }
        }

        public static string ToString(object obj)
        {
            try
            {
                return (obj == null ? string.Empty : obj.ToString());
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
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
        public static void ToHumanReadable(ulong amount, out double result, out string unit)
        {
            uint factor = 1024u;
            ulong KBs = amount / factor;
            if (KBs > 0)
            {
                // multi KB
                ulong MBs = KBs / factor;
                if (MBs > 0)
                {
                    // multi MB
                    ulong GBs = MBs / factor;
                    if (GBs > 0)
                    {
                        // multi GB
                        /*ulong TBs = GBs / factor;
                        if (TBs > 0)
                        {
                            // 你是魔鬼吗？ 用这么多流量
                            result = TBs + GBs % factor / (factor + 0.0);
                            unit = "TB";
                            return;
                        }*/
                        result = GBs + MBs % factor / (factor + 0.0);
                        unit = "GB";
                        return;
                    }
                    result = MBs + KBs % factor / (factor + 0.0);
                    unit = "MB";
                    return;
                }
                result = KBs + amount % factor / (factor + 0.0);
                unit = "KB";
                return;
            }
            else
            {
                result = amount;
                unit = "B";
            }
        }

        public static string HumanFy(ulong amount)
        {
            ToHumanReadable(amount, out double result, out string unit);
            return $"{string.Format("{0:f1}", result)} {unit}";
        }



        public static string UrlEncode(string url)
        {
            return HttpUtility.UrlEncode(url);
        }
        public static string UrlDecode(string url)
        {
            return HttpUtility.UrlDecode(url);
        }
        #endregion


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
                SaveLog(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 文本
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            if (text.Equals("null"))
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

            //清除要验证字符串中的空格
            //domain = domain.TrimEx();

            //模式字符串
            string pattern = @"^(?=^.{3,255}$)[a-zA-Z0-9][-a-zA-Z0-9]{0,62}(\.[a-zA-Z0-9][-a-zA-Z0-9]{0,62})+$";

            //验证
            return IsMatch(domain, pattern);
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
            IPAddress address;
            if (IPAddress.TryParse(ip, out address))
            {
                switch (address.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        return false;
                    case AddressFamily.InterNetworkV6:
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }

        #endregion

        #region 开机自动启动

        private static string autoRunName = "v2rayNAutoRun";
        private static string autoRunRegPath
        {
            get
            {
                return @"Software\Microsoft\Windows\CurrentVersion\Run";
                //if (Environment.Is64BitProcess)
                //{
                //    return @"Software\Microsoft\Windows\CurrentVersion\Run";
                //}
                //else
                //{
                //    return @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
                //}
            }
        }

        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(bool run)
        {
            try
            {
                string exePath = GetExePath();
                RegWriteValue(autoRunRegPath, autoRunName, run ? $"\"{exePath}\"" : "");
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
        }

        /// <summary>
        /// 是否已经设置开机自动启动
        /// </summary>
        /// <returns></returns>
        public static bool IsAutoRun()
        {
            try
            {
                string value = RegReadValue(autoRunRegPath, autoRunName, "");
                string exePath = GetExePath();
                if (value?.Equals(exePath) == true || value?.Equals($"\"{exePath}\"") == true)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return false;
        }

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
            return Application.ExecutablePath;
        }

        public static string StartupPath()
        {
            return Application.StartupPath;
        }

        public static string RegReadValue(string path, string name, string def)
        {
            RegistryKey regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string value = regKey?.GetValue(name) as string;
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
                SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
            return def;
        }

        public static void RegWriteValue(string path, string name, object value)
        {
            RegistryKey regKey = null;
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
                SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
        }

        /// <summary>
        /// 判断.Net Framework的Release是否符合
        /// (.Net Framework 版本在4.0及以上)
        /// </summary>
        /// <param name="release">需要的版本4.6.2=394802;4.8=528040</param>
        /// <returns></returns>
        public static bool GetDotNetRelease(int release)
        {
            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
            {
                if (ndpKey != null && ndpKey.GetValue("Release") != null)
                {
                    return (int)ndpKey.GetValue("Release") >= release ? true : false;
                }
                return false;
            }
        }

        #endregion

        #region 测速

        /// <summary>
        /// Ping
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        public static long Ping(string host)
        {
            long roundtripTime = -1;
            try
            {
                int timeout = 30;
                int echoNum = 2;
                Ping pingSender = new Ping();
                for (int i = 0; i < echoNum; i++)
                {
                    PingReply reply = pingSender.Send(host, timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        if (reply.RoundtripTime < 0)
                        {
                            continue;
                        }
                        if (roundtripTime < 0 || reply.RoundtripTime < roundtripTime)
                        {
                            roundtripTime = reply.RoundtripTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
                return -1;
            }
            return roundtripTime;
        }

        /// <summary>
        /// 取得本机 IP Address
        /// </summary>
        /// <returns></returns>
        public static List<string> GetHostIPAddress()
        {
            List<string> lstIPAddress = new List<string>();
            try
            {
                IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ipa in IpEntry.AddressList)
                {
                    if (ipa.AddressFamily == AddressFamily.InterNetwork)
                        lstIPAddress.Add(ipa.ToString());
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return lstIPAddress;
        }

        public static void SetSecurityProtocol(bool enableSecurityProtocolTls13)
        {
            if (enableSecurityProtocolTls13)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                                           | SecurityProtocolType.Tls
                                           | SecurityProtocolType.Tls11
                                           | SecurityProtocolType.Tls12
                                           | SecurityProtocolType.Tls13;
            }
            else
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                                           | SecurityProtocolType.Tls
                                           | SecurityProtocolType.Tls11
                                           | SecurityProtocolType.Tls12;
            }
            ServicePointManager.DefaultConnectionLimit = 256;
        }

        public static bool PortInUse(int port)
        {
            bool inUse = false;

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
            return inUse;
        }
        #endregion

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
                            FileVersionInfo.GetVersionInfo(location).FileVersion.ToString(),
                            File.GetLastWriteTime(location).ToString("yyyy/MM/dd"));
                }
                else
                {
                    return string.Format("v2rayN/{0}",
                        FileVersionInfo.GetVersionInfo(location).FileVersion.ToString());
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 深度拷贝
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T DeepCopy<T>(T obj)
        {
            object retval;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                //序列化成流
                bf.Serialize(ms, obj);
                ms.Seek(0, SeekOrigin.Begin);
                //反序列化成对象
                retval = bf.Deserialize(ms);
                ms.Close();
            }
            return (T)retval;
        }

        /// <summary>
        /// 获取剪贴板数
        /// </summary>
        /// <returns></returns>
        public static string GetClipboardData()
        {
            string strData = string.Empty;
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                if (data.GetDataPresent(DataFormats.UnicodeText))
                {
                    strData = data.GetData(DataFormats.UnicodeText).ToString();
                }
                return strData;
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
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
                SaveLog(ex.Message, ex);
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
                SaveLog(ex.Message, ex);
                return false;
            }
        }

        public static void AddSubItem(ListViewItem i, string name, string text)
        {
            i.SubItems.Add(new ListViewItem.ListViewSubItem() { Name = name, Text = text });
        }

        public static string GetDownloadFileName(string url)
        {
            var fileName = System.IO.Path.GetFileName(url);
            fileName += "_temp";

            return fileName;
        }
        #endregion

        #region TempPath

        // return path to store temporary files
        public static string GetTempPath()
        {
            string _tempPath = Path.Combine(StartupPath(), "v2ray_win_temp");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            return _tempPath;
        }

        public static string GetTempPath(string filename)
        {
            return Path.Combine(GetTempPath(), filename);
        }

        public static string UnGzip(byte[] buf)
        {
            MemoryStream sb = new MemoryStream();
            using (GZipStream input = new GZipStream(new MemoryStream(buf),
            CompressionMode.Decompress,
            false))
            {
                input.CopyTo(sb);
            }
            return Encoding.UTF8.GetString(sb.ToArray());
        }

        #endregion

        #region Log

        public static void SaveLog(string strContent)
        {
            var logger = LogManager.GetLogger("Log1");
            logger.Info(strContent);
        }
        public static void SaveLog(string strTitle, Exception ex)
        {
            var logger = LogManager.GetLogger("Log2");
            logger.Debug(strTitle);
            logger.Debug(ex);
        }

        #endregion


        #region scan screen

        public static string ScanScreen()
        {
            try
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    using (Bitmap fullImage = new Bitmap(screen.Bounds.Width,
                                                    screen.Bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(fullImage))
                        {
                            g.CopyFromScreen(screen.Bounds.X,
                                             screen.Bounds.Y,
                                             0, 0,
                                             fullImage.Size,
                                             CopyPixelOperation.SourceCopy);
                        }
                        int maxTry = 10;
                        for (int i = 0; i < maxTry; i++)
                        {
                            int marginLeft = (int)((double)fullImage.Width * i / 2.5 / maxTry);
                            int marginTop = (int)((double)fullImage.Height * i / 2.5 / maxTry);
                            Rectangle cropRect = new Rectangle(marginLeft, marginTop, fullImage.Width - marginLeft * 2, fullImage.Height - marginTop * 2);
                            Bitmap target = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);

                            double imageScale = (double)screen.Bounds.Width / (double)cropRect.Width;
                            using (Graphics g = Graphics.FromImage(target))
                            {
                                g.DrawImage(fullImage, new Rectangle(0, 0, target.Width, target.Height),
                                                cropRect,
                                                GraphicsUnit.Pixel);
                            }

                            BitmapLuminanceSource source = new BitmapLuminanceSource(target);
                            BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
                            QRCodeReader reader = new QRCodeReader();
                            Result result = reader.decode(bitmap);
                            if (result != null)
                            {
                                string ret = result.Text;
                                return ret;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.Message, ex);
            }
            return string.Empty;
        }

        #endregion

    }
}
