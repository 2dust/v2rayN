namespace ServiceLib.UdpTest.Tester;

public class NtpService : IUdpTest
{
    private const int NtpDefaultPort = 123;
    private const string NtpDefaultServer = "pool.ntp.org";

    public byte[] BuildUdpRequestPacket()
    {
        var ntpReq = new byte[48];
        ntpReq[0] = 0x23; // LI=0, VN=4, Mode=3
        return ntpReq;
    }

    public bool VerifyAndExtractUdpResponse(byte[] ntpResponseBytes)
    {
        if (ntpResponseBytes.Length < 48)
        {
            return false;
        }
        if ((ntpResponseBytes[0] & 0x07) != 4)
        {
            return false;
        }
        return true;
    }

    public ushort GetDefaultTargetPort()
    {
        return NtpDefaultPort;
    }

    public string GetDefaultTargetHost()
    {
        return NtpDefaultServer;
    }
}
