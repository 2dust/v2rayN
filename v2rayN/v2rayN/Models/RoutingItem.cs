using SQLite;

namespace v2rayN.Models
{
    [Serializable]
    public class RoutingItem
    {
        [PrimaryKey]
        public string id { get; set; }

        public string remarks { get; set; }
        public string url { get; set; }
        public string ruleSet { get; set; }
        public int ruleNum { get; set; }
        public bool enabled { get; set; } = true;
        public bool locked { get; set; }
        public string customIcon { get; set; }
        public string domainStrategy { get; set; }
        public string domainStrategy4Singbox { get; set; }
        public int sort { get; set; }
    }
}