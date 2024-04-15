using SQLite;

namespace v2rayN.Models
{
    [Serializable]
    public class SingGeoRuleSet
    {
        [PrimaryKey]
        public string id { get; set; }

        public string remarks { get; set; }

        public string rules { get; set; }
    }
}
