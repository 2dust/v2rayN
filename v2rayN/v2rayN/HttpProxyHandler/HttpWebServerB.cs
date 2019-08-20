using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace v2rayN.HttpProxyHandler
{
    public class HttpWebServerB
    {
        private int port;
        private TcpListener listener;

        private Func<TcpClient, string> _responderMethod;

        public HttpWebServerB(int port, Func<TcpClient, string> method)
        {
            try
            {
                this.port = port;
                this._responderMethod = method;

                listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }

            Utils.SaveLog("WebserverB running...");
            AsyncCallback callback = null;
            listener.BeginAcceptTcpClient(callback = ((ares) =>
            {
                try
                {
                    if (listener != null)
                    {
                        TcpClient client = listener.EndAcceptTcpClient(ares);
                        listener.BeginAcceptTcpClient(callback, null);

                        if (client != null && _responderMethod != null)
                        {
                            _responderMethod(client);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.SaveLog(ex.Message, ex);
                }
                //Console.WriteLine("Client connected completed");

            }), null);
        }


        public void Stop()
        {
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
        }

    }
}
