using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class AttributionTests
{
    [Test]
    public async Task IndexedLookupsDoNotResolveProcessesPerPacketAndPreserveAmbiguity()
    {
        var flow = PacketTests.Flow(false);
        var rule = new AppRouteRule { Id = "selected" };
        var decisions = new Dictionary<int, RouteDecision>
        {
            [10] = new(RouteDecisionKind.Selected, new(10, 1), rule),
            [20] = new(RouteDecisionKind.Unselected, new(20, 2))
        };
        var calls = 0;
        var snapshot = new RouteAttributionSnapshot([], [new(IPAddress.Any, flow.LocalPort, null, 0, 10)],
            pid => { calls++; return decisions[pid]; }, 100);
        for (var i = 0; i < 10_000; i++) { _ = snapshot.Find(flow); }
        await calls.Should().BeEqualTo(1);
        await snapshot.Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Selected);
        await snapshot.Find(flow with { LocalPort = 1 }).Kind.Should().BeEqualTo(RouteDecisionKind.Unresolved);
        var shared = new RouteAttributionSnapshot([], [new(IPAddress.Any, flow.LocalPort, null, 0, 10), new(flow.LocalAddress, flow.LocalPort, null, 0, 20)],
            pid => decisions[pid], 100);
        await shared.Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Ambiguous);
    }

    [Test]
    public async Task RefreshedSnapshotsInvalidatePriorOwnerAndPassThroughDecisions()
    {
        var flow = PacketTests.Flow(false) with { Protocol = 6 };
        var row = new RouteOwnerTable.Row(flow.LocalAddress, flow.LocalPort, flow.RemoteAddress, flow.RemotePort, 10);
        var old = new RouteAttributionSnapshot([row], [], _ => new(RouteDecisionKind.Unselected, new(10, 100)), 1);
        var current = new RouteAttributionSnapshot([row], [], _ => new(RouteDecisionKind.Selected, new(10, 200), new()), 2);
        var closed = new RouteAttributionSnapshot([], [], _ => throw new Exception(), 3);
        await old.Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Unselected);
        await current.Find(flow).Process.Should().BeEqualTo(new RouteProcessKey(10, 200));
        await closed.Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Unresolved);
    }

    [Test]
    public async Task AttributionQueueIsBoundedAndRetainsOriginalArrivalAcrossRetries()
    {
        var queue = new RoutePendingPackets();
        for (var i = 0; i < 64; i++) { await queue.Add(new byte[65536], default, 123, null).Should().BeTrue(); }
        await queue.Add([1], default, 123, null).Should().BeFalse();
        var packets = queue.Drain();
        await packets.All(p => p.Arrived == 123).Should().BeTrue();
        await queue.Add([1], default, packets[0].Arrived, null).Should().BeTrue();
    }

    [Test]
    public async Task PolicyChangeRetiresOnlyAffectedTcpMappings()
    {
        var first = new AppRouteRule { Id = "first", ExecutablePath = "first.exe", MatchByName = true, Kind = AppRouteKind.ActiveProfile };
        var second = new AppRouteRule { Id = "second", ExecutablePath = "second.exe", MatchByName = true, Kind = AppRouteKind.ActiveProfile };
        var nat = new RouteNatTable();
        var flow = PacketTests.Flow(false) with { Protocol = 6 };
        var kept = nat.GetOrAdd(flow, first, 1);
        var retired = nat.GetOrAdd(flow with { LocalPort = 12345 }, second, 2);
        using var active = retired.BeginRelay(default);
        nat.Retain(new([first], []));
        await kept.Closed.Should().BeFalse();
        await retired.Closed.Should().BeTrue();
        await active.IsCancellationRequested.Should().BeTrue();
        await (nat.Reverse(retired.Flow.LocalAddress, retired.Flow.RemoteAddress, retired.TranslatedPort) == retired).Should().BeTrue();
    }

    [Test]
    public async Task NativeIndexedSnapshotObservesOwnedUdpSocketClosureWithoutDriver()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var source = new RouteAttributionSource();
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint!).Port;
        var flow = new RouteFlow(17, IPAddress.Loopback, port, IPAddress.Loopback, 12345);
        var policy = new RoutePolicy([], []);
        await source.Read(policy).Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Unselected);
        socket.Dispose();
        await source.Read(policy).Find(flow).Kind.Should().BeEqualTo(RouteDecisionKind.Unresolved);
    }
}
