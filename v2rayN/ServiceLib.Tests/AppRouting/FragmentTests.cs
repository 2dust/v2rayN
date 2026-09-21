using System.Buffers.Binary;
using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class FragmentTests
{
    [Test]
    [Arguments(false, 8)]
    [Arguments(false, 16)]
    [Arguments(true, 8)]
    [Arguments(true, 16)]
    public async Task FragmentedSynMustReachTheFreshOwnershipCheck(bool six, int firstLength)
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(six), new byte[32]);
        var header = six ? 40 : 20;
        original[six ? 6 : 9] = 6;
        original[header + 13] = 2;
        var buffer = new RouteFragmentBuffer(_ => RouteDecisionKind.Unselected);
        buffer.Add(Fragment(original, 0, firstLength, true), default, out var first);
        await first.Should().BeNull();
        buffer.Add(Fragment(original, firstLength, 40 - firstLength, false), default, out var whole);
        await whole!.PassThrough.Should().BeFalse();
        await whole.Packet.SequenceEqual(original).Should().BeTrue();
    }

    [Test]
    public async Task ReflectedTcpFragmentsMustReachReverseNat()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        await using var engine = new AppRouteEngine([], [], _ => { });
        var ports = (Dictionary<AddressFamily, ushort>)typeof(AppRouteEngine).GetField("_ports", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        ports[AddressFamily.InterNetwork] = 43210;
        var flow = PacketTests.Flow(false) with { Protocol = 6, LocalPort = 43210 };
        await engine.ClassifyFragment(flow).Should().BeEqualTo(RouteDecisionKind.Selected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OutOfOrderFragmentsProduceOneDatagramAndKeepOriginals(bool six)
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(six), Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        var buffer = new RouteFragmentBuffer();
        var last = Fragment(original, 16, 24, false);
        var first = Fragment(original, 0, 16, true);
        await buffer.Add(last, default, out var pending).Should().BeTrue();
        await (pending == null).Should().BeTrue();
        await buffer.Add(first, default, out var batch).Should().BeTrue();
        await (batch != null).Should().BeTrue();
        await batch!.Packet.SequenceEqual(original).Should().BeTrue();
        await batch.Originals.Count.Should().BeEqualTo(2);
        await RoutePacket.Parse(batch.Packet)!.Flow.Should().BeEqualTo(RoutePacket.Parse(original)!.Flow);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OverlapRejectsEntireAssembly(bool six)
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(six), new byte[32]);
        var buffer = new RouteFragmentBuffer();
        buffer.Add(Fragment(original, 0, 16, true), default, out _);
        var rejected = false;
        try
        {
            buffer.Add(Fragment(original, 8, 32, false), default, out _);
        }
        catch (IOException) { rejected = true; }
        await rejected.Should().BeTrue();
        buffer.Add(Fragment(original, 16, 24, false), default, out var batch);
        await (batch == null).Should().BeTrue();
    }

    [Test]
    public async Task DuplicatesDoNotCompleteEarlyAndStaleAssembliesExpire()
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(false), new byte[32]);
        var first = Fragment(original, 0, 16, true);
        var last = Fragment(original, 16, 24, false);
        var buffer = new RouteFragmentBuffer();
        buffer.Add(first, default, out _);
        buffer.Add(first, default, out var duplicate);
        await (duplicate == null).Should().BeTrue();
        buffer.Expire(Environment.TickCount64 + 16_000);
        buffer.Add(last, default, out var expired);
        await (expired == null).Should().BeTrue();
        buffer.Add(first, default, out var complete);
        await (complete != null).Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnselectedFirstFragmentReleasesEarlierPartsAndBypassesLaterReassembly(bool six)
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(six), new byte[40]);
        var decisions = 0;
        var buffer = new RouteFragmentBuffer(_ => { decisions++; return RouteDecisionKind.Unselected; });
        var middle = Fragment(original, 16, 16, true);
        var first = Fragment(original, 0, 16, true);
        var last = Fragment(original, 32, 16, false);
        buffer.Add(middle, default, out _);
        buffer.Add(first, default, out var bypass);
        await bypass!.PassThrough.Should().BeTrue();
        await bypass.Originals.Count.Should().BeEqualTo(2);
        await bypass.Originals[0].Packet.SequenceEqual(middle).Should().BeTrue();
        buffer.Add(last, default, out var tail);
        await tail!.PassThrough.Should().BeTrue();
        await tail.Originals.Single().Packet.SequenceEqual(last).Should().BeTrue();
        await decisions.Should().BeEqualTo(1);
    }

    [Test]
    public async Task FragmentBypassDecisionIsReevaluatedForNewFirstFragmentAndPolicy()
    {
        var original = RoutePacket.CreateUdpReply(PacketTests.Flow(false), new byte[32]);
        var decision = RouteDecisionKind.Unselected;
        var buffer = new RouteFragmentBuffer(_ => decision);
        var first = Fragment(original, 0, 16, true);
        buffer.Add(first, default, out var bypass);
        await bypass!.PassThrough.Should().BeTrue();
        decision = RouteDecisionKind.Selected;
        buffer.ClearDecisions();
        buffer.Add(first, default, out var pending);
        await pending.Should().BeNull();
        buffer.Add(Fragment(original, 16, 24, false), default, out var selected);
        await selected!.PassThrough.Should().BeFalse();
        await selected.Packet.SequenceEqual(original).Should().BeTrue();
    }

    private static byte[] Fragment(byte[] original, int offset, int count, bool more)
    {
        var six = original[0] >> 4 == 6;
        var header = six ? 40 : 20;
        var result = new byte[header + (six ? 8 : 0) + count];
        original.AsSpan(0, header).CopyTo(result);
        if (six)
        {
            result[6] = 44;
            result[40] = original[6];
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(42), (ushort)(offset | (more ? 1 : 0)));
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4), (ushort)(result.Length - 40));
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(6), (ushort)(offset / 8 | (more ? 0x2000 : 0)));
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2), (ushort)result.Length);
        }
        original.AsSpan(header + offset, count).CopyTo(result.AsSpan(header + (six ? 8 : 0)));
        return result;
    }
}
