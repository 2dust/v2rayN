using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

internal readonly record struct RouteFlow(byte Protocol, IPAddress LocalAddress, ushort LocalPort,
    IPAddress RemoteAddress, ushort RemotePort);

/// <summary>Bounds-checked IP/TCP/UDP parsing. Checksums are calculated by WinDivert after rewriting.</summary>
internal sealed record RoutePacket(RouteFlow Flow, int TransportOffset, int SourceOffset, int DestinationOffset, int AddressLength, byte TcpFlags, uint TcpSequence)
{
    public bool IsTcpSyn => Flow.Protocol == 6 && (TcpFlags & 0x12) == 0x02;

    public static RoutePacket? Parse(ReadOnlySpan<byte> bytes, uint interfaceIndex = 0)
    {
        if (bytes.Length < 20)
        {
            return null;
        }

        int transport, source, destination, length;
        byte protocol;
        if ((bytes[0] >> 4) == 4)
        {
            transport = (bytes[0] & 15) * 4;
            var total = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            // Outbound fragments cannot safely be attributed from their transport header.
            if (transport < 20 || total < transport || total > bytes.Length ||
                (BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]) & 0x3fff) != 0)
            {
                return null;
            }

            bytes = bytes[..total];
            protocol = bytes[9];
            source = 12;
            destination = 16;
            length = 4;
        }
        else if ((bytes[0] >> 4) == 6)
        {
            if (bytes.Length < 40)
            {
                return null;
            }

            var total = 40 + BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
            if (total > bytes.Length)
            {
                return null;
            }

            bytes = bytes[..total];
            transport = 40;
            protocol = bytes[6];
            source = 8;
            destination = 24;
            length = 16;
            // Walk extension headers; reject fragments and opaque IPsec payloads.
            for (var i = 0; protocol is 0 or 43 or 60; i++)
            {
                if (i >= 8 || transport + 2 > bytes.Length)
                {
                    return null;
                }

                var next = bytes[transport];
                transport += (bytes[transport + 1] + 1) * 8;
                if (transport > bytes.Length)
                {
                    return null;
                }

                protocol = next;
            }
        }
        else
        {
            return null;
        }

        var minimum = protocol == 6 ? 20 : protocol == 17 ? 8 : int.MaxValue;
        if (minimum > bytes.Length - transport)
        {
            return null;
        }

        if (protocol == 6 && ((bytes[transport + 12] >> 4) * 4 < 20 ||
                             (bytes[transport + 12] >> 4) * 4 > bytes.Length - transport))
        {
            return null;
        }

        if (protocol == 17 && (BinaryPrimitives.ReadUInt16BigEndian(bytes[(transport + 4)..]) < 8 ||
                              BinaryPrimitives.ReadUInt16BigEndian(bytes[(transport + 4)..]) > bytes.Length - transport))
        {
            return null;
        }

        IPAddress ReadAddress(ReadOnlySpan<byte> raw)
        {
            var address = new IPAddress(raw);
            return address.IsIPv6LinkLocal ? new IPAddress(raw, interfaceIndex) : address;
        }
        return new(new(protocol, ReadAddress(bytes.Slice(source, length)),
                BinaryPrimitives.ReadUInt16BigEndian(bytes[transport..]),
                ReadAddress(bytes.Slice(destination, length)),
                BinaryPrimitives.ReadUInt16BigEndian(bytes[(transport + 2)..])),
            transport, source, destination, length, protocol == 6 ? bytes[transport + 13] : (byte)0,
            protocol == 6 ? BinaryPrimitives.ReadUInt32BigEndian(bytes[(transport + 4)..]) : 0);
    }

    public void Rewrite(Span<byte> bytes, IPAddress source, ushort sourcePort, IPAddress destination, ushort destinationPort)
    {
        if (source.GetAddressBytes().Length != AddressLength || destination.GetAddressBytes().Length != AddressLength)
        {
            throw new ArgumentException("Packet address families must match.");
        }

        source.GetAddressBytes().CopyTo(bytes[SourceOffset..]);
        destination.GetAddressBytes().CopyTo(bytes[DestinationOffset..]);
        BinaryPrimitives.WriteUInt16BigEndian(bytes[TransportOffset..], sourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(bytes[(TransportOffset + 2)..], destinationPort);
    }
    public static byte[] CreateUdpReply(RouteFlow flow, ReadOnlySpan<byte> payload)
    {
        var v6 = flow.LocalAddress.AddressFamily == AddressFamily.InterNetworkV6;
        var header = v6 ? 40 : 20;
        if (payload.Length > (v6 ? 65527 : 65507))
        {
            throw new IOException("UDP payload too large.");
        }

        var bytes = new byte[header + 8 + payload.Length];
        bytes[0] = v6 ? (byte)0x60 : (byte)0x45;
        if (v6)
        {
            bytes[6] = 17;
            bytes[7] = 64;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)(payload.Length + 8));
        }
        else
        {
            bytes[8] = 64;
            bytes[9] = 17;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)bytes.Length);
        }
        flow.RemoteAddress.GetAddressBytes().CopyTo(bytes, v6 ? 8 : 12);
        flow.LocalAddress.GetAddressBytes().CopyTo(bytes, v6 ? 24 : 16);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(header), flow.RemotePort);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(header + 2), flow.LocalPort);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(header + 4), (ushort)(payload.Length + 8));
        payload.CopyTo(bytes.AsSpan(header + 8));
        return bytes;
    }

}
