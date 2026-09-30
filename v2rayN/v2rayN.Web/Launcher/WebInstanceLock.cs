using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace v2rayN.Web.Launcher;

public sealed class WebInstanceLock : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    private WebInstanceLock(FileStream stream)
    {
        _stream = stream;
    }

    public static bool TryAcquire(string path, bool writeOwner, out WebInstanceLock? instanceLock)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Instance lock path has no parent directory.");
        Directory.CreateDirectory(directory);

        FileStream stream;
        if (OperatingSystem.IsLinux())
        {
            var descriptor = Open(fullPath, OpenReadWrite | OpenCreate | OpenCloseOnExec, UserReadWriteMode);
            if (descriptor < 0)
            {
                throw new IOException("Unable to open the v2rayN Web instance lock.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }
            stream = new FileStream(new SafeFileHandle((IntPtr)descriptor, ownsHandle: true), FileAccess.ReadWrite);
        }
        else
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
            };
            try
            {
                stream = new FileStream(fullPath, options);
            }
            catch (IOException)
            {
                instanceLock = null;
                return false;
            }
        }

        var acquired = !OperatingSystem.IsLinux() || TryFlock(stream, LockExclusive | LockNonBlocking);
        if (!acquired)
        {
            stream.Dispose();
            instanceLock = null;
            return false;
        }

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        if (writeOwner)
        {
            var owner = Encoding.ASCII.GetBytes($"{Environment.ProcessId}\n");
            stream.SetLength(0);
            stream.Position = 0;
            stream.Write(owner);
            stream.Flush(flushToDisk: true);
        }

        instanceLock = new WebInstanceLock(stream);
        return true;
    }

    public static int? ReadOwnerProcessId(string path)
    {
        try
        {
            string ownerText;
            if (OperatingSystem.IsLinux())
            {
                var descriptor = Open(Path.GetFullPath(path), OpenReadOnly | OpenCloseOnExec, 0);
                if (descriptor < 0)
                {
                    return null;
                }
                using var stream = new FileStream(new SafeFileHandle((IntPtr)descriptor, ownsHandle: true), FileAccess.Read);
                using var reader = new StreamReader(stream, Encoding.ASCII);
                ownerText = reader.ReadToEnd().Trim();
            }
            else
            {
                ownerText = File.ReadAllText(path, Encoding.ASCII).Trim();
            }
            return int.TryParse(ownerText, out var processId) && processId > 0 ? processId : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool IsHeld(string path)
    {
        if (!TryAcquire(path, writeOwner: false, out var probe))
        {
            return true;
        }

        probe!.Dispose();
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (OperatingSystem.IsLinux())
        {
            var descriptor = _stream.SafeFileHandle.DangerousGetHandle().ToInt32();
            _ = Flock(descriptor, LockUnlock);
        }
        _stream.Dispose();
    }

    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int OpenReadOnly = 0;
    private const int OpenReadWrite = 2;
    private const int OpenCreate = 64;
    private const int OpenCloseOnExec = 524288;
    private const uint UserReadWriteMode = 0x180;

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags, uint mode);

    private static bool TryFlock(FileStream stream, int operation)
    {
        var descriptor = stream.SafeFileHandle.DangerousGetHandle().ToInt32();
        if (Flock(descriptor, operation) == 0)
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error is 11 or 35)
        {
            return false;
        }

        throw new IOException("Unable to acquire the v2rayN Web instance lock.", new Win32Exception(error));
    }

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int fileDescriptor, int operation);
}
