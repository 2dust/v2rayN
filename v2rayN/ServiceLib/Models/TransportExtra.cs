namespace ServiceLib.Models;

public record TransportExtra
{
    public string? TcpHeaderType { get; init; }
    public string? TcpHost { get; init; }

    public string? WsHost { get; init; }
    public string? WsPath { get; init; }

    public string? HttpupgradeHost { get; init; }
    public string? HttpupgradePath { get; init; }

    public string? XhttpHost { get; init; }
    public string? XhttpPath { get; init; }
    public string? XhttpMode { get; init; }
    public string? XhttpExtra { get; init; }

    public string? GrpcAuthority { get; init; }
    public string? GrpcServiceName { get; init; }
    public string? GrpcMode { get; init; }

    public string? KcpHeaderType { get; init; }
    public string? KcpSeed { get; init; }
}