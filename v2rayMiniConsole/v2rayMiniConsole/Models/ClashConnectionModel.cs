namespace v2rayN.Models
{
    public class ClashConnectionModel
    {
        public string id { get; set; }
        public string network { get; set; }
        public string type { get; set; }
        public string host { get; set; }
        public ulong upload { get; set; }
        public ulong download { get; set; }
        public string uploadTraffic { get; set; }
        public string downloadTraffic { get; set; }
        public double time { get; set; }
        public string elapsed { get; set; }
        public string chain { get; set; }
    }
}