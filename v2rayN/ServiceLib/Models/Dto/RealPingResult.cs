namespace ServiceLib.Models.Dto;

public readonly record struct RealPingResult(int Delay, IpInfoResult? IpInfo = null);