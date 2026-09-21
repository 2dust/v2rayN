using System.Buffers.Binary;
using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class PacketBatchTests
{
    [Test]
    public async Task MixedReceiveSlicesRetainLengthsAndOnlyChosenPacketsAreInjected()
    {
        var packets = new[] { RoutePacket.CreateUdpReply(PacketTests.Flow(false), [1]),
            RoutePacket.CreateUdpReply(PacketTests.Flow(true), [2, 3]), RoutePacket.CreateUdpReply(PacketTests.Flow(false), [4]) };
        // Fragments/unsupported transports must still be framed without parsing transport headers.
        packets[0][6] = 0x20;
        packets[2][9] = 1;
        var received = packets.SelectMany(p => p).ToArray();
        var lengths = new int[32];
        await RoutePacketBatch.ReadLengths(received, 3 * 80, lengths).Should().BeEqualTo(3);
        await lengths.Take(3).SequenceEqual(packets.Select(p => p.Length)).Should().BeTrue();
        var sends = new List<(byte[] Bytes, DivertAddress[] Addresses)>();
        var batch = new RoutePacketBatch((bytes, addresses) => sends.Add((bytes.ToArray(), addresses.ToArray())));
        batch.Add(received.AsSpan(0, lengths[0]), new() { InterfaceIndex = 7, Flags = 0x1234 });
        batch.Add(received.AsSpan(lengths[0] + lengths[1], lengths[2]), new() { InterfaceIndex = 9, Timestamp = 123 });
        received.AsSpan().Fill(255); // Queued output owns its bytes across capture-buffer reuse.
        await sends.Count.Should().BeEqualTo(0);
        batch.Flush();
        batch.Flush();
        await sends.Count.Should().BeEqualTo(1);
        await sends[0].Bytes.SequenceEqual(packets[0].Concat(packets[2])).Should().BeTrue();
        await sends[0].Addresses.Length.Should().BeEqualTo(2);
        await sends[0].Addresses[0].InterfaceIndex.Should().BeEqualTo(7u);
        await sends[0].Addresses[0].Flags.Should().BeEqualTo(0x1234u);
        await sends[0].Addresses[1].Timestamp.Should().BeEqualTo(123L);
    }

    [Test]
    public async Task PacketCapacityFlushesWithoutWaitingForAnotherFullBatch()
    {
        var counts = new List<int>();
        var batch = new RoutePacketBatch((_, addresses) => counts.Add(addresses.Length));
        var packet = RoutePacket.CreateUdpReply(PacketTests.Flow(false), [1]);
        for (var i = 0; i < 33; i++) { batch.Add(packet, new()); }
        batch.Flush();
        await counts.SequenceEqual(new[] { 32, 1 }).Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MaximumPacketFitsAndByteCapacityFlushesIndependentlyOfPacketCount(bool ipv6)
    {
        var packet = RoutePacket.CreateUdpReply(PacketTests.Flow(ipv6), new byte[ipv6 ? 65527 : 65507]);
        var lengths = new int[32];
        await RoutePacketBatch.ReadLengths(packet, 80, lengths).Should().BeEqualTo(1);
        await lengths[0].Should().BeEqualTo(packet.Length);
        var sends = new List<int>();
        var batch = new RoutePacketBatch((bytes, _) => sends.Add(bytes.Length));
        batch.Add(packet, new());
        batch.Add(packet, new());
        batch.Flush();
        await sends.SequenceEqual(new[] { packet.Length, packet.Length }).Should().BeTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(79)]
    [Arguments(81)]
    [Arguments(160)]
    [Arguments(2640)]
    public async Task InvalidMetadataCountIsRejected(int addressLength)
    {
        var rejected = false;
        try { RoutePacketBatch.ReadLengths(RoutePacket.CreateUdpReply(PacketTests.Flow(false), [1]), (uint)addressLength, new int[32]); }
        catch (IOException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task InvalidPacketBoundaryRejectsTheReceive(int corruption)
    {
        var packet = RoutePacket.CreateUdpReply(PacketTests.Flow(false), [1]);
        if (corruption == 0) { packet[0] = 0x70; }
        if (corruption == 1) { BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), 0); }
        if (corruption == 2) { BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)(packet.Length + 1)); }
        if (corruption == 3) { packet = [.. packet, 99]; }
        var rejected = false;
        try { RoutePacketBatch.ReadLengths(packet, 80, new int[32]); }
        catch (IOException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedCapacityFlushPreservesThePacketThatWasNotSubmitted(bool byteLimit)
    {
        var attempts = new List<(byte[] Bytes, DivertAddress[] Addresses)>();
        var batch = new RoutePacketBatch((bytes, addresses) =>
        {
            attempts.Add((bytes.ToArray(), addresses.ToArray()));
            if (attempts.Count == 1) { throw new IOException("Partial native injection"); }
        });
        var previous = RoutePacket.CreateUdpReply(PacketTests.Flow(byteLimit), new byte[byteLimit ? 65527 : 1]);
        for (var i = 0; i < (byteLimit ? 1 : 32); i++) { batch.Add(previous, new()); }
        var next = RoutePacket.CreateUdpReply(PacketTests.Flow(false), [42]);
        var expected = next.ToArray();
        var failed = false;
        try { batch.Add(next, new() { InterfaceIndex = 42 }); }
        catch (IOException) { failed = true; }
        next.AsSpan().Fill(255);
        batch.Flush();
        await failed.Should().BeTrue();
        await attempts.Count.Should().BeEqualTo(2);
        await attempts[1].Bytes.SequenceEqual(expected).Should().BeTrue();
        await attempts[1].Addresses.Length.Should().BeEqualTo(1);
        await attempts[1].Addresses[0].InterfaceIndex.Should().BeEqualTo(42u);
    }

    [Test]
    public async Task FailedInjectionIsNotReplayedByLaterFlushOrAdd()
    {
        var attempts = new List<byte[]>();
        var batch = new RoutePacketBatch((bytes, _) =>
        {
            attempts.Add(bytes.ToArray());
            if (attempts.Count == 1) { throw new IOException("Partial native injection"); }
        });
        var first = RoutePacket.CreateUdpReply(PacketTests.Flow(false), [1]);
        var second = RoutePacket.CreateUdpReply(PacketTests.Flow(true), [2]);
        batch.Add(first, new());
        try { batch.Flush(); } catch (IOException) { }
        batch.Flush();
        batch.Add(second, new());
        batch.Flush();
        await attempts.Count.Should().BeEqualTo(2);
        await attempts[1].SequenceEqual(second).Should().BeTrue();
    }
}
