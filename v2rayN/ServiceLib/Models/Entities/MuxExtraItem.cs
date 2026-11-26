namespace ServiceLib.Models.Entities;

public record MuxExtraItem
{
    public XrayMuxItem? Xray { get; init; }
    public SingBoxMuxItem? SingBox { get; init; }

    public record XrayMuxItem
    {
        public bool? Enabled { get; init; }
        public int? Concurrency { get; init; }
        public int? XudpConcurrency { get; init; }
        public string? XudpProxyUDP443 { get; init; }
    }

    public record SingBoxMuxItem
    {
        public bool? Enabled { get; init; }
        public string? Protocol { get; init; }
        public int? MaxConnections { get; init; }
        public int? MinStreams { get; init; }
        public int? MaxStreams { get; init; }
        public bool? Padding { get; init; }
    }
}
