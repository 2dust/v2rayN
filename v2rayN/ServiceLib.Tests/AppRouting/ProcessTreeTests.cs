using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class ProcessTreeTests
{
    private static AppRouteRule Rule(string name, bool children = true) => new()
    {
        ExecutablePath = name,
        MatchByName = true,
        IncludeChildProcesses = children,
        Kind = AppRouteKind.Socks5
    };
    private static RouteProcessInfo Process(int pid, long started, int parent, string name, long? exited = null) =>
        new(new(pid, started), parent, name, null, exited);

    [Test]
    public async Task DescendantsInheritOnlyWhenEnabled()
    {
        foreach (var children in new[] { false, true })
        {
            var rule = Rule("Parent.exe", children);
            var tree = new RouteProcessTree(new([rule]), []);
            tree.Update([Process(10, 1, 0, "Parent.exe"), Process(20, 2, 10, "Child.exe"), Process(30, 3, 20, "Grandchild.exe")]);
            await tree.Find(new(10, 1)).Should().BeEqualTo(rule);
            await tree.Find(new(20, 2)).Should().BeEqualTo(children ? rule : null);
            await tree.Find(new(30, 3)).Should().BeEqualTo(children ? rule : null);
        }
    }

    [Test]
    public async Task ExplicitChildRuleAndNearestEnabledAncestorTakePriority()
    {
        var root = Rule("Parent.exe");
        var child = Rule("Child.exe");
        var leaf = Rule("Leaf.exe", false);
        var tree = new RouteProcessTree(new([root, child, leaf]), []);
        tree.Update([Process(10, 1, 0, "Parent.exe"), Process(20, 2, 10, "Child.exe"),
            Process(30, 3, 20, "Leaf.exe"), Process(40, 4, 30, "Worker.exe")]);
        await tree.Find(new(20, 2)).Should().BeEqualTo(child);
        await tree.Find(new(30, 3)).Should().BeEqualTo(leaf);
        await tree.Find(new(40, 4)).Should().BeEqualTo(child);
    }

    [Test]
    public async Task ParentExitAndPidReuseDoNotLoseOrTransferExistingInheritance()
    {
        var rule = Rule("Parent.exe");
        var tree = new RouteProcessTree(new([rule]), []);
        tree.Update([Process(10, 1, 0, "Parent.exe"), Process(20, 2, 10, "Child.exe")]);
        tree.Update([Process(10, 1, 0, "Parent.exe", 3), Process(10, 4, 0, "Unrelated.exe"),
            Process(30, 5, 10, "Other child.exe"), Process(40, 6, 20, "Grandchild.exe")]);
        await tree.Find(new(20, 2)).Should().BeEqualTo(rule);
        await tree.Find(new(40, 6)).Should().BeEqualTo(rule);
        await (tree.Find(new(30, 5)) == null).Should().BeTrue();
        await (tree.Find(new(10, 4)) == null).Should().BeTrue();
    }

    [Test]
    public async Task ChildFirstObservedAfterParentExitUsesVerifiedLifetime()
    {
        var rule = Rule("Parent.exe");
        var tree = new RouteProcessTree(new([rule]), []);
        tree.Update([Process(10, 1, 0, "Parent.exe")]);
        tree.Update([Process(10, 1, 0, "Parent.exe", 3), Process(20, 2, 10, "Child.exe"), Process(30, 4, 10, "Unrelated.exe")]);
        await tree.Find(new(20, 2)).Should().BeEqualTo(rule);
        await (tree.Find(new(30, 4)) == null).Should().BeTrue();
    }

    [Test]
    public async Task LaterParentGenerationCannotCaptureAnOlderChild()
    {
        var tree = new RouteProcessTree(new([Rule("Parent.exe")]), []);
        tree.Update([Process(10, 5, 0, "Parent.exe"), Process(20, 2, 10, "Child.exe")]);
        await (tree.Find(new(20, 2)) == null).Should().BeTrue();
    }

    [Test]
    public async Task ProxyCoresAndExcludedSubtreesStayUnroutedEvenWithExplicitRules()
    {
        var rule = Rule("Parent.exe");
        var child = Rule("Child.exe");
        var tree = new RouteProcessTree(new([rule, child]), [40]);
        tree.Update([Process(10, 1, 0, "Parent.exe"), Process(20, 2, 10, "xray.exe"),
            Process(30, 3, 20, "Child.exe"), Process(40, 4, 10, "Excluded.exe"), Process(50, 5, 40, "Child.exe")]);
        foreach (var key in new[] { new RouteProcessKey(20, 2), new(30, 3), new(40, 4), new(50, 5) })
        {
            await (tree.Find(key) == null).Should().BeTrue();
        }
    }

    [Test]
    public async Task NativeSnapshotFindsCurrentProcessAndStableCreationTimeWithoutDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var snapshot = new RouteProcessSnapshot();
        var own = snapshot.Read().Single(p => p.Key.Pid == Environment.ProcessId);
        await (own.Key.Started > 0).Should().BeTrue();
        await (own.ParentPid > 0).Should().BeTrue();
        await RouteProcessSnapshot.ReadKey(Environment.ProcessId).Should().BeEqualTo(own.Key);
        await snapshot.Read().Single(p => p.Key == own.Key).Name.Should().BeEqualTo(own.Name);
    }

    [Test]
    public async Task NativeChildInheritsAndSnapshotRecordsItsExitWithoutDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var snapshot = new RouteProcessSnapshot();
        var own = snapshot.Read().Single(p => p.Key.Pid == Environment.ProcessId);
        var rule = Rule(own.Name);
        var tree = new RouteProcessTree(new([rule]), []);
        // Exercise x86-to-x64 process lookup when this test host runs under WOW64.
        var systemDirectory = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative") : Environment.SystemDirectory;
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", "Write-Output ('ready:' + [Environment]::Is64BitProcess); [Console]::ReadLine() | Out-Null" })
        {
            start.ArgumentList.Add(arg);
        }

        using var child = System.Diagnostics.Process.Start(start)!;
        try
        {
            var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await ready.Should().BeEqualTo($"ready:{Environment.Is64BitOperatingSystem}");
            var processes = snapshot.Read();
            var info = processes.Single(p => p.Key.Pid == child.Id);
            var identity = AppRouteProcessCatalog.ReadIdentity(child.Id);
            await identity.HasValue.Should().BeTrue();
            await identity!.Value.Path.Should().BeEqualTo(info.Path);
            await info.ParentPid.Should().BeEqualTo(Environment.ProcessId);
            tree.Update(processes);
            await tree.Find(info.Key).Should().BeEqualTo(rule);
            await child.StandardInput.WriteLineAsync("");
            // Wait for the kernel handle: HasExited/WaitForExitAsync can observe
            // an exit code before native process teardown has finished.
            await child.WaitForExit(10_000).Should().BeTrue();
            var exited = snapshot.Read().Single(p => p.Key == info.Key);
            await exited.Exited.Should().BeEqualTo(child.ExitTime.ToUniversalTime().ToFileTimeUtc());
            await (exited.Exited >= info.Key.Started).Should().BeTrue();
        }
        finally
        {
            // Only the fixture process created above can be stopped here.
            if (!child.HasExited)
            {
                child.Kill();
                await child.WaitForExitAsync();
            }
        }
    }
}
