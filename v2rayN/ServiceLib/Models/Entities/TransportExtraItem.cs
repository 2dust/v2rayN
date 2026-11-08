namespace ServiceLib.Models.Entities;

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
    public int? KcpMtu { get; init; }

    public string? XdriveService { get; init; }
    public string? XdriveRemoteFolder { get; init; }
    public string? XdriveSecrets { get; init; }
    public string? XdriveS3Endpoint { get; init; }
    public string? XdriveS3Region { get; init; }
    public string? XdriveS3Bucket { get; init; }
    public bool? XdriveS3PathStyle { get; init; }
    public string? XdriveExtra { get; init; }
}
