using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace v2rayN.HttpProxyHandler
{
    public class HttpWebServerB
    {
        TcpListener listener;
        bool is_active = true;
        private Stream inputStream;
        public StreamWriter outputStream;

        public String http_method;
        public String http_url;
        public String http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();

        private int port;
        private Func<string, string> _responderMethod;

        public HttpWebServerB(int port, Func<string, string> method)
        {
            this.port = port;
            this._responderMethod = method;

            Thread thread = new Thread(WorkThread);
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            //if (listener != null)
            //{
            //    listener.Stop();
            //    listener = null;
            //}
        }

        public void WorkThread()
        {
            listen();
        }

        private void listen()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            while (is_active)
            {
                TcpClient socket = listener.AcceptTcpClient();
                //HttpProcessor processor = new HttpProcessor(s, this);
                //Thread thread = new Thread(new ThreadStart(process));
                Thread thread = new Thread(new ParameterizedThreadStart(process));
                thread.Start(socket);

                //thread.Start();
                Thread.Sleep(1);
            }
        }
        private void process(object obj)
        {
            try
            {
                var socket = obj as TcpClient;

                inputStream = new BufferedStream(socket.GetStream());
                outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET"))
                {
                    handleGETRequest(socket);
                }

                outputStream.BaseStream.Flush();
                inputStream = null; outputStream = null; // bs = null;   
                socket.Close();
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// 复制请求
        /// </summary>
        private void parseRequest()
        {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];
        }
        /// <summary>
        /// 读取请求头
        /// </summary>
        private void readHeaders()
        {
            String line;
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                httpHeaders[name] = value;
            }
        }
        private string streamReadLine(Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }

        /// <summary>
        /// 重载相应Get方法
        /// </summary>
        /// <param name="p"></param>
        private void handleGETRequest(TcpClient socket)
        {
            String executeResult = String.Empty;
            if (http_url.ToLower().Contains("/pac/"))
            {               
                var address = ((IPEndPoint)socket.Client.LocalEndPoint).Address.ToString();
                string pac = _responderMethod(address);

                writeSuccess("application/x-ns-proxy-autoconfig");
                outputStream.WriteLine(pac);
                outputStream.Flush();
                return;
            }
        }

        /// <summary>
        /// 回复成功
        /// </summary>
        /// <param name="content_type"></param>
        private void writeSuccess(string content_type = "text/html")
        {
            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine(String.Format("Content-Type:{0};", content_type));
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }
    }
}
