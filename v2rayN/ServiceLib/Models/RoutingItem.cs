using SQLite;

namespace ServiceLib.Models
{
    [Serializable]
    public class RoutingItem
    {
        [PrimaryKey]
        public string Id { get; set; }

        public string Remarks { get; set; }
        public string Url { get; set; }
        public string RuleSet { get; set; }
        public int RuleNum { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Locked { get; set; }
        public string CustomIcon { get; set; }
        public string CustomRulesetPath4Singbox { get; set; }
        public string DomainStrategy { get; set; }
        public string DomainStrategy4Singbox { get; set; }
        public int Sort { get; set; }
    }
}