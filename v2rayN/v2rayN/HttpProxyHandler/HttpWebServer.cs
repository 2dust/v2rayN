using System;
using System.Net;
using System.Text;
using System.Threading;

namespace v2rayN.HttpProxyHandler
{
    public class HttpWebServer
    {
        private HttpListener _listener;
        private Func<HttpListenerRequest, string> _responderMethod;

        public HttpWebServer(string[] prefixes, Func<HttpListenerRequest, string> method)
        {
            try
            {
                _listener = new HttpListener();

                if (!HttpListener.IsSupported)
                    throw new NotSupportedException(
                        "Needs Windows XP SP2, Server 2003 or later.");

                // URI prefixes are required, for example 
                // "http://localhost:8080/index/".
                if (prefixes == null || prefixes.Length == 0)
                    throw new ArgumentException("prefixes");

                // A responder method is required
                if (method == null)
                    throw new ArgumentException("method");

                foreach (string s in prefixes)
                    _listener.Prefixes.Add(s);

                _responderMethod = method;
                _listener.Start();

            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        public HttpWebServer(Func<HttpListenerRequest, string> method, params string[] prefixes)
            : this(prefixes, method) { }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Utils.SaveLog("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                string rstr = _responderMethod(ctx.Request);
                                byte[] buf = Encoding.UTF8.GetBytes(rstr);
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentType = "application/x-ns-proxy-autoconfig";
                                ctx.Response.ContentLength64 = buf.Length;
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            catch
                            {
                            }  // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch (Exception ex)
                {
                    Utils.SaveLog(ex.Message, ex);
                } // suppress any exceptions
            });
        }

        public void Stop()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
        }

    }
}
