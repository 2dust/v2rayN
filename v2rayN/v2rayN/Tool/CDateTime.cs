using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace v2rayN
{
    class CDateTime
    {
        /// <summary>
        /// 设置本地系统时间
        /// </summary>
        public static void SetLocalTime()
        {
            using (WebClient wc = new WebClient())
            {
                string url = "";
                string result = string.Empty;

                try
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.DownloadStringCompleted += wc_DownloadStringCompleted;
                    wc.DownloadStringAsync(new Uri(url));
                }
                catch
                {
                }
            }
        }

        static void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                string result = e.Result;
                if (Utils.IsNullOrEmpty(result))
                {
                    return;
                }
                EWebTime webTime = Utils.FromJson<EWebTime>(result);
                if (webTime != null
                    && webTime.result != null
                    && webTime.result.stime != null
                    && !Utils.IsNullOrEmpty(webTime.result.stime))
                {
                    DateTime dtWeb = GetTimeFromLinux(webTime.result.stime);

                    SYSTEMTIME st = new SYSTEMTIME();
                    st.FromDateTime(dtWeb);

                    //调用Win32 API设置系统时间
                    Win32API.SetLocalTime(ref st);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 时间戳转为C#格式时间
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        private static DateTime GetTimeFromLinux(string timeStamp)
        {
            DateTime dtStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            long lTime = long.Parse(timeStamp + "0000000");
            TimeSpan toNow = new TimeSpan(lTime); return dtStart.Add(toNow);
        }
    }

    /// <summary>
    ///
    /// </summary>
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;

        /// <summary>
        /// 从System.DateTime转换。
        /// </summary>
        /// <param name="time">System.DateTime类型的时间。</param>
        public void FromDateTime(DateTime time)
        {
            wYear = (ushort)time.Year;
            wMonth = (ushort)time.Month;
            wDayOfWeek = (ushort)time.DayOfWeek;
            wDay = (ushort)time.Day;
            wHour = (ushort)time.Hour;
            wMinute = (ushort)time.Minute;
            wSecond = (ushort)time.Second;
            wMilliseconds = (ushort)time.Millisecond;
        }

        /// <summary>
        /// 转换为System.DateTime类型。
        /// </summary>
        /// <returns></returns>
        public DateTime ToDateTime()
        {
            return new DateTime(wYear, wMonth, wDay, wHour, wMinute, wSecond, wMilliseconds);
        }

        /// <summary>
        /// 静态方法。转换为System.DateTime类型。
        /// </summary>
        /// <param name="time">SYSTEMTIME类型的时间。</param>
        /// <returns></returns>
        public static DateTime ToDateTime(SYSTEMTIME time)
        {
            return time.ToDateTime();
        }
    }

    public class Win32API
    {
        [DllImport("Kernel32.dll")]
        public static extern bool SetLocalTime(ref SYSTEMTIME Time);
        [DllImport("Kernel32.dll")]
        public static extern void GetLocalTime(ref SYSTEMTIME Time);
    }

    public class WTResult
    {
        /// <summary>
        /// 
        /// </summary>
        public string stime { get; set; }
    }

    public class EWebTime
    {
        /// <summary>
        /// 
        /// </summary>
        public WTResult result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int error_code { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string reason { get; set; }
    }
}

