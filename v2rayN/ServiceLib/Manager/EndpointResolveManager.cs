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
            return ParseResponse(message, expectedType).Addresses;
        }

        public static DnsResponse ParseResponse(
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
                return new DnsResponse([], null);
            }

            var offset = 12;

            // Parse the Question section to obtain the query name.
            string? queryName = null;
            for (var i = 0; i < questionCount; i++)
            {
                var name = ReadName(message, offset, out offset);
                queryName ??= NormalizeName(name);

                if (offset + 4 > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS question.");
                }

                offset += 4; // QTYPE + QCLASS
            }

            var addresses = new List<IPAddress>();
            string? cname = null;

            for (var i = 0; i < answerCount; i++)
            {
                // NAME
                var ownerName = ReadName(message, offset, out offset);

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

                if (@class != 1)
                {
                    offset += rdLength;
                    continue;
                }

                var ownerMatchesQuery = NamesEqual(ownerName, queryName);
                var ownerMatchesCname = NamesEqual(ownerName, cname);

                if (type == expectedType)
                {
                    if (ownerMatchesQuery || ownerMatchesCname)
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
                }
                else if (type == 5 && ownerMatchesQuery && cname is null)
                {
                    cname = ReadName(message, offset, out _);
                }

                offset += rdLength;
            }

            return new DnsResponse(addresses, cname);
        }

        public static async Task<List<IPAddress>> ResolveWithCnameAsync(
            string host,
            bool ipv6,
            Func<string, ushort, CancellationToken, Task<byte[]>> queryAsync,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host is empty.", nameof(host));
            }

            var queryType = ipv6 ? (ushort)28 : (ushort)1;
            var currentName = host.TrimEnd('.');
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const int maxHops = 8;

            for (var hop = 0; hop < maxHops; hop++)
            {
                if (!visited.Add(currentName))
                {
                    // CNAME loop detected.
                    return [];
                }

                var response = await queryAsync(currentName, queryType, cancellationToken);
                var result = ParseResponse(response, queryType);

                if (result.Addresses.Count > 0)
                {
                    return result.Addresses;
                }

                if (string.IsNullOrWhiteSpace(result.Cname))
                {
                    return [];
                }

                currentName = result.Cname.TrimEnd('.');
            }

            return [];
        }

        public static string ReadName(
            ReadOnlySpan<byte> message,
            int offset,
            out int nextOffset)
        {
            var name = new StringBuilder();
            var position = offset;
            var endOffset = -1;
            var pointerCount = 0;

            while (true)
            {
                if ((uint)position >= (uint)message.Length)
                {
                    throw new InvalidDataException("Invalid DNS name.");
                }

                var length = message[position];

                // End of name.
                if (length == 0)
                {
                    position++;
                    if (endOffset < 0)
                    {
                        endOffset = position;
                    }
                    break;
                }

                // Compression pointer.
                if ((length & 0xC0) == 0xC0)
                {
                    if (position + 1 >= message.Length)
                    {
                        throw new InvalidDataException("Invalid DNS pointer.");
                    }

                    if (++pointerCount > 64)
                    {
                        throw new InvalidDataException("Too many DNS compression pointers.");
                    }

                    if (endOffset < 0)
                    {
                        endOffset = position + 2;
                    }

                    var pointer = ((length & 0x3F) << 8) | message[position + 1];
                    position = pointer;
                    continue;
                }

                // Reserved label types.
                if ((length & 0xC0) != 0)
                {
                    throw new InvalidDataException("Invalid DNS label.");
                }

                position++;
                if (position + length > message.Length)
                {
                    throw new InvalidDataException("Invalid DNS label length.");
                }

                if (name.Length > 0)
                {
                    name.Append('.');
                }

                name.Append(Encoding.ASCII.GetString(message.Slice(position, length)));
                position += length;
            }

            nextOffset = endOffset;
            return name.ToString();
        }

        public sealed record DnsResponse(List<IPAddress> Addresses, string? Cname);

        private static string? NormalizeName(string name)
        {
            name = name.TrimEnd('.');
            return name.Length == 0 ? null : name;
        }

        private static bool NamesEqual(string? left, string? right)
        {
            return left is not null &&
                   right is not null &&
                   string.Equals(
                       left.TrimEnd('.'),
                       right.TrimEnd('.'),
                       StringComparison.OrdinalIgnoreCase);
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
            return await DnsWire.ResolveWithCnameAsync(
                host,
                ipv6,
                async (name, queryType, ct) =>
                {
                    var query = DnsWire.BuildQuery(name, queryType);
                    using var udpClient = new UdpClient();
                    udpClient.Connect(dnsServer);
                    await udpClient.SendAsync(query, query.Length);
                    var result = await udpClient.ReceiveAsync(ct);
                    return result.Buffer;
                },
                cancellationToken);
        }
    }

    public class TcpDnsResolver(IPEndPoint dnsServer) : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            return await DnsWire.ResolveWithCnameAsync(
                host,
                ipv6,
                async (name, queryType, ct) =>
                {
                    var query = DnsWire.BuildQuery(name, queryType);
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(dnsServer.Address, dnsServer.Port, ct);
                    await using var networkStream = tcpClient.GetStream();
                    await networkStream.WriteAsync(query, 0, query.Length, ct);
                    // Read the length of the response (2 bytes)
                    var lengthBuffer = new byte[2];
                    await networkStream.ReadExactlyAsync(lengthBuffer, 0, 2, ct);
                    var responseLength = (lengthBuffer[0] << 8) | lengthBuffer[1];
                    // Read the actual response
                    var responseBuffer = new byte[responseLength];
                    await networkStream.ReadExactlyAsync(responseBuffer, 0, responseLength, ct);
                    return responseBuffer;
                },
                cancellationToken);
        }
    }

    public class DohDnsResolver(HttpClient httpClient, Uri dohServer) : IDnsResolver
    {
        public async Task<List<IPAddress>> ResolveAsync(
            string host,
            bool ipv6,
            CancellationToken cancellationToken = default)
        {
            return await DnsWire.ResolveWithCnameAsync(
                host,
                ipv6,
                async (name, queryType, ct) =>
                {
                    var query = DnsWire.BuildQuery(name, queryType);

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
                        await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsByteArrayAsync(ct);
                },
                cancellationToken);
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
