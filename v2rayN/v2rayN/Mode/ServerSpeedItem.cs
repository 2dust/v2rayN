namespace v2rayN.Mode
{
    [Serializable]
    internal class ServerSpeedItem : ServerStatItem
    {
        public long proxyUp
        {
            get; set;
        }

        public long proxyDown
        {
            get; set;
        }

        public long directUp
        {
            get; set;
        }

        public long directDown
        {
            get; set;
        }
    }

    [Serializable]
    public class TrafficItem
    {
        public ulong up
        {
            get; set;
        }

        public ulong down
        {
            get; set;
        }
    }
}