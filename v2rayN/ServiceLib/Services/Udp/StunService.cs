using System.Security.Cryptography;

namespace ServiceLib.Services.Udp;
public class StunService : UdpService
{
    private const int StunDefaultPort = 3478;
    private const string StunDefaultServer = "stun.voztovoice.org";
    private byte[] _transactionId;

    protected override byte[] BuildUdpRequestPacket()
    {
        // STUN Binding Request
        var packet = new byte[20];

        // Message Type: Binding Request (0x0001)
        packet[0] = 0x00;
        packet[1] = 0x01;

        // Message Length: 0 (no attributes)
        packet[2] = 0x00;
        packet[3] = 0x00;

        // Magic Cookie: 0x2112A442
        packet[4] = 0x21;
        packet[5] = 0x12;
        packet[6] = 0xA4;
        packet[7] = 0x42;

        // Transaction ID: 96 bits (12 bytes) random
        _transactionId = new byte[12];
        RandomNumberGenerator.Fill(_transactionId);
        Array.Copy(_transactionId, 0, packet, 8, 12);

        return packet;
    }

    protected override bool VerifyAndExtractUdpResponse(byte[] stunResponseBytes)
    {
        if (stunResponseBytes == null || stunResponseBytes.Length < 20)
        {
            return false;
        }

        // Message Type: Binding Success Response (0x0101) 或 Binding Error Response (0x0111)
        if (stunResponseBytes.Length >= 2)
        {
            var messageType = (stunResponseBytes[0] << 8) | stunResponseBytes[1];
            // 0x0101 = Success Response, 0x0111 = Error Response
            if (messageType is 0x0101 or 0x0111)
            {
                return true;
            }
        }

        return true;
    }

    protected override ushort GetDefaultTargetPort()
    {
        return StunDefaultPort;
    }

    protected override string GetDefaultTargetHost()
    {
        return StunDefaultServer;
    }
}
