namespace ServiceLib.UdpTest.Tester;

public interface IUdpTest
{
    public byte[] BuildUdpRequestPacket();
    public bool VerifyAndExtractUdpResponse(byte[] udpResponseBytes);
    public ushort GetDefaultTargetPort();
    public string GetDefaultTargetHost();
}
