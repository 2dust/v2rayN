using SQLite;

namespace ServiceLib.Models
{
    [Serializable]
    public class SubItem
    {
        [PrimaryKey]
        public string Id { get; set; }

        public string Remarks { get; set; }

        public string Url { get; set; }

        public string MoreUrl { get; set; }

        public bool Enabled { get; set; } = true;

        public string UserAgent { get; set; } = string.Empty;

        public int Sort { get; set; }

        public string? Filter { get; set; }

        public int AutoUpdateInterval { get; set; }

        public long UpdateTime { get; set; }

        public string? ConvertTarget { get; set; }

        public string? PrevProfile { get; set; }

        public string? NextProfile { get; set; }

        public int? PreSocksPort { get; set; }
    }
}