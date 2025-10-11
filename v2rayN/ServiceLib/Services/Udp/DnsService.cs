using System.Buffers.Binary;
using System.Text;

namespace ServiceLib.Services.Udp;
public class DnsService : UdpService
{
    private const int DnsDefaultPort = 53;
    private const string DnsDefaultServer = "8.8.8.8"; // Google Public DNS
    private const string QueryDomain = "www.google.com";

    protected override byte[] BuildUdpRequestPacket()
    {
        // DNS Query for www.google.com A record
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Transaction ID (random 2 bytes)
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)0x1234));

        // Flags: Standard query (0x0100)
        // QR=0 (query), Opcode=0 (standard), RD=1 (recursion desired)
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)0x0100));

        // Questions: 1
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)1));

        // Answer RRs: 0
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)0));

        // Authority RRs: 0
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)0));

        // Additional RRs: 0
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)0));

        // Question section
        // Domain name: www.google.com
        var labels = QueryDomain.Split('.');
        foreach (var label in labels)
        {
            writer.Write((byte)label.Length);
            writer.Write(Encoding.ASCII.GetBytes(label));
        }
        writer.Write((byte)0); // End of domain name

        // Type: A (0x0001)
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)1));

        // Class: IN (0x0001)
        writer.Write(BinaryPrimitives.ReverseEndianness((ushort)1));

        return ms.ToArray();
    }

    protected override bool VerifyAndExtractUdpResponse(byte[] dnsResponseBytes)
    {
        if (dnsResponseBytes == null || dnsResponseBytes.Length < 12)
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

    protected override ushort GetDefaultTargetPort()
    {
        return DnsDefaultPort;
    }

    protected override string GetDefaultTargetHost()
    {
        return DnsDefaultServer;
    }
}
