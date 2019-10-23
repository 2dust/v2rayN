using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace v2rayN.Base
{
    public class HttpWebServerB
    {
        private TcpListener listener;
        private int port;
        private Func<string, string> _responderMethod;

        public HttpWebServerB(int port, Func<string, string> method)
        {
            this.port = port;
            this._responderMethod = method;

            Thread thread = new Thread(StartListen);
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
        }

        private void StartListen()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Utils.SaveLog("WebserverB running...");

            while (true)
            {
                if (!listener.Pending())
                {
                    continue;
                }

                TcpClient socket = listener.AcceptTcpClient();
                Thread thread = new Thread(new ParameterizedThreadStart(ProcessThread));
                thread.IsBackground = true;
                thread.Start(socket);
                Thread.Sleep(1);
            }
        }
        private void ProcessThread(object obj)
        {
            try
            {
                var socket = obj as TcpClient;

                var inputStream = new BufferedStream(socket.GetStream());
                var outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
                if (inputStream.CanRead)
                {
                    var data = ReadStream(inputStream);

                    if (data.Contains("/pac/"))
                    {
                        if (_responderMethod != null)
                        {
                            var address = ((IPEndPoint)socket.Client.LocalEndPoint).Address.ToString();
                            Utils.SaveLog("WebserverB Request " + address);
                            string pac = _responderMethod(address);

                            if (inputStream.CanWrite)
                            {
                                WriteStream(outputStream, pac);
                            }
                        }
                    }
                }

                outputStream.BaseStream.Flush();
                inputStream = null;
                outputStream = null;
                socket.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private string ReadStream(Stream inputStream)
        {
            int nextchar;
            string data = "";
            while (true)
            {
                nextchar = inputStream.ReadByte();
                if (nextchar == '\n')
                {
                    break;
                }
                if (nextchar == '\r')
                {
                    continue;
                }
                if (nextchar == -1)
                {
                    Thread.Sleep(1);
                    continue;
                };
                data += Convert.ToChar(nextchar);
            }
            return data;
        }

        private void WriteStream(StreamWriter outputStream, string pac)
        {
            var content_type = "application/x-ns-proxy-autoconfig";
            outputStream.WriteLine("HTTP/1.1 200 OK");
            outputStream.WriteLine(String.Format("Content-Type:{0}", content_type));
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.WriteLine(pac);
            outputStream.Flush();
        }
    }
}
