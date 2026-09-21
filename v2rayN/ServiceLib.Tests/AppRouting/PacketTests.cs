using System.Buffers.Binary;
using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class PacketTests
{
    [Test]
    public async Task NativeAddressHasCompleteUnion()
    {
        await Marshal.SizeOf<DivertAddress>().Should().BeEqualTo(80);
        await Marshal.OffsetOf<DivertAddress>(nameof(DivertAddress.InterfaceIndex)).ToInt32().Should().BeEqualTo(16);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UdpReplyPreservesBothAddressesPortsAndPayload(bool ipv6)
    {
        var flow = Flow(ipv6);
        var payload = new byte[] { 0, 1, 2, 255 };
        var reply = RoutePacket.CreateUdpReply(flow, payload);
        var parsed = RoutePacket.Parse(reply)!;
        await parsed.Flow.LocalAddress.Equals(flow.RemoteAddress).Should().BeTrue();
        await parsed.Flow.RemoteAddress.Equals(flow.LocalAddress).Should().BeTrue();
        await parsed.Flow.LocalPort.Should().BeEqualTo(flow.RemotePort);
        await parsed.Flow.RemotePort.Should().BeEqualTo(flow.LocalPort);
        await reply.AsSpan(parsed.TransportOffset + 8).SequenceEqual(payload).Should().BeTrue();
        parsed.Rewrite(reply, flow.LocalAddress, flow.LocalPort, flow.RemoteAddress, flow.RemotePort);
        await RoutePacket.Parse(reply)!.Flow.Should().BeEqualTo(flow);
    }

    [Test]
    public async Task MalformedAndFragmentedPacketsAreRejected()
    {
        var reply = RoutePacket.CreateUdpReply(Flow(false), [1, 2, 3]);
        for (var size = 0; size < reply.Length; size++)
        {
            await (RoutePacket.Parse(reply.AsSpan(0, size)) == null).Should().BeTrue();
        }

        reply[6] = 0x20;
        await (RoutePacket.Parse(reply) == null).Should().BeTrue();
        reply[6] = 0;
        reply[0] = 0x41;
        await (RoutePacket.Parse(reply) == null).Should().BeTrue();
        var six = RoutePacket.CreateUdpReply(Flow(true), [1]);
        six[6] = 44;
        await (RoutePacket.Parse(six) == null).Should().BeTrue();
    }

    [Test]
    public async Task Ipv6ExtensionHeadersAreWalked()
    {
        var original = RoutePacket.CreateUdpReply(Flow(true), [8, 9]);
        var bytes = new byte[original.Length + 8];
        original.AsSpan(0, 40).CopyTo(bytes);
        bytes[6] = 0;
        bytes[40] = 17;
        bytes[41] = 0;
        original.AsSpan(40).CopyTo(bytes.AsSpan(48));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)(bytes.Length - 40));
        await RoutePacket.Parse(bytes)!.TransportOffset.Should().BeEqualTo(48);
    }

    [Test]
    public async Task LinkLocalIpv6UsesCapturedInterfaceScope()
    {
        var flow = Flow(true) with
        {
            LocalAddress = IPAddress.Parse("fe80::1"),
            RemoteAddress = IPAddress.Parse("fe80::2")
        };
        var packet = RoutePacket.Parse(RoutePacket.CreateUdpReply(flow, [1]), 7)!;
        await packet.Flow.LocalAddress.ScopeId.Should().BeEqualTo(7L);
        await packet.Flow.RemoteAddress.ScopeId.Should().BeEqualTo(7L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TcpSynPreservesInitialSequenceAndDistinguishesSynAck(bool ipv6)
    {
        var bytes = RoutePacket.CreateUdpReply(Flow(ipv6), new byte[12]);
        var transport = ipv6 ? 40 : 20;
        bytes[ipv6 ? 6 : 9] = 6;
        bytes[transport + 12] = 0x50;
        bytes[transport + 13] = 0x02;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(transport + 4), 0xfedcba98);
        var syn = RoutePacket.Parse(bytes)!;
        await syn.IsTcpSyn.Should().BeTrue();
        await syn.TcpSequence.Should().BeEqualTo(0xfedcba98u);
        bytes[transport + 13] = 0x12;
        await RoutePacket.Parse(bytes)!.IsTcpSyn.Should().BeFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReconnectedTupleKeepsLateResponsesSeparate(bool relayAlreadyClosed)
    {
        var table = new RouteNatTable();
        var flow = Flow(false) with
        {
            Protocol = 6
        };
        var rule = new AppRouteRule();
        var old = table.GetOrAdd(flow, rule, 100);
        old.Accepted = true;
        await ReferenceEquals(table.Find(flow, 100), old).Should().BeTrue(); // Retransmitted SYN.
        old.Closed = relayAlreadyClosed;
        old.LastActivity = 0;
        await (table.Find(flow, relayAlreadyClosed ? 100u : 200u) == null).Should().BeTrue();
        var fresh = table.GetOrAdd(flow, rule, 200);
        fresh.Accepted = true;
        await (fresh.TranslatedPort != old.TranslatedPort).Should().BeTrue();
        await ReferenceEquals(table.Reverse(flow.LocalAddress, flow.RemoteAddress, old.TranslatedPort), old).Should().BeTrue();
        table.Expire(old.LastActivity + 150_000);
        await ReferenceEquals(table.Find(flow), fresh).Should().BeTrue();
        await (table.Reverse(flow.LocalAddress, flow.RemoteAddress, old.TranslatedPort) == null).Should().BeTrue();
    }

    [Test]
    public async Task SameSourcePortDoesNotMixConnectionsOrAddressFamilies()
    {
        var table = new RouteNatTable();
        var rule = new AppRouteRule();
        var first = Flow(false) with
        {
            Protocol = 6
        };
        var second = first with
        {
            RemoteAddress = IPAddress.Parse("203.0.113.9")
        };
        var third = Flow(true) with
        {
            Protocol = 6
        };
        var a = table.GetOrAdd(first, rule, 100);
        var b = table.GetOrAdd(second, rule, 100);
        var c = table.GetOrAdd(third, rule, 100);
        await (a.TranslatedPort != b.TranslatedPort && b.TranslatedPort != c.TranslatedPort).Should().BeTrue();
        await (table.Reverse(first.LocalAddress, second.RemoteAddress, a.TranslatedPort) == null).Should().BeTrue();
        await table.Find(first)!.Flow.Should().BeEqualTo(first);
        a.Closed = true;
        a.LastActivity = 0;
        b.Accepted = true;
        b.LastActivity = 150_000;
        table.Expire(150_001);
        await (table.Find(first) == null).Should().BeTrue();
        await (table.Find(second) != null).Should().BeTrue();
    }

    internal static RouteFlow Flow(bool six) => new(17,
        IPAddress.Parse(six ? "2001:db8::1" : "192.0.2.1"), 51000,
        IPAddress.Parse(six ? "2001:db8::2" : "198.51.100.2"), 443);
}
