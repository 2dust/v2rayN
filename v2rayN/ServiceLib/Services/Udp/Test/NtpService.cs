using System.Buffers.Binary;

namespace ServiceLib.Services.Udp.Test;

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
        if (ntpResponseBytes == null || ntpResponseBytes.Length < 48)
        {
            return false;
        }
        if ((ntpResponseBytes[0] & 0x07) != 4)
        {
            return false;
        }
        try
        {
            var secsSince1900 = BinaryPrimitives.ReadUInt32BigEndian(ntpResponseBytes.AsSpan(40, 4));
            const long ntpToUnixEpochOffsetSeconds = 2208988800L;
            var unixSecs = (long)secsSince1900 - ntpToUnixEpochOffsetSeconds;
            return true;
        }
        catch
        {
            return false;
        }
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