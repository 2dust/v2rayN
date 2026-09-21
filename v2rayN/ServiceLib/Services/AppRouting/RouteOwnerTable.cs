using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

internal static class RouteOwnerTable
{
    internal sealed record Row(IPAddress Local, ushort Port, IPAddress? Remote, ushort RemotePort, int Pid);

    [SupportedOSPlatform("windows")]
    internal static List<Row> Read(byte protocol, AddressFamily family)
    {
        uint size = 0;
        var tcp = protocol == 6;
        uint Query(IntPtr buffer) => tcp
            ? GetExtendedTcpTable(buffer, ref size, false, (uint)family, 5, 0)
            : GetExtendedUdpTable(buffer, ref size, false, (uint)family, 1, 0);
        var error = Query(IntPtr.Zero);
        if (error is not (0 or 122))
        {
            throw new Win32Exception((int)error);
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var capacity = checked((int)size);
            var ptr = Marshal.AllocHGlobal(capacity);
            try
            {
                error = Query(ptr);
                if (error == 122)
                {
                    continue;
                }

                if (error != 0)
                {
                    throw new Win32Exception((int)error);
                }

                var data = new byte[capacity];
                Marshal.Copy(ptr, data, 0, capacity);
                var ipv6 = family == AddressFamily.InterNetworkV6;
                var rowSize = tcp ? (ipv6 ? 56 : 24) : (ipv6 ? 28 : 12);
                var count = BinaryPrimitives.ReadUInt32LittleEndian(data);
                if (count > (capacity - 4) / rowSize)
                {
                    throw new IOException("Invalid IP owner table.");
                }

                var rows = new List<Row>();
                for (var i = 0; i < count; i++)
                {
                    var row = data.AsSpan(4 + i * rowSize, rowSize);
                    var localOffset = tcp && !ipv6 ? 4 : 0;
                    var localPortOffset = ipv6 ? 20 : tcp ? 8 : 4;
                    var remoteOffset = ipv6 ? 24 : 12;
                    var remotePortOffset = ipv6 ? 44 : 16;
                    rows.Add(new(ipv6 ? new IPAddress(row[..16], BinaryPrimitives.ReadUInt32LittleEndian(row[16..])) : new IPAddress(row.Slice(localOffset, 4)),
                        BinaryPrimitives.ReadUInt16BigEndian(row[localPortOffset..]),
                        tcp ? ipv6 ? new IPAddress(row.Slice(remoteOffset, 16), BinaryPrimitives.ReadUInt32LittleEndian(row[40..])) : new IPAddress(row.Slice(remoteOffset, 4)) : null,
                        tcp ? BinaryPrimitives.ReadUInt16BigEndian(row[remotePortOffset..]) : (ushort)0,
                        BinaryPrimitives.ReadInt32LittleEndian(row[(rowSize - 4)..])));
                }
                return rows;
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        throw new IOException("IP owner table changed repeatedly.");
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref uint size, bool order, uint family, uint tableClass, uint reserved);
    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedUdpTable(IntPtr table, ref uint size, bool order, uint family, uint tableClass, uint reserved);
}
