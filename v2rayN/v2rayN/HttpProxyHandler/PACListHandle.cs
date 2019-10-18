using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Properties;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// 提供PAC功能支持
    /// </summary>
    class PACListHandle
    {
        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;

            public ResultEventArgs(bool success)
            {
                this.Success = success;
            }
        }

        private const string GFWLIST_URL = "https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        private static readonly IEnumerable<char> IgnoredLineBegins = new[] { '!', '[' };

        public void UpdatePACFromGFWList(Config config)
        {
            string url = GFWLIST_URL;
            if (!Utils.IsNullOrEmpty(config.urlGFWList))
            {
                url = config.urlGFWList;
            }

            //默认用户已开启系统代理
            //var httpProxy = config.inbound.FirstOrDefault(x => x.protocol=="http");
            //if (httpProxy == null)
            //{
            //    throw new Exception("未发现HTTP代理，无法设置代理更新");
            //}
            var http = new WebClientEx();
            //http.Headers.Add("Connection", "Close");
            //http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), httpProxy.localPort);
            http.DownloadStringCompleted += http_DownloadStringCompleted;
            http.DownloadStringAsync(new Uri(url));
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                File.WriteAllText(Utils.GetTempPath("gfwlist.txt"), e.Result, Encoding.UTF8);
                List<string> lines = ParseResult(e.Result);
                string abpContent = Utils.UnGzip(Resources.abp_js);
                abpContent = abpContent.Replace("__RULES__", JsonConvert.SerializeObject(lines, Formatting.Indented));
                File.WriteAllText(Utils.GetPath(Global.pacFILE), abpContent, Encoding.UTF8);
                if (UpdateCompleted != null) UpdateCompleted(this, new ResultEventArgs(true));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null) Error(this, new ErrorEventArgs(ex));
            }
        }

        public static List<string> ParseResult(string response)
        {
            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.UTF8.GetString(bytes);
            List<string> valid_lines = new List<string>();
            using (var sr = new StringReader(content))
            {
                foreach (var line in sr.NonWhiteSpaceLines())
                {
                    if (line.BeginWithAny(IgnoredLineBegins))
                        continue;
                    valid_lines.Add(line);
                }
            }
            return valid_lines;
        }
    }
}
