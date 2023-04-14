using SQLite;

namespace v2rayN.Mode
{
    [Serializable]
    public class SubItem
    {
        [PrimaryKey]
        public string id { get; set; }

        public string remarks { get; set; }

        public string url { get; set; }

        public string moreUrl { get; set; }

        public bool enabled { get; set; } = true;

        public string userAgent { get; set; } = string.Empty;

        public int sort { get; set; }

        public string filter { get; set; }

        public int autoUpdateInterval { get; set; }

        public long updateTime { get; set; }
    }
}