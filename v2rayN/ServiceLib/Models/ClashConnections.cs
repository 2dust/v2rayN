namespace ServiceLib.Models
{
    public class ClashConnections
    {
        public ulong downloadTotal { get; set; }
        public ulong uploadTotal { get; set; }
        public List<ConnectionItem>? connections { get; set; }
    }

    public class ConnectionItem
    {
        public string? id { get; set; }
        public MetadataItem? metadata { get; set; }
        public ulong upload { get; set; }
        public ulong download { get; set; }
        public DateTime start { get; set; }
        public List<string>? chains { get; set; }
        public string? rule { get; set; }
        public string? rulePayload { get; set; }
    }

    public class MetadataItem
    {
        public string? network { get; set; }
        public string? type { get; set; }
        public string? sourceIP { get; set; }
        public string? destinationIP { get; set; }
        public string? sourcePort { get; set; }
        public string? destinationPort { get; set; }
        public string? host { get; set; }
        public string? nsMode { get; set; }
        public object? uid { get; set; }
        public string? process { get; set; }
        public string? processPath { get; set; }
        public string? remoteDestination { get; set; }
    }
}