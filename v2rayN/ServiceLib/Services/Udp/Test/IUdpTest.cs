namespace ServiceLib.Services.Udp.Test;

public interface IUdpTest
{
    public byte[] BuildUdpRequestPacket();
    public bool VerifyAndExtractUdpResponse(byte[] udpResponseBytes);
    public ushort GetDefaultTargetPort();
    public string GetDefaultTargetHost();
}