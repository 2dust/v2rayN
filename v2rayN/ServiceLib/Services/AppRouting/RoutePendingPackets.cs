namespace ServiceLib.Services.AppRouting;

/// <summary>Short, bounded attribution wait. The engine serializes access with packet processing.</summary>
internal sealed class RoutePendingPackets
{
    internal sealed record Packet(byte[] Bytes, DivertAddress Address, long Arrived,
        List<(byte[] Packet, DivertAddress Address)>? Fragments);
    private readonly Queue<Packet> _packets = new();
    private int _bytes;
    public const int WaitMilliseconds = 250;

    public bool Add(ReadOnlySpan<byte> bytes, DivertAddress address, long arrived,
        List<(byte[] Packet, DivertAddress Address)>? fragments)
    {
        var size = bytes.Length + (fragments?.Sum(f => f.Packet.Length) ?? 0);
        if (_packets.Count >= 512 || _bytes + size > 4 * 1024 * 1024) { return false; }
        _packets.Enqueue(new(bytes.ToArray(), address, arrived, fragments));
        _bytes += size;
        return true;
    }

    public List<Packet> Drain()
    {
        var packets = _packets.ToList();
        _packets.Clear();
        _bytes = 0;
        return packets;
    }
}
