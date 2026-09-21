using System.Buffers.Binary;

namespace ServiceLib.Services.AppRouting;

internal static class RouteConnector
{
    public static Socket CreateInterfaceSocket(AppRouteRule rule, IPEndPoint destination, ProtocolType protocol)
    {
        var adapter = NetworkInterface.GetAllNetworkInterfaces().SingleOrDefault(a => a.Id == rule.InterfaceId && a.OperationalStatus == OperationalStatus.Up)
            ?? throw new IOException("Selected network interface is unavailable.");
        var family = destination.AddressFamily;
        var properties = adapter.GetIPProperties();
        var local = properties.UnicastAddresses.Select(a => a.Address).FirstOrDefault(a =>
            a.AddressFamily == family && !IPAddress.IsLoopback(a) &&
            (family != AddressFamily.InterNetworkV6 || a.IsIPv6LinkLocal == destination.Address.IsIPv6LinkLocal))
            ?? throw new IOException("Selected network interface has no address for this IP version.");
        var index = family == AddressFamily.InterNetwork ? properties.GetIPv4Properties().Index : properties.GetIPv6Properties().Index;
        var socket = new Socket(family, protocol == ProtocolType.Tcp ? SocketType.Stream : SocketType.Dgram, protocol);
        try
        {
            socket.SetSocketOption(family == AddressFamily.InterNetwork ? SocketOptionLevel.IP : SocketOptionLevel.IPv6,
                (SocketOptionName)31, family == AddressFamily.InterNetwork ? IPAddress.HostToNetworkOrder(index) : index);
            socket.Bind(new IPEndPoint(local, 0));
            if (destination.Address.IsIPv6LinkLocal)
            {
                destination.Address = new IPAddress(destination.Address.GetAddressBytes(), index);
            }

            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    public static async Task<Socket> ConnectTcp(AppRouteRule rule, IPEndPoint destination, CancellationToken token)
    {
        if (rule.Kind == AppRouteKind.Interface)
        {
            var socket = CreateInterfaceSocket(rule, destination, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(destination, token);
                return socket;
            }
            catch { socket.Dispose(); throw; }
        }
        var proxy = await ConnectProxy(rule, token);
        try
        {
            await Request(proxy, 1, destination, token);
            return proxy;
        }
        catch { proxy.Dispose(); throw; }
    }

    public static Task<Socket> ConnectProxy(AppRouteRule rule, CancellationToken token) =>
        ConnectProxy(rule, token, rule.Kind == AppRouteKind.ActiveProfile ? AppManager.Instance.GetLocalPort(EInboundProtocol.socks) : 0);

    internal static async Task<Socket> ConnectProxy(AppRouteRule rule, CancellationToken token, int mainSocksPort)
    {
        // Resolve for each new TCP connection / UDP association so changes to the
        // main listener are picked up without rewriting the saved application rule.
        var activeProfile = rule.Kind == AppRouteKind.ActiveProfile;
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(activeProfile ? Global.Loopback : rule.SocksHost, activeProfile ? mainSocksPort : rule.SocksPort, token);
            using var stream = new NetworkStream(socket, false);
            // Credentials on the separate LAN listener do not apply to the main local listener.
            var authentication = !activeProfile && !string.IsNullOrEmpty(rule.SocksUsername);
            await stream.WriteAsync(new byte[] { 5, 1, authentication ? (byte)2 : (byte)0 }, token);
            var reply = new byte[2];
            await stream.ReadExactlyAsync(reply, token);
            if (reply[0] != 5 || reply[1] != (authentication ? 2 : 0))
            {
                throw new IOException("SOCKS5 authentication method rejected.");
            }

            if (authentication)
            {
                var user = Encoding.UTF8.GetBytes(rule.SocksUsername);
                var password = Encoding.UTF8.GetBytes(rule.SocksPassword);
                if (user.Length is 0 or > 255 || password.Length is 0 or > 255)
                {
                    throw new IOException("SOCKS5 credentials must be 1–255 UTF-8 bytes.");
                }

                await stream.WriteAsync(new byte[] { 1, (byte)user.Length }.Concat(user).Concat(new byte[] { (byte)password.Length }).Concat(password).ToArray(), token);
                await stream.ReadExactlyAsync(reply, token);
                if (reply[0] != 1 || reply[1] != 0)
                {
                    throw new IOException("SOCKS5 authentication failed.");
                }
            }
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    public static async Task<EndPoint> Request(Socket socket, byte command, IPEndPoint destination, CancellationToken token)
    {
        using var stream = new NetworkStream(socket, false);
        var address = EncodeAddress(destination);
        await stream.WriteAsync(new byte[] { 5, command, 0 }.Concat(address).ToArray(), token);
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        if (header[0] != 5 || header[1] != 0 || header[2] != 0)
        {
            throw new IOException($"SOCKS5 request rejected ({header[1]}).");
        }

        var addressLength = header[3] switch
        {
            1 => 4,
            4 => 16,
            3 => 0,
            _ => throw new IOException("Invalid SOCKS5 address type.")
        };
        if (header[3] == 3)
        {
            var length = new byte[1];
            await stream.ReadExactlyAsync(length, token);
            addressLength = length[0];
            if (addressLength == 0)
            {
                throw new IOException("Invalid SOCKS5 domain length.");
            }
        }
        var raw = new byte[addressLength];
        await stream.ReadExactlyAsync(raw, token);
        var port = new byte[2];
        await stream.ReadExactlyAsync(port, token);
        var boundPort = BinaryPrimitives.ReadUInt16BigEndian(port);
        if (command == 3 && boundPort == 0)
        {
            throw new IOException("Invalid SOCKS5 UDP relay endpoint.");
        }
        // CONNECT only needs the reply consumed. Resolve a domain only when the
        // UDP socket actually connects to the returned relay endpoint.
        if (header[3] == 3)
        {
            return new DnsEndPoint(Encoding.ASCII.GetString(raw), boundPort);
        }
        var ip = new IPAddress(raw);
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            ip = ((IPEndPoint)socket.RemoteEndPoint!).Address;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        return new IPEndPoint(ip, boundPort);
    }

    public static byte[] EncodeAddress(IPEndPoint destination)
    {
        var result = new byte[EncodedAddressLength(destination)];
        WriteAddress(destination, result);
        return result;
    }

    private static int WriteAddress(IPEndPoint destination, Span<byte> result)
    {
        var ip = destination.Address.IsIPv4MappedToIPv6 ? destination.Address.MapToIPv4() : destination.Address;
        ip.TryWriteBytes(result[1..], out var length);
        result[0] = length == 4 ? (byte)1 : (byte)4;
        BinaryPrimitives.WriteUInt16BigEndian(result[(length + 1)..], (ushort)destination.Port);
        return length + 3;
    }

    // ATYP + address + port, shared by CONNECT/ASSOCIATE and UDP framing.
    private static int EncodedAddressLength(IPEndPoint destination) =>
        destination.Address.AddressFamily == AddressFamily.InterNetwork || destination.Address.IsIPv4MappedToIPv6 ? 7 : 19;

    internal static int DatagramHeaderLength(IPEndPoint destination) => 3 + EncodedAddressLength(destination); // RSV + FRAG

    public static byte[] WrapDatagram(IPEndPoint destination, ReadOnlySpan<byte> payload)
    {
        var result = new byte[DatagramHeaderLength(destination) + payload.Length];
        WriteDatagram(result, destination, payload);
        return result;
    }

    internal static int WriteDatagram(Span<byte> bytes, IPEndPoint destination, ReadOnlySpan<byte> payload)
    {
        bytes[..3].Clear();
        var header = 3 + WriteAddress(destination, bytes[3..]);
        payload.CopyTo(bytes[header..]);
        return header + payload.Length;
    }

    public static int UnwrapDatagram(ReadOnlySpan<byte> packet, IPEndPoint expected)
    {
        var offset = UnwrapDatagram(packet, out var peer);
        return peer?.Equals(expected) == true ? offset : -1;
    }

    public static int UnwrapDatagram(ReadOnlySpan<byte> packet, out IPEndPoint? peer)
    {
        peer = null;
        if (packet.Length < 4 || packet[0] != 0 || packet[1] != 0 || packet[2] != 0)
        {
            return -1;
        }

        var addressLength = packet[3] == 1 ? 4 : packet[3] == 4 ? 16 : 0;
        if (addressLength == 0 || packet.Length < addressLength + 6)
        {
            return -1;
        }

        var address = new IPAddress(packet.Slice(4, addressLength));
        var port = BinaryPrimitives.ReadUInt16BigEndian(packet[(4 + addressLength)..]);
        peer = new(address, port);
        return addressLength + 6;
    }
}
