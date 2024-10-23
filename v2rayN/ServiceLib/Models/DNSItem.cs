using SQLite;

namespace ServiceLib.Models
{
    [Serializable]
    public class DNSItem
    {
        [PrimaryKey]
        public string Id { get; set; }

        public string Remarks { get; set; }
        public bool Enabled { get; set; } = true;
        public ECoreType CoreType { get; set; }
        public bool UseSystemHosts { get; set; }
        public string? NormalDNS { get; set; }
        public string? TunDNS { get; set; }
        public string? DomainStrategy4Freedom { get; set; }
        public string? DomainDNSAddress { get; set; }
    }
}