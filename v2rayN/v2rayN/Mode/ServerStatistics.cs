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
        public ulong totalUp;
        public ulong totalDown;
        public ulong todayUp;
        public ulong todayDown;

        public ServerStatistics() { }
        public ServerStatistics(string name, string addr, int port, ulong totalUp, ulong totalDown, ulong todayUp, ulong todayDown)
        {
            this.name = name;
            this.address = addr;
            this.port = port;
            this.totalUp = totalUp;
            this.totalDown = totalDown;
            this.todayUp = todayUp;
            this.todayDown = todayDown;
        }
    }
}
