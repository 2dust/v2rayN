using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServiceLib.Services.Udp;

public class Socks5UdpChannel(string socks5Host, int socks5TcpPort) : IDisposable
{
    private TcpClient tcpClient;
    private UdpClient udpClient;
    private IPEndPoint relayEndPoint;

    private bool _initialized = false;

    /// <summary>
    /// Send UDP data to a remote endpoint (IP address)
    /// </summary>
    public async Task SendAsync(IPEndPoint remote, byte[] data)
    {
        var addrData = new Socks5AddressData
        {
            AddressType = remote.Address.AddressFamily == AddressFamily.InterNetwork 
                ? Socks5AddressData.AddrTypeIPv4 
                : Socks5AddressData.AddrTypeIPv6,
            Host = remote.Address.ToString(),
            Port = (ushort)remote.Port
        };
        var packet = BuildSocks5UdpPacket(addrData, data);
        await udpClient.SendAsync(packet, packet.Length, relayEndPoint);
    }

    /// <summary>
    /// Send UDP data to a remote endpoint (domain name or IP address)
    /// </summary>
    /// <param name="host">Domain name or IP address</param>
    /// <param name="port">Port number</param>
    /// <param name="data">Data to send</param>
    public async Task SendAsync(string host, ushort port, byte[] data)
    {
        var addrData = new Socks5AddressData();
        
        // Try to parse as IP address first
        if (IPAddress.TryParse(host, out var ipAddr))
        {
            addrData.AddressType = ipAddr.AddressFamily == AddressFamily.InterNetwork 
                ? Socks5AddressData.AddrTypeIPv4 
                : Socks5AddressData.AddrTypeIPv6;
            addrData.Host = ipAddr.ToString();
        }
        else
        {
            // Treat as domain name
            addrData.AddressType = Socks5AddressData.AddrTypeDomain;
            addrData.Host = host;
        }
        addrData.Port = port;
        
        var packet = BuildSocks5UdpPacket(addrData, data);
        await udpClient.SendAsync(packet, packet.Length, relayEndPoint);
    }

    /// <summary>
    /// Receive UDP data from remote endpoint
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the receive operation</param>
    /// <returns>Remote endpoint information and received data</returns>
    public async Task<(Socks5RemoteEndpoint Remote, byte[] Data)> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        var (remote, payload) = ParseSocks5UdpPacket(result.Buffer);
        return (remote, payload);
    }

    /// <summary>
    /// Represents a remote endpoint that can be either an IP address or a domain name
    /// </summary>
    public class Socks5RemoteEndpoint(string host, ushort port, bool isDomain)
    {
        public string Host { get; set; } = host;
        public ushort Port { get; set; } = port;
        public bool IsDomain { get; set; } = isDomain;
    }

    private static byte[] BuildSocks5UdpPacket(Socks5AddressData addressData, byte[] data)
    {
        using var ms = new MemoryStream();
        
        // RSV (2 bytes) + FRAG (1 byte) - Reserved and Fragment fields
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // Write address (ATYP + address + port)
        ms.Write(addressData.ToBytes());

        // User data payload
        ms.Write(data);

        return ms.ToArray();
    }

    private static (Socks5RemoteEndpoint Remote, byte[] Data) ParseSocks5UdpPacket(byte[] packet)
    {
        if (packet.Length < 10) // Minimum length: RSV(2) + FRAG(1) + ATYP(1) + IPv4(4) + Port(2) = 10
        {
            throw new ArgumentException("Invalid SOCKS5 UDP packet: too short");
        }

        var offset = 0;

        // RSV (2 bytes) - Reserved field, skip
        offset += 2;

        // FRAG (1 byte) - Fragment number, currently only support 0 (no fragmentation)
        var frag = packet[offset++];
        if (frag != 0x00)
        {
            throw new NotSupportedException("SOCKS5 UDP fragmentation is not supported");
        }

        // ATYP (1 byte) - Address type
        var addressType = packet[offset++];

        string host;
        int addressLength;
        bool isDomain;

        switch (addressType)
        {
            case Socks5AddressData.AddrTypeIPv4:
                if (packet.Length < offset + 4)
                {
                    throw new ArgumentException("Invalid SOCKS5 UDP packet: IPv4 address incomplete");
                }
                var ipv4Bytes = new byte[4];
                Array.Copy(packet, offset, ipv4Bytes, 0, 4);
                host = new IPAddress(ipv4Bytes).ToString();
                addressLength = 4;
                isDomain = false;
                break;

            case Socks5AddressData.AddrTypeIPv6:
                if (packet.Length < offset + 16)
                {
                    throw new ArgumentException("Invalid SOCKS5 UDP packet: IPv6 address incomplete");
                }
                var ipv6Bytes = new byte[16];
                Array.Copy(packet, offset, ipv6Bytes, 0, 16);
                host = new IPAddress(ipv6Bytes).ToString();
                addressLength = 16;
                isDomain = false;
                break;

            case Socks5AddressData.AddrTypeDomain:
                if (packet.Length < offset + 1)
                {
                    throw new ArgumentException("Invalid SOCKS5 UDP packet: domain length missing");
                }
                var domainLength = packet[offset++];
                if (packet.Length < offset + domainLength)
                {
                    throw new ArgumentException("Invalid SOCKS5 UDP packet: domain incomplete");
                }
                host = Encoding.ASCII.GetString(packet, offset, domainLength);
                addressLength = domainLength;
                isDomain = true;
                break;

            default:
                throw new NotSupportedException($"Unsupported SOCKS5 address type: {addressType}");
        }

        offset += addressLength;

        // Port (2 bytes, big-endian)
        if (packet.Length < offset + 2)
        {
            throw new ArgumentException("Invalid SOCKS5 UDP packet: port incomplete");
        }
        var port = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(offset, 2));
        offset += 2;

        // Data (remaining bytes)
        var dataLength = packet.Length - offset;
        var data = new byte[dataLength];
        if (dataLength > 0)
        {
            Array.Copy(packet, offset, data, 0, dataLength);
        }

        // Create remote endpoint without DNS resolution
        var remote = new Socks5RemoteEndpoint(host, port, isDomain);
        return (remote, data);
    }

    public void Dispose()
    {
        tcpClient.Dispose();
        udpClient.Dispose();
    }

    #region SOCKS5 Connection Handling

    private const byte Socks5Version = 0x05;
    private const byte SocksCmdUdpAssociate = 0x03;

    public async Task<bool> EstablishUdpAssociationAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            Dispose();
            _initialized = false;
        }
        udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        var clientListenEp = (IPEndPoint)udpClient.Client.LocalEndPoint!;

        tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(socks5Host, socks5TcpPort, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return false;
        }
        var tcpControlStream = tcpClient.GetStream();

        byte[] handshakeRequest = { Socks5Version, 0x01, 0x00 };
        await tcpControlStream.WriteAsync(handshakeRequest, cancellationToken).ConfigureAwait(false);
        var handshakeResponse = new byte[2];
        if (await tcpControlStream.ReadAsync(handshakeResponse, cancellationToken).ConfigureAwait(false) < 2 ||
            handshakeResponse[0] != Socks5Version || handshakeResponse[1] != 0x00)
        {
            return false;
        }

        var clientListenIp = clientListenEp.Address;
        var clientAddrForSocks = new Socks5AddressData();
        if (clientListenIp.Equals(IPAddress.Any))
        {
            clientAddrForSocks.AddressType = Socks5AddressData.AddrTypeIPv4;
            clientAddrForSocks.Host = "0.0.0.0";
        }
        else if (clientListenIp.Equals(IPAddress.IPv6Any))
        {
            clientAddrForSocks.AddressType = Socks5AddressData.AddrTypeIPv6;
            clientAddrForSocks.Host = "::";
        }
        else if (clientListenIp.IsIPv4MappedToIPv6)
        {
            clientAddrForSocks.AddressType = Socks5AddressData.AddrTypeIPv4;
            clientAddrForSocks.Host = clientListenIp.MapToIPv4().ToString();
        }
        else if (clientListenIp.AddressFamily == AddressFamily.InterNetwork)
        {
            clientAddrForSocks.AddressType = Socks5AddressData.AddrTypeIPv4;
            clientAddrForSocks.Host = clientListenIp.ToString();
        }
        else if (clientListenIp.AddressFamily == AddressFamily.InterNetworkV6)
        {
            clientAddrForSocks.AddressType = Socks5AddressData.AddrTypeIPv6;
            clientAddrForSocks.Host = clientListenIp.ToString();
        }
        else
        {
            return false;
        }
        clientAddrForSocks.Port = (ushort)clientListenEp.Port;

        using var udpAssociateReqMs = new MemoryStream();
        udpAssociateReqMs.WriteByte(Socks5Version);
        udpAssociateReqMs.WriteByte(SocksCmdUdpAssociate);
        udpAssociateReqMs.WriteByte(0x00);
        udpAssociateReqMs.Write(clientAddrForSocks.ToBytes());
        await tcpControlStream.WriteAsync(udpAssociateReqMs.ToArray(), cancellationToken).ConfigureAwait(false);

        var verRepRsv = new byte[3];
        if (await tcpControlStream.ReadAsync(verRepRsv, cancellationToken).ConfigureAwait(false) < 3 ||
            verRepRsv[0] != Socks5Version || verRepRsv[1] != 0x00)
        {
            return false;
        }

        var proxyRelaySocksAddr = await Socks5AddressData.ParseAsync(tcpControlStream, cancellationToken).ConfigureAwait(false);
        if (proxyRelaySocksAddr == null || !IPAddress.TryParse(proxyRelaySocksAddr.Host, out var proxyRelayIp))
        {
            return false;
        }

        relayEndPoint = new IPEndPoint(proxyRelayIp, proxyRelaySocksAddr.Port);
        _initialized = true;
        return true;
    }
    #endregion

    #region SOCKS5 Address Handling
    private class Socks5AddressData
    {
        public const byte AddrTypeIPv4 = 0x01;
        public const byte AddrTypeDomain = 0x03;
        public const byte AddrTypeIPv6 = 0x04;

        public byte AddressType { get; set; }
        public string Host { get; set; } = string.Empty;
        public ushort Port { get; set; }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            ms.WriteByte(AddressType);
            switch (AddressType)
            {
                case AddrTypeIPv4:
                    if (IPAddress.TryParse(Host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ms.Write(ip.GetAddressBytes(), 0, 4);
                    }
                    else
                    {
                        ms.Write(new byte[] { 0, 0, 0, 0 });
                    }
                    break;
                case AddrTypeDomain:
                    if (string.IsNullOrEmpty(Host))
                    {
                        ms.WriteByte(0);
                    }
                    else
                    {
                        var domainBytes = Encoding.ASCII.GetBytes(Host);
                        ms.WriteByte((byte)domainBytes.Length);
                        ms.Write(domainBytes);
                    }
                    break;
                case AddrTypeIPv6:
                    if (IPAddress.TryParse(Host, out var ip6) && ip6.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        ms.Write(ip6.GetAddressBytes(), 0, 16);
                    }
                    else
                    {
                        ms.Write(new byte[16]);
                    }
                    break;
                default:
                    throw new NotSupportedException($"SOCKS5 address type {AddressType} not supported.");
            }
            var portBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(portBytes, Port);
            ms.Write(portBytes);
            return ms.ToArray();
        }

        public static async Task<Socks5AddressData?> ParseAsync(Stream stream, CancellationToken ct)
        {
            var addr = new Socks5AddressData();
            var typeByte = new byte[1];
            try
            {
                if (await stream.ReadAsync(typeByte.AsMemory(0, 1), ct).ConfigureAwait(false) < 1)
                {
                    return null;
                }
                addr.AddressType = typeByte[0];
                switch (addr.AddressType)
                {
                    case AddrTypeIPv4:
                        var ipv4Bytes = new byte[4];
                        if (await stream.ReadAsync(ipv4Bytes.AsMemory(0, 4), ct).ConfigureAwait(false) < 4)
                        {
                            return null;
                        }
                        addr.Host = new IPAddress(ipv4Bytes).ToString();
                        break;
                    case AddrTypeDomain:
                        var lenByte = new byte[1];
                        if (await stream.ReadAsync(lenByte.AsMemory(0, 1), ct).ConfigureAwait(false) < 1)
                        {
                            return null;
                        }
                        if (lenByte[0] == 0)
                        {
                            addr.Host = string.Empty;
                        }
                        else
                        {
                            var domainBytes = new byte[lenByte[0]];
                            if (await stream.ReadAsync(domainBytes.AsMemory(0, domainBytes.Length), ct).ConfigureAwait(false) < domainBytes.Length)
                            {
                                return null;
                            }
                            addr.Host = Encoding.ASCII.GetString(domainBytes);
                        }
                        break;
                    case AddrTypeIPv6:
                        var ipv6Bytes = new byte[16];
                        if (await stream.ReadAsync(ipv6Bytes.AsMemory(0, 16), ct).ConfigureAwait(false) < 16)
                        {
                            return null;
                        }
                        addr.Host = new IPAddress(ipv6Bytes).ToString();
                        break;
                    default:
                        return null;
                }
                var portBytes = new byte[2];
                if (await stream.ReadAsync(portBytes.AsMemory(0, 2), ct).ConfigureAwait(false) < 2)
                {
                    return null;
                }
                addr.Port = BinaryPrimitives.ReadUInt16BigEndian(portBytes);
                return addr;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                return null;
            }
        }
    }
    #endregion SOCKS5 Address Handling
}
