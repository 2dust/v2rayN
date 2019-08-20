using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace v2rayN.HttpProxyHandler
{
    public class HttpWebServerC
    {
        private int port;
        private TcpListener listener;
        private bool is_active = true;
        private string pacRespone = string.Empty;

        public HttpWebServerC(int port, string pacRespone)
        {
            this.port = port;
            this.pacRespone = pacRespone;
        }

        public void WorkThread()
        {
            is_active = true;
            Listen();
        }

        public void Listen()
        {
            try
            {
                listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();

                Utils.SaveLog("WebserverB running...");

                while (is_active)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    //HttpWebProcessor processor = new HttpWebProcessor(client, pacRespone);
                    //Thread thread = new Thread(new ThreadStart(processor.process));
                    //thread.Start();
                    //Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public void Stop()
        {
            if (listener != null)
            {
                is_active = false;
                listener.Stop();
                listener = null;
            }
        }
    }
}
