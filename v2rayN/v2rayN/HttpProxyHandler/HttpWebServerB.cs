using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
                throw;
            }

            Utils.SaveLog("WebserverB running...");
            AsyncCallback callback = null;
            listener.BeginAcceptTcpClient(callback = ((ares) =>
            {
                try
                {
                    if (listener != null)
                    {
                        TcpClient tcpClient = listener.EndAcceptTcpClient(ares);
                        listener.BeginAcceptTcpClient(callback, null);

                        if (tcpClient != null && _responderMethod != null)
                        {
                            string pac = _responderMethod(tcpClient);

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
                                if (!Utils.IsNullOrEmpty(returndata)
                                    && returndata.IndexOf("/pac/") >= 0
                                    && netStream.CanWrite)
                                {
                                    BinaryWriter writer = new BinaryWriter(netStream);
                                    //writeSuccess(writer, pac);

                                    Byte[] sendBytes = ASCIIEncoding.ASCII.GetBytes(writeSuccess(pac));
                                    writer.Write(sendBytes, 0, sendBytes.Length);
                                    writer.Flush();

                                    writer.Close();
                                }
                            }

                            netStream.Close();
                            tcpClient.Close();
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
        

        //private static void writeSuccess(BinaryWriter writer, string pac)
        //{
        //    writer.Write("HTTP/1.0 200 OK");
        //    writer.Write(Environment.NewLine);
        //    writer.Write("Content-Type:application/x-ns-proxy-autoconfig; charset=UTF-8");
        //    writer.Write(Environment.NewLine);
        //    writer.Write("Content-Length: " + pac.Length);
        //    writer.Write(Environment.NewLine);
        //    writer.Write(Environment.NewLine);
        //    writer.Write(pac);
        //    writer.Flush();

        //}

        private static string writeSuccess(string pac)
        {
            StringBuilder sb = new StringBuilder();
            string content_type = "application/x-ns-proxy-autoconfig";

            sb.Append("HTTP/1.0 200 OK");
            sb.AppendLine();
            sb.Append(String.Format("Content-Type:{0};", content_type));
            sb.AppendLine();
            //sb.Append("Connection: close");
            //sb.AppendLine();
            sb.Append(pac);
            sb.AppendLine();

            return sb.ToString();
        }

    }
}
