namespace ServiceLib.Services.AppRouting;

/// <summary>Short, bounded attribution wait. The engine serializes access with packet processing.</summary>
internal sealed class RoutePendingPackets
{
    internal sealed record Packet(byte[] Bytes, DivertAddress Address, long Arrived,
        List<(byte[] Packet, DivertAddress Address)>? Fragments)
    {
        // Completed fragment lists are retained unchanged until this packet leaves the queue.
        public int Size { get; } = Bytes.Length + (Fragments?.Sum(f => f.Packet.Length) ?? 0);
    }
    private readonly Queue<Packet> _packets = new();
    private int _bytes;
    public const int WaitMilliseconds = 250;
    public int Count => _packets.Count;

    public bool Add(ReadOnlySpan<byte> bytes, DivertAddress address, long arrived,
        List<(byte[] Packet, DivertAddress Address)>? fragments)
    {
        var size = bytes.Length + (fragments?.Sum(f => f.Packet.Length) ?? 0);
        if (!HasCapacity(size)) { return false; }
        Enqueue(new(bytes.ToArray(), address, arrived, fragments));
        return true;
    }

    // Retries already own their storage; preserve both it and the original deadline.
    public bool Add(Packet packet)
    {
        if (!HasCapacity(packet.Size)) { return false; }
        Enqueue(packet);
        return true;
    }

    public Packet Dequeue()
    {
        var packet = _packets.Dequeue();
        _bytes -= packet.Size;
        return packet;
    }

    private bool HasCapacity(int size) => Count < 512 && _bytes + size <= 4 * 1024 * 1024;

    private void Enqueue(Packet packet)
    {
        _packets.Enqueue(packet);
        _bytes += packet.Size;
    }
}
