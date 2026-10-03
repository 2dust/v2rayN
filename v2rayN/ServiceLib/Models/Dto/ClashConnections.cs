namespace ServiceLib.Models.Dto;

public record ClashConnections
{
    public ulong downloadTotal { get; init; }
    public ulong uploadTotal { get; init; }
    public List<ConnectionItem>? connections { get; init; }
}

public record ConnectionItem
{
    public string? id { get; init; }
    public MetadataItem? metadata { get; init; }
    public ulong upload { get; init; }
    public ulong download { get; init; }
    public DateTime start { get; init; }
    public List<string>? chains { get; init; }
    public string? rule { get; init; }
    public string? rulePayload { get; init; }
}

public record MetadataItem
{
    public string? network { get; init; }
    public string? type { get; init; }
    public string? sourceIP { get; init; }
    public string? destinationIP { get; init; }
    public string? sourcePort { get; init; }
    public string? destinationPort { get; init; }
    public string? host { get; init; }
    public string? nsMode { get; init; }
    public object? uid { get; init; }
    public string? process { get; init; }
    public string? processPath { get; init; }
    public string? remoteDestination { get; init; }
    public string? sniffHost { get; init; }
}
