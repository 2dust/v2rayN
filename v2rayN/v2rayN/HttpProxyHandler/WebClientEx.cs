using System;
using System.Net;

namespace v2rayN.HttpProxyHandler
{
    class WebClientEx : WebClient
    {
        public int Timeout { get; set; }
        public WebClientEx(int timeout = 3000)
        {
            Timeout = timeout;
        }


        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            request.Timeout = Timeout;
            return request;
        }
    }
}
