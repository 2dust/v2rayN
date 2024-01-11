namespace v2rayN.Mode
{
    [Serializable]
    public class RulesItem
    {
        public string id { get; set; }
        public string type { get; set; }

        public string port { get; set; }

        public List<string> inboundTag { get; set; }

        public string outboundTag { get; set; }

        // 分流修改
        public string bingServerIndexId { get; set; }

        public List<string> ip { get; set; }

        public List<string> domain { get; set; }

        public List<string> protocol { get; set; }

        public List<string> process { get; set; }

        public bool enabled { get; set; } = true;
    }
}