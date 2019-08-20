using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
        //private static Hashtable httpWebServer = new Hashtable();
        //private static Hashtable pacList = new Hashtable();

        //private static string pac = "";
        private static int pacPort = 0;
        private static HttpWebServerB server;
        // private static HttpWebServerC server;
        //static Thread thread;

        public static void Init(Config config)
        {
            InitServer("127.0.0.1");
            //if (config.allowLANConn)
            //{
            //    List<string> lstIPAddress = Utils.GetHostIPAddress();
            //    if (lstIPAddress.Count <= 0)
            //    {
            //        return;
            //    }
            //    InitServer(lstIPAddress[0]);
            //    //foreach (string str in lstIPAddress)
            //    //{
            //    //    InitServer(str);
            //    //}
            //}
            //else
            //{
            //    InitServer("127.0.0.1");
            //}
        }

        public static void InitServer(string address)
        {
            try
            {
                //pac = GetPacList(address);

                if (pacPort != Global.pacPort)
                {
                    if (server != null)
                    {
                        server.Stop();
                        server = null;
                    }

                    if (server == null)
                    {
                        server = new HttpWebServerB(Global.pacPort, SendResponse);
                        //server = new HttpWebServerC(Global.pacPort, pac);
                        pacPort = Global.pacPort;
                    }
                }

                //thread = new Thread(server.WorkThread);
                //thread.IsBackground = true;
                //thread.Start();
                Utils.SaveLog("Webserver at " + address);
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Webserver InitServer " + ex.Message);
            }

            //    if (!pacList.ContainsKey(address))
            //    {
            //        pacList.Add(address, GetPacList(address));
            //    }

            //    string prefixes = string.Format("http://{0}:{1}/pac/", address, Global.pacPort);
            //    Utils.SaveLog("Webserver prefixes " + prefixes);

            //    HttpWebServer ws = new HttpWebServer(SendResponse, prefixes);
            //    ws.Run();

            //    if (!httpWebServer.ContainsKey(address) && ws != null)
            //    {
            //        httpWebServer.Add(address, ws);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Utils.SaveLog("Webserver InitServer " + ex.Message);
            //}
        }

        public static string SendResponse(TcpClient tcpClient)
        {
            try
            {
                var address = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Address.ToString();
                var pac = GetPacList(address);

                Console.WriteLine("SendResponse addr " + address);
                Utils.SaveLog("SendResponse addr " + address);

                NetworkStream netStream = tcpClient.GetStream();
                if (netStream.CanRead)
                {
                    // Reads NetworkStream into a byte buffer.
                    byte[] bytes = new byte[tcpClient.ReceiveBufferSize];

                    // Read can return anything from 0 to numBytesToRead. 
                    // This method blocks until at least one byte is read.
                    netStream.Read(bytes, 0, (int)tcpClient.ReceiveBufferSize);

                    // Returns the data received from the host to the console.
                    string returndata = Encoding.UTF8.GetString(bytes);
                    if (!string.IsNullOrEmpty(returndata)
                        && returndata.IndexOf("/pac/") >= 0
                        && netStream.CanWrite)
                    {

                        Byte[] sendBytes = Encoding.UTF8.GetBytes(writeSuccess(pac));
                        netStream.Write(sendBytes, 0, sendBytes.Length);
                    }
                }

                netStream.Close();
                tcpClient.Close();
                return "";
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return "";
        }


        private static string writeSuccess(string pac)
        {
            StringBuilder sb = new StringBuilder();
            string content_type = "application/x-ns-proxy-autoconfig";

            sb.Append("HTTP/1.0 200 OK");
            sb.AppendLine();
            sb.Append(String.Format("Content-Type:{0};charset=utf-8", content_type));
            sb.AppendLine();
            sb.Append("Connection: close");
            sb.AppendLine();
            sb.Append(pac);
            sb.AppendLine();

            return sb.ToString();
        }

        /*
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
         */

        public static void Stop()
        {
            //try
            //{
            //    if (server != null)
            //    {
            //        server.Stop();
            //        server = null;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Utils.SaveLog("Webserver Stop " + ex.Message);
            //}

            //try
            //{
            //    if (httpWebServer == null)
            //    {
            //        return;
            //    }
            //    foreach (var key in httpWebServer.Keys)
            //    {
            //        Utils.SaveLog("Webserver Stop " + key.ToString());
            //        ((HttpWebServer)httpWebServer[key]).Stop();
            //    }
            //    httpWebServer.Clear();
            //}
            //catch (Exception ex)
            //{
            //    Utils.SaveLog("Webserver Stop " + ex.Message);
            //}
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
