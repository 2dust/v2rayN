using v2rayN.WebAPI.Launcher;

if (args.Length != 2) return 64;
var mode = args[0];
var path = args[1];
if (mode == "read-owner")
{
    Console.WriteLine($"owner:{WebInstanceLock.ReadOwnerProcessId(path)}");
    return 0;
}
if (mode is not ("hold" or "try-acquire")) return 64;
if (!WebInstanceLock.TryAcquire(path, writeOwner: true, out var instanceLock))
{
    Console.WriteLine("busy");
    return 2;
}
using (instanceLock)
{
    if (mode == "try-acquire")
    {
        Console.WriteLine("acquired");
        return 0;
    }
    Console.WriteLine($"locked:{Environment.ProcessId}");
    while (await Console.In.ReadLineAsync() is { } command && command != "release")
    {
        if (command != "read-owner") return 64;
        Console.WriteLine($"owner:{WebInstanceLock.ReadOwnerProcessId(path)}");
    }
}
return 0;
