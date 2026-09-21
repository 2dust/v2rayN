using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

/// <summary>A bounded, synchronous injection batch. Its sink must consume the borrowed buffers before returning.</summary>
internal sealed class RoutePacketBatch(RoutePacketBatch.Sink send)
{
    internal delegate void Sink(Span<byte> packets, Span<DivertAddress> addresses);
    internal const int Capacity = 32;
    internal const int MaxPacketLength = 65575;
    internal const int AddressSize = 80;
    private readonly byte[] _packets = new byte[MaxPacketLength];
    private readonly DivertAddress[] _addresses = new DivertAddress[Capacity];
    private int _length;
    private int _count;

    public void Add(ReadOnlySpan<byte> packet, DivertAddress address)
    {
        if (packet.Length > _packets.Length)
        { throw new IOException("Packet exceeds the WinDivert buffer size."); }
        try
        {
            if (_count == Capacity || packet.Length > _packets.Length - _length)
            { Flush(); }
        }
        finally
        {
            // A failed flush concerns the previous batch, not this new packet.
            // Keep it for the next send while still reporting the earlier error.
            packet.CopyTo(_packets.AsSpan(_length));
            _length += packet.Length;
            _addresses[_count++] = address;
        }
    }

    public void Flush()
    {
        if (_count == 0) { return; }
        var count = _count;
        var length = _length;
        // Injection is not transactional: a failed native call may have sent part
        // of the batch. Never retry it, including on the next Add/Flush.
        _count = _length = 0;
        send(_packets.AsSpan(0, length), _addresses.AsSpan(0, count));
    }

    /// <summary>Validate the entire packed receive before exposing any packet slices.</summary>
    internal static int ReadLengths(ReadOnlySpan<byte> packets, uint addressLength, Span<int> lengths)
    {
        if (addressLength == 0 || addressLength % AddressSize != 0 || addressLength / AddressSize > lengths.Length)
        { throw new IOException("Invalid WinDivert batch address length."); }
        var count = (int)(addressLength / AddressSize);
        for (var i = 0; i < count; i++)
        {
            var version = packets.IsEmpty ? 0 : packets[0] >> 4;
            var minimum = version == 4 ? 20 : version == 6 ? 40 : int.MaxValue;
            if (packets.Length < minimum)
            { throw new IOException("Truncated IP packet in WinDivert batch."); }
            var length = version == 4 ? BinaryPrimitives.ReadUInt16BigEndian(packets[2..])
                : 40 + BinaryPrimitives.ReadUInt16BigEndian(packets[4..]);
            if (length < minimum || length > packets.Length)
            { throw new IOException("Invalid IP packet length in WinDivert batch."); }
            lengths[i] = length;
            packets = packets[length..];
        }
        if (!packets.IsEmpty)
        { throw new IOException("WinDivert packet and address counts do not match."); }
        return count;
    }
}
