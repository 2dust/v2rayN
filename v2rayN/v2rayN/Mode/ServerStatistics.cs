using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace v2rayN.Mode
{
    class ServerStatistics
    {
        public string name;
        public string address;
        public int port;
        public string path;
        public string host;
        public ulong totalUp;
        public ulong totalDown;
        public ulong todayUp;
        public ulong todayDown;

        public ServerStatistics() { }
        public ServerStatistics(string name, string addr, int port, string path, string host, ulong totalUp, ulong totalDown, ulong todayUp, ulong todayDown)
        {
            this.name = name;
            this.address = addr;
            this.port = port;
            this.path = path;
            this.host = host;
            this.totalUp = totalUp;
            this.totalDown = totalDown;
            this.todayUp = todayUp;
            this.todayDown = todayDown;
        }
    }
}
