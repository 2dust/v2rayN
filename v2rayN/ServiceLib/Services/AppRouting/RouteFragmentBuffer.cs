using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

/// <summary>Bypasses known unselected fragments and reassembles selected/unknown traffic.
/// The engine serializes access with packet processing and policy updates.</summary>
internal sealed class RouteFragmentBuffer(Func<RouteFlow, RouteDecisionKind>? classify = null)
{
    internal sealed record Batch(byte[] Packet, DivertAddress Address, List<(byte[] Packet, DivertAddress Address)> Originals, bool PassThrough = false);
    private sealed record Key(IPAddress Source, IPAddress Destination, uint Id, byte Protocol, uint Interface);
    private sealed class Assembly
    {
        public long Created = Environment.TickCount64;
        public byte[]? Prefix;
        public DivertAddress Address;
        public int? Length;
        public int Received;
        public bool Rejected;
        public SortedDictionary<int, byte[]> Parts = [];
        public List<(byte[] Packet, DivertAddress Address)> Originals = [];
    }
    private readonly Dictionary<Key, Assembly> _pending = [];
    private readonly Dictionary<Key, long> _bypass = [];
    private int _buffered;

    // False means an ordinary unfragmented packet. True with null batch means buffered.
    public bool Add(ReadOnlySpan<byte> packet, DivertAddress address, out Batch? batch)
    {
        batch = null;
        if (packet.Length < 20)
        {
            return false;
        }

        var six = packet[0] >> 4 == 6;
        int offset, start, prefixLength, previousNext = 6, total;
        uint id;
        byte protocol;
        bool more;
        if (packet[0] >> 4 == 4)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(packet[6..]);
            if ((flags & 0x3fff) == 0)
            {
                return false;
            }

            start = prefixLength = (packet[0] & 15) * 4;
            total = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
            offset = (flags & 0x1fff) * 8;
            more = (flags & 0x2000) != 0;
            id = BinaryPrimitives.ReadUInt16BigEndian(packet[4..]);
            protocol = packet[9];
        }
        else if (six)
        {
            if (packet.Length < 40)
            {
                return false;
            }

            total = 40 + BinaryPrimitives.ReadUInt16BigEndian(packet[4..]);
            protocol = packet[6];
            start = 40;
            for (var i = 0; protocol is 0 or 43 or 60; i++)
            {
                if (i >= 8 || start + 2 > packet.Length)
                {
                    throw new IOException("Invalid IPv6 extension header.");
                }

                previousNext = start;
                protocol = packet[start];
                start += (packet[start + 1] + 1) * 8;
            }
            if (protocol != 44)
            {
                return false;
            }

            if (start + 8 > packet.Length)
            {
                throw new IOException("Truncated IPv6 fragment header.");
            }

            var flags = BinaryPrimitives.ReadUInt16BigEndian(packet[(start + 2)..]);
            offset = flags & 0xfff8;
            more = (flags & 1) != 0;
            id = BinaryPrimitives.ReadUInt32BigEndian(packet[(start + 4)..]);
            protocol = packet[start];
            prefixLength = start;
            start += 8;
        }
        else
        {
            return false;
        }

        if (protocol is not (6 or 17)) { return false; }
        if (prefixLength < 20 || total > packet.Length || start >= total ||
            more && (total - start) % 8 != 0 || offset + total - start > 65535)
        {
            throw new IOException("Invalid IP fragment length.");
        }

        packet = packet[..total];
        var key = new Key(new(packet.Slice(six ? 8 : 12, six ? 16 : 4)),
            new(packet.Slice(six ? 24 : 16, six ? 16 : 4)), id, protocol, address.InterfaceIndex);
        Expire(Environment.TickCount64);
        if (offset == 0 && total - start >= 4 && classify != null)
        {
            _bypass.Remove(key);
            IPAddress Scoped(IPAddress ip) => ip.IsIPv6LinkLocal ? new(ip.GetAddressBytes(), address.InterfaceIndex) : ip;
            var flow = new RouteFlow(protocol, Scoped(key.Source), BinaryPrimitives.ReadUInt16BigEndian(packet[start..]),
                Scoped(key.Destination), BinaryPrimitives.ReadUInt16BigEndian(packet[(start + 2)..]));
            // A fragmented TCP SYN still needs the normal fresh ownership check.
            // A tiny first fragment without the flags cannot prove it is not a SYN.
            var canBypass = protocol == 17 || total - start >= 14 && (packet[start + 13] & 2) == 0;
            if (canBypass && classify(flow) == RouteDecisionKind.Unselected)
            {
                var originals = new List<(byte[] Packet, DivertAddress Address)>();
                if (_pending.Remove(key, out var buffered))
                {
                    originals.AddRange(buffered.Originals);
                    _buffered -= buffered.Originals.Sum(p => p.Packet.Length) + buffered.Received;
                }
                if (_bypass.Count >= 4096) { _bypass.Remove(_bypass.Keys.First()); }
                _bypass[key] = Environment.TickCount64;
                originals.Add((packet.ToArray(), address));
                batch = new([], address, originals, true);
                return true;
            }
        }
        else if (_bypass.ContainsKey(key))
        {
            batch = new([], address, [(packet.ToArray(), address)], true);
            return true;
        }
        if (!_pending.TryGetValue(key, out var assembly))
        {
            if (_pending.Count >= 256)
            {
                throw new IOException("IP fragment assembly limit reached.");
            }

            _pending[key] = assembly = new();
        }
        if (assembly.Rejected)
        {
            return true;
        }

        var payload = packet[start..];
        foreach (var part in assembly.Parts)
        {
            if (part.Key == offset && payload.SequenceEqual(part.Value))
            {
                return true;
            }

            if (part.Key < offset + payload.Length && offset < part.Key + part.Value.Length)
            {
                Reject(assembly);
                throw new IOException("Overlapping IP fragments blocked.");
            }
        }
        if (_buffered + packet.Length + payload.Length > 16 * 1024 * 1024 || assembly.Parts.Count >= 1024)
        {
            Reject(assembly);
            throw new IOException("IP fragment buffer limit reached.");
        }
        if (!more)
        {
            assembly.Length = offset + payload.Length;
        }

        if (assembly.Length is int length && (offset + payload.Length > length ||
            assembly.Parts.Any(p => p.Key + p.Value.Length > length)))
        {
            Reject(assembly);
            throw new IOException("Inconsistent IP fragment length.");
        }
        assembly.Parts.Add(offset, payload.ToArray());
        assembly.Originals.Add((packet.ToArray(), address));
        assembly.Received += payload.Length;
        _buffered += packet.Length + payload.Length;
        if (offset == 0)
        {
            assembly.Prefix = packet[..prefixLength].ToArray();
            assembly.Address = address;
            if (six)
            {
                assembly.Prefix[previousNext] = protocol;
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(assembly.Prefix.AsSpan(6), 0);
            }
        }
        if (assembly.Prefix == null || assembly.Length != assembly.Received)
        {
            return true;
        }

        var header = assembly.Prefix;
        var resultLength = header.Length + assembly.Received;
        if (resultLength > (six ? 65575 : 65535))
        {
            Reject(assembly);
            throw new IOException("Reassembled IP packet is too large.");
        }
        var result = new byte[resultLength];
        header.CopyTo(result, 0);
        foreach (var part in assembly.Parts)
        {
            part.Value.CopyTo(result, header.Length + part.Key);
        }

        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(six ? 4 : 2), (ushort)(six ? resultLength - 40 : resultLength));
        batch = new(result, assembly.Address, assembly.Originals);
        Remove(key, assembly);
        return true;
    }

    internal void Expire(long now)
    {
        foreach (var key in _bypass.Where(p => now - p.Value > 15_000).Select(p => p.Key).ToArray()) { _bypass.Remove(key); }
        foreach (var pair in _pending.Where(p => now - p.Value.Created > 15_000).ToArray())
        {
            Remove(pair.Key, pair.Value);
        }
    }

    public void ClearDecisions() => _bypass.Clear();

    private void Reject(Assembly assembly)
    {
        _buffered -= assembly.Originals.Sum(p => p.Packet.Length) + assembly.Received;
        assembly.Originals.Clear();
        assembly.Parts.Clear();
        assembly.Received = 0;
        assembly.Prefix = null;
        assembly.Rejected = true;
    }

    private void Remove(Key key, Assembly assembly)
    {
        _pending.Remove(key);
        _buffered -= assembly.Originals.Sum(p => p.Packet.Length) + assembly.Received;
    }
}
