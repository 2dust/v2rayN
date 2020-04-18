using System;
using System.Collections.Generic;

namespace v2rayN.Mode
{
    [Serializable]
    public class ServerStatistics
    {
        public List<ServerStatItem> server { get; set; }
    }

    [Serializable]
    public class ServerStatItem
    {
        public string itemId { get; set; }
        public ulong totalUp { get; set; }
        public ulong totalDown { get; set; }
        public ulong todayUp { get; set; }
        public ulong todayDown { get; set; }
        public long dateNow { get; set; }
    }
}
