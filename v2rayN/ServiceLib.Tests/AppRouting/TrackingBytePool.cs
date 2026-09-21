using System.Buffers;

namespace ServiceLib.Tests.AppRouting;

// Oversized, dirty buffers expose missing length limits and uninitialized headers.
internal sealed class TrackingBytePool : ArrayPool<byte>
{
    private readonly HashSet<byte[]> _outstanding = [];
    public int Rents { get; private set; }
    public int Outstanding { get { lock (_outstanding) { return _outstanding.Count; } } }
    public override byte[] Rent(int minimumLength)
    {
        var bytes = new byte[minimumLength + 17];
        bytes.AsSpan().Fill(0xa5);
        lock (_outstanding) { Rents++; _outstanding.Add(bytes); }
        return bytes;
    }
    public override void Return(byte[] array, bool clearArray = false)
    {
        lock (_outstanding)
        {
            if (!_outstanding.Remove(array)) { throw new InvalidOperationException("Buffer returned more than once."); }
            array.AsSpan().Fill(0xdd);
        }
    }
}
