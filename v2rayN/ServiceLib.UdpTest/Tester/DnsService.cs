namespace ServiceLib.UdpTest.Tester;

public class DnsService : IUdpTest
{
    private const int DnsDefaultPort = 53;
    private const string DnsDefaultServer = "8.8.8.8"; // Google Public DNS
    private static readonly byte[] DnsQueryPacket =
    [
        // Header: ID=0x1234, Standard query with RD set, QDCOUNT=1
        0x12, 0x34, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        // Question: www.google.com, Type A, Class IN
        0x03, 0x77, 0x77, 0x77, 0x06, 0x67, 0x6F, 0x6F,
        0x67, 0x6C, 0x65, 0x03, 0x63, 0x6F, 0x6D, 0x00,
        0x00, 0x01, 0x00, 0x01
    ];

    public byte[] BuildUdpRequestPacket()
    {
        return (byte[])DnsQueryPacket.Clone();
    }

    public bool VerifyAndExtractUdpResponse(byte[] dnsResponseBytes)
    {
        if (dnsResponseBytes.Length < 12)
        {
            return false;
        }

        try
        {
            // Check transaction ID (should match 0x1234)
            var transactionId = BinaryPrimitives.ReadUInt16BigEndian(dnsResponseBytes.AsSpan(0, 2));
            if (transactionId != 0x1234)
            {
                return false;
            }

            // Check flags - should be a response (QR=1)
            var flags = BinaryPrimitives.ReadUInt16BigEndian(dnsResponseBytes.AsSpan(2, 2));
            if ((flags & 0x8000) == 0)
            {
                return false; // Not a response
            }

            // Check response code (RCODE) - should be 0 (no error)
            if ((flags & 0x000F) != 0)
            {
                return false; // DNS error
            }

            // Check answer count
            var answerCount = BinaryPrimitives.ReadUInt16BigEndian(dnsResponseBytes.AsSpan(6, 2));
            if (answerCount == 0)
            {
                return false; // No answers
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public ushort GetDefaultTargetPort()
    {
        return DnsDefaultPort;
    }

    public string GetDefaultTargetHost()
    {
        return DnsDefaultServer;
    }
}
