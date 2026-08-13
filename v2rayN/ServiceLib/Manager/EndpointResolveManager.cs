using System.Net.Http.Headers;

namespace ServiceLib.Manager;

public class EndpointResolveManager
{
    public static IDnsResolver BuildDnsClient(string dnsServer)
    {
        if (string.IsNullOrEmpty(dnsServer))
        {
            throw new ArgumentNullException(nameof(dnsServer));
        }

        if (dnsServer is "local" or "localhost" or "system" or "dhcp" ||
            dnsServer.StartsWith("127.0.0.1") ||
            dnsServer.StartsWith("::1"))
        {
            return new SystemDnsResolver();
        }

        var (domain, scheme, port, path) = Utils.ParseUrl(dnsServer);

        if (string.IsNullOrEmpty(scheme))
        {
            return new UdpDnsResolver(new IPEndPoint(IPAddress.Parse(domain), 53));
        }

        var processedScheme = scheme.ToLowerInvariant().Replace("+local", "");

        switch (processedScheme)
        {
            case "udp":
                return new UdpDnsResolver(new IPEndPoint(IPAddress.Parse(domain), port != 0 ? port : 53));

            case "tcp":
                return new TcpDnsResolver(new IPEndPoint(IPAddress.Parse(domain), port != 0 ? port : 53));

            case "doh" or "https":
                {
                    var uri = new Uri($"https://{domain}:{(port != 0 ? port : 443)}{path}");
                    var httpClient = new HttpClient();
                    return new DohDnsResolver(httpClient, uri);
                }

            default:
                throw new NotSupportedException($"Unsupported DNS scheme: {scheme}");
        }
    }

    public class DnsWire
    {
        #region Build DNS Query

        public static byte[] BuildQuery(string host, ushort queryType)
        {
            if (queryType is not (1 or 28))
            {
                throw new ArgumentOutOfRangeException(nameof(queryType));
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host is empty.", nameof(host));
            }

            host = host.TrimEnd('.');

            if (host.Length == 0)
            {
                host = ".";
            }

            using var stream = new MemoryStream();

            // Header
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0x0100);
            WriteUInt16(stream, 1);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0);

            // Question: QNAME
            if (host != ".")
            {
                var labels = host.Split('.');
                foreach (var label in labels)
                {
                    if (label.Length is < 1 or > 63)
                    {
                        throw new ArgumentException($"Invalid DNS label length: {label.Length}.", nameof(host));
                    }

                    foreach (var c in label)
                    {
                        if (c > 0x7F)
                        {
                            throw new ArgumentException("Non-ASCII domain names must be converted to A-labels first.",
                                nameof(host));
                        }
                    }

                    stream.WriteByte((byte)label.Length);

                    var labelBytes = Encoding.ASCII.GetBytes(label);
                    stream.Write(labelBytes, 0, labelBytes.Length);
                }
            }

            stream.WriteByte(0);

            // Question: QTYPE & QCLASS
            WriteUInt16(stream, queryType);
            WriteUInt16(stream, 1);

            return stream.ToArray();
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value & 0xFF));
        }

        #endregion

        #region Parse DNS Response

        public static List<IPAddress> ParseAddresses(
            ReadOnlySpan<byte> message,
            ushort expectedType)
        {
            if (expectedType is not (1 or 28))
            {
                throw new ArgumentOutOfRangeException(nameof(expectedType));
            }

            if (message.Length < 12)
            {
                throw new InvalidDataException("DNS message is too short.");
            }

            var flags = ReadUInt16(message, 2);
            var questionCount = ReadUInt16(message, 4);
            var answerCount = ReadUInt16(message, 6);

            // QR = 1
            if ((flags & 0x8000) == 0)
            {
                throw new InvalidDataException("Not a DNS response.");
            }

            // RCODE
            var rcode = flags & 0x000F;
            if (rcode != 0)
            {
                return [];
            }

            var offset = 12;

            // Skip Question section.
            for (var i = 0; i < questionCount; i++)
            {
                offset = SkipName(message, offset);

                if (offset + 4 > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS question.");
                }

                offset += 4; // QTYPE + QCLASS
            }

            var addresses = new List<IPAddress>();

            for (var i = 0; i < answerCount; i++)
            {
                // NAME
                offset = SkipName(message, offset);

                if (offset + 10 > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS answer.");
                }

                var type = ReadUInt16(message, offset);
                var @class = ReadUInt16(message, offset + 2);
                // TTL
                var rdLength = ReadUInt16(message, offset + 8);

                offset += 10;

                if (offset + rdLength > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS RDATA.");
                }

                if (@class == 1 && type == expectedType)
                {
                    if (expectedType == 1 && rdLength == 4)
                    {
                        addresses.Add(new IPAddress(
                            message.Slice(offset, 4)));
                    }
                    else if (expectedType == 28 && rdLength == 16)
                    {
                        addresses.Add(new IPAddress(
                            message.Slice(offset, 16)));
                    }
                }

                offset += rdLength;
            }

            return addresses;
        }

        private static int SkipName(ReadOnlySpan<byte> message, int offset)
        {
            while (true)
            {
                if ((uint)offset >= (uint)message.Length)
                {
                    throw new InvalidDataException("Invalid DNS name.");
                }

                var length = message[offset++];

                // End of name.
                if (length == 0)
                {
                    return offset;
                }

                // Compression pointer.
                if ((length & 0xC0) == 0xC0)
                {
                    if (offset >= message.Length)
                    {
                        throw new InvalidDataException("Invalid DNS pointer.");
                    }

                    return offset + 1;
                }

                // Reserved label types.
                if ((length & 0xC0) != 0)
                {
                    throw new InvalidDataException("Invalid DNS label.");
                }

                if (offset + length > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS label length.");
                }

                offset += length;
            }
        }

        private static ushort ReadUInt16(
            ReadOnlySpan<byte> data,
            int offset)
        {
            return (ushort)(
                (data[offset] << 8) |
                data[offset + 1]);
        }

        #endregion
    }

    public interface IDnsResolver
    {
        public Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default);

        public async Task<List<IPAddress>> ResolveIpv46Async(
            string host,
            CancellationToken cancellationToken = default)
        {
            var ipv4Task = ResolveAsync(host, false, cancellationToken);
            var ipv6Task = ResolveAsync(host, true, cancellationToken);
            var results = await Task.WhenAll(ipv4Task, ipv6Task);
            return results.SelectMany(x => x).ToList();
        }
    }

    public class UdpDnsResolver(IPEndPoint dnsServer) : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            var queryType = ipv6 ? (ushort)28 : (ushort)1;
            var query = DnsWire.BuildQuery(host, queryType);
            using var udpClient = new UdpClient();
            udpClient.Connect(dnsServer);
            await udpClient.SendAsync(query, query.Length);
            var result = await udpClient.ReceiveAsync(cancellationToken);
            return DnsWire.ParseAddresses(result.Buffer, queryType);
        }
    }

    public class TcpDnsResolver(IPEndPoint dnsServer) : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            var queryType = ipv6 ? (ushort)28 : (ushort)1;
            var query = DnsWire.BuildQuery(host, queryType);
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(dnsServer.Address, dnsServer.Port, cancellationToken);
            await using var networkStream = tcpClient.GetStream();
            await networkStream.WriteAsync(query, 0, query.Length, cancellationToken);
            // Read the length of the response (2 bytes)
            var lengthBuffer = new byte[2];
            await networkStream.ReadExactlyAsync(lengthBuffer, 0, 2, cancellationToken);
            var responseLength = (lengthBuffer[0] << 8) | lengthBuffer[1];
            // Read the actual response
            var responseBuffer = new byte[responseLength];
            await networkStream.ReadExactlyAsync(responseBuffer, 0, responseLength, cancellationToken);
            return DnsWire.ParseAddresses(responseBuffer, queryType);
        }
    }

    public class DohDnsResolver(HttpClient httpClient, Uri dohServer) : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            var queryType = ipv6 ? (ushort)28 : (ushort)1;
            var query = DnsWire.BuildQuery(host, queryType);

            using var request = new HttpRequestMessage(HttpMethod.Post, dohServer);
            request.Content = new ByteArrayContent(query)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/dns-message"),
                },
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));

            using var response =
                await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return DnsWire.ParseAddresses(responseData, queryType);
        }
    }

    public class SystemDnsResolver : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.Where(ip => (!ipv6 && ip.AddressFamily == AddressFamily.InterNetwork) ||
                                         (ipv6 && ip.AddressFamily == AddressFamily.InterNetworkV6))
                .ToList();
        }

        public async Task<List<IPAddress>> ResolveIpv46Async(
            string host,
            CancellationToken cancellationToken = default)
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.ToList();
        }
    }
}
