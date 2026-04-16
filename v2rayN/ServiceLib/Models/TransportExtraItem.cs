namespace ServiceLib.Models;

public record TransportExtraItem
{
    public string? RawHeaderType { get; init; }

    public string? Host { get; init; }
    public string? Path { get; init; }
    public string? XhttpMode { get; init; }
    public string? XhttpExtra { get; init; }

    public string? GrpcAuthority { get; init; }
    public string? GrpcServiceName { get; init; }
    public string? GrpcMode { get; init; }

    public string? KcpHeaderType { get; init; }
    public string? KcpSeed { get; init; }
}
