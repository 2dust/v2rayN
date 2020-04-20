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

            Thread thread = new Thread(StartListen)
            {
                IsBackground = true
            };
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
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                Utils.SaveLog("WebserverB running...");

                while (true)
                {
                    if (!listener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    TcpClient socket = listener.AcceptTcpClient();
                    Thread thread = new Thread(new ParameterizedThreadStart(ProcessThread))
                    {
                        IsBackground = true
                    };
                    thread.Start(socket);
                    Thread.Sleep(1);
                }
            }
            catch
            {
                Utils.SaveLog("WebserverB start fail.");
            }
        }
        private void ProcessThread(object obj)
        {
            try
            {
                TcpClient socket = obj as TcpClient;

                BufferedStream inputStream = new BufferedStream(socket.GetStream());
                StreamWriter outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
                if (inputStream.CanRead)
                {
                    string data = ReadStream(inputStream);

                    if (data.Contains("/pac/"))
                    {
                        if (_responderMethod != null)
                        {
                            string address = ((IPEndPoint)socket.Client.LocalEndPoint).Address.ToString();
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
            string content_type = "application/x-ns-proxy-autoconfig";
            outputStream.WriteLine("HTTP/1.1 200 OK");
            outputStream.WriteLine(String.Format("Content-Type:{0}", content_type));
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.WriteLine(pac);
            outputStream.Flush();
        }
    }
}
