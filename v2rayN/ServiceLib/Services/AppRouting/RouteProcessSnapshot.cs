using Microsoft.Win32.SafeHandles;

namespace ServiceLib.Services.AppRouting;

/// <summary>Read-only Toolhelp snapshots with stable process handles and exact creation/exit times.</summary>
[SupportedOSPlatform("windows")]
internal sealed class RouteProcessSnapshot : IDisposable
{
    private readonly Dictionary<int, (RouteProcessInfo Info, SafeProcessHandle Handle)> _processes = [];

    public List<RouteProcessInfo> Read()
    {
        var result = new List<RouteProcessInfo>();
        // Retained handles allow an already observed parent's children to inherit after it exits.
        foreach (var (pid, tracked) in _processes.ToList())
        {
            var wait = WaitForSingleObject(tracked.Handle, 0);
            if (wait == 258)
            {
                result.Add(tracked.Info);
                continue;
            } // WAIT_TIMEOUT: still alive
            if (wait != 0 || !GetProcessTimes(tracked.Handle, out _, out var exited, out _, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            result.Add(tracked.Info with
            {
                Exited = exited
            });
            tracked.Handle.Dispose();
            _processes.Remove(pid);
        }

        var snapshotTime = DateTime.UtcNow.ToFileTimeUtc();
        using var snapshot = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
        if (snapshot.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        if (!Process32FirstW(snapshot, ref entry))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        do
        {
            var pid = checked((int)entry.Pid);
            if (pid <= 4 || _processes.ContainsKey(pid))
            {
                continue;
            }

            var handle = OpenProcess(0x1000 | 0x100000, false, pid); // QUERY_LIMITED_INFORMATION | SYNCHRONIZE
            var retained = false;
            try
            {
                if (handle.IsInvalid || !GetProcessTimes(handle, out var started, out _, out _, out _) || started > snapshotTime)
                {
                    continue; // The PID was reused after this snapshot, or is inaccessible.
                }

                var path = new StringBuilder(32768);
                var length = path.Capacity;
                var fullPath = QueryFullProcessImageName(handle, 0, path, ref length) ? path.ToString() : null;
                var info = new RouteProcessInfo(new(pid, started), checked((int)entry.ParentPid),
                    fullPath == null ? entry.Name : Path.GetFileName(fullPath), fullPath);
                _processes.Add(pid, (info, handle));
                retained = true;
                result.Add(info);
            }
            finally
            {
                if (!retained)
                {
                    handle.Dispose();
                }
            }
        } while (Process32NextW(snapshot, ref entry));
        if (Marshal.GetLastWin32Error() != 18)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error()); // NO_MORE_FILES
        }
        // Recheck after enumerating children: a parent may have exited/reused its PID during the snapshot.
        foreach (var (pid, tracked) in _processes.ToList())
        {
            var wait = WaitForSingleObject(tracked.Handle, 0);
            if (wait == 258)
            {
                continue;
            }

            if (wait != 0 || !GetProcessTimes(tracked.Handle, out _, out var exited, out _, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            result.Add(tracked.Info with
            {
                Exited = exited
            });
            tracked.Handle.Dispose();
            _processes.Remove(pid);
        }
        return result;
    }

    public static RouteProcessKey? ReadKey(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid);
        return !handle.IsInvalid && GetProcessTimes(handle, out var started, out _, out _, out _) ? new(pid, started) : null;
    }

    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            process.Handle.Dispose();
        }

        _processes.Clear();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Pid;
        public UIntPtr DefaultHeap;
        public uint ModuleId, Threads, ParentPid;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
}
