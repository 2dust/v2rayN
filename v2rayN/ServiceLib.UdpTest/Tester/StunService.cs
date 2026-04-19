namespace ServiceLib.UdpTest.Tester;

public class StunService : IUdpTest
{
    private const int StunDefaultPort = 3478;
    private const string StunDefaultServer = "stun.voztovoice.org";
    private static readonly byte[] StunBindingRequestPacket =
    [
        // STUN Binding Request
        0x00, 0x01, // Message Type: Binding Request (0x0001)
        0x00, 0x00, // Message Length: 0 (no attributes)
        0x21, 0x12, 0xA4, 0x42, // Magic Cookie: 0x2112A442
        // Transaction ID: 96 bits (12 bytes) random
        0x66, 0x0E, 0xAB, 0xBC, 0x61, 0x0D,
        0xA4, 0x40, 0x8C, 0x65, 0xC1, 0xBE,
    ];

    public byte[] BuildUdpRequestPacket()
    {
        return (byte[])StunBindingRequestPacket.Clone();
    }

    public bool VerifyAndExtractUdpResponse(byte[] stunResponseBytes)
    {
        if (stunResponseBytes.Length < 20)
        {
            return false;
        }

        if (stunResponseBytes.Length >= 2)
        {
            var messageType = (stunResponseBytes[0] << 8) | stunResponseBytes[1];
            if (messageType is 0x0101 or 0x0111)
            {
                return true;
            }
        }

        return true;
    }

    public ushort GetDefaultTargetPort()
    {
        return StunDefaultPort;
    }

    public string GetDefaultTargetHost()
    {
        return StunDefaultServer;
    }
}
