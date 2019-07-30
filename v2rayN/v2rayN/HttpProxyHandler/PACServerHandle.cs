using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using v2rayN.Mode;
using v2rayN.Properties;
using v2rayN.Tool;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// 提供PAC功能支持
    /// </summary>
    class PACServerHandle
    {
        private static Hashtable httpWebServer = new Hashtable();
        private static Hashtable pacList = new Hashtable();

        public static void Init(Config config)
        {
            InitServer("127.0.0.1");

            if (config.allowLANConn)
            {
                List<string> lstIPAddress = Utils.GetHostIPAddress();
                if (lstIPAddress.Count <= 0)
                {
                    return;
                }
                foreach (string str in lstIPAddress)
                {
                    InitServer(str);
                }
            }
        }

        public static void InitServer(string address)
        {
            try
            {
                if (!pacList.ContainsKey(address))
                {
                    pacList.Add(address, GetPacList(address));
                }

                string prefixes = string.Format("http://{0}:{1}/pac/", address, Global.pacPort);
                Utils.SaveLog("Webserver prefixes " + prefixes);

                HttpWebServer ws = new HttpWebServer(SendResponse, prefixes);
                ws.Run();

                if (!httpWebServer.ContainsKey(address) && ws != null)
                {
                    httpWebServer.Add(address, ws);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Webserver InitServer " + ex.Message);
            }
        }

        public static string SendResponse(HttpListenerRequest request)
        {
            try
            {
                string[] arrAddress = request.UserHostAddress.Split(':');
                string address = "127.0.0.1";
                if (arrAddress.Length > 0)
                {
                    address = arrAddress[0];
                }
                return pacList[address].ToString();
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Webserver SendResponse " + ex.Message);
                return ex.Message;
            }
        }


        public static void Stop()
        {
            try
            {
                if (httpWebServer == null)
                {
                    return;
                }
                foreach (var key in httpWebServer.Keys)
                {
                    Utils.SaveLog("Webserver Stop " + key.ToString());
                    ((HttpWebServer)httpWebServer[key]).Stop();
                }
                httpWebServer.Clear();
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Webserver Stop " + ex.Message);
            }
        }

        private static string GetPacList(string address)
        {
            var port = Global.sysAgentPort;
            if (port <= 0)
            {
                return "No port";
            }
            try
            {
                List<string> lstProxy = new List<string>();
                lstProxy.Add(string.Format("PROXY {0}:{1};", address, port));
                var proxy = string.Join("", lstProxy.ToArray());

                string strPacfile = Utils.GetPath(Global.pacFILE);
                if (!File.Exists(strPacfile))
                {
                    FileManager.UncompressFile(strPacfile, Resources.pac_txt);
                }
                var pac = File.ReadAllText(strPacfile, Encoding.UTF8);
                pac = pac.Replace("__PROXY__", proxy);
                return pac;
            }
            catch
            { }
            return "No pac content";
        }
    }
}
