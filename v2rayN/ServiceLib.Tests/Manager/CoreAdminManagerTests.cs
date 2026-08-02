using AwesomeAssertions;
using ServiceLib.Manager;
using Xunit;

namespace ServiceLib.Tests.Manager;

public class CoreAdminManagerTests
{
    [Fact]
    public void TrackSudoPid_TwoElevatedCores_KeepsBothPids()
    {
        // Regression guard for the leaked root-owned core: with TUN enabled on
        // non-Windows, LoadCore elevates the main core and then the pre core, so
        // RunProcessAsLinuxSudo runs twice per launch. Keeping only the most recent
        // PID orphans the other launcher, and the unelevated app can never kill a
        // root-owned process afterwards.
        var manager = new CoreAdminManager();

        manager.TrackSudoPid(1001); // main core, e.g. Xray
        manager.TrackSudoPid(1002); // pre core, e.g. sing-box

        // Reverse order: the most recently started core is terminated first.
        manager.DrainSudoPids().Should().Equal([1002, 1001]);
    }

    [Fact]
    public void DrainSudoPids_SecondCall_ReturnsEmpty()
    {
        var manager = new CoreAdminManager();
        manager.TrackSudoPid(1001);

        manager.DrainSudoPids().Should().Equal([1001]);
        manager.DrainSudoPids().Should().BeEmpty();
    }

    [Fact]
    public void DrainSudoPids_NothingTracked_ReturnsEmpty()
    {
        new CoreAdminManager().DrainSudoPids().Should().BeEmpty();
    }
}
