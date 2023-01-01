namespace v2rayN.Mode
{
    [Serializable]
    class ServerSpeedItem
    {
        public string indexId
        {
            get; set;
        }
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
}
