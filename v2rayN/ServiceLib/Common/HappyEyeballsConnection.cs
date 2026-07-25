using System.Net.Sockets;

namespace ServiceLib.Common;

public static class HappyEyeballsConnection
{
    private static readonly TimeSpan DefaultPerAttemptTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultFamilyDelay = TimeSpan.FromMilliseconds(250);

    public static IList<IPAddress> OrderCandidates(IEnumerable<IPAddress> addresses)
    {
        var list = addresses.Where(static address => address != null).ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var ipv6 = list.Where(static address => address.AddressFamily == AddressFamily.InterNetworkV6)
            .OrderBy(static address => address.ToString(), StringComparer.Ordinal)
            .ToList();
        var ipv4 = list.Where(static address => address.AddressFamily == AddressFamily.InterNetwork)
            .OrderBy(static address => address.ToString(), StringComparer.Ordinal)
            .ToList();

        return [.. ipv6, .. ipv4];
    }

    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken,
        TimeSpan? perAttemptTimeout = null,
        TimeSpan? familyDelay = null)
    {
        if (context.DnsEndPoint is null)
        {
            throw new InvalidOperationException("DnsEndPoint is required for Happy Eyeballs connection.");
        }

        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        var ordered = OrderCandidates(addresses);
        var ipv6 = ordered.Where(static address => address.AddressFamily == AddressFamily.InterNetworkV6).ToList();
        var ipv4 = ordered.Where(static address => address.AddressFamily == AddressFamily.InterNetwork).ToList();

        if (ipv6.Count == 0 && ipv4.Count == 0)
        {
            throw new SocketException((int)SocketError.AddressFamilyNotSupported);
        }

        return await ConnectFamiliesAsync(port, ipv6, ipv4, perAttemptTimeout ?? DefaultPerAttemptTimeout, familyDelay ?? DefaultFamilyDelay, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Stream> ConnectFamiliesAsync(
        int port,
        List<IPAddress> ipv6,
        List<IPAddress> ipv4,
        TimeSpan perAttemptTimeout,
        TimeSpan familyDelay,
        CancellationToken cancellationToken)
    {
        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var attempts = new List<(Task<Socket?> Task, CancellationTokenSource Cts)>();

        try
        {
            var primary = ipv6.Count > 0 ? ipv6 : ipv4;
            var secondary = ipv6.Count > 0 ? ipv4 : ipv6;

            StartFamily(primary, port, attempts, raceCts.Token, perAttemptTimeout);

            if (secondary.Count > 0)
            {
                var delayTask = Task.Delay(familyDelay, raceCts.Token);
                var winner = await RaceForWinnerAsync(attempts, delayTask).ConfigureAwait(false);
                if (winner != null)
                {
                    return winner;
                }

                StartFamily(secondary, port, attempts, raceCts.Token, perAttemptTimeout);
            }

            while (attempts.Count > 0)
            {
                var finishedTask = await Task.WhenAny(attempts.Select(attempt => (Task)attempt.Task)).ConfigureAwait(false);
                var finished = attempts.First(attempt => (Task)attempt.Task == finishedTask);
                attempts.Remove(finished);

                var socket = await finished.Task.ConfigureAwait(false);
                finished.Cts.Dispose();
                if (socket != null)
                {
                    return new NetworkStream(socket, ownsSocket: true);
                }
            }

            throw new SocketException((int)SocketError.TimedOut);
        }
        finally
        {
            raceCts.Cancel();
            foreach (var (_, attemptCts) in attempts)
            {
                attemptCts.Dispose();
            }
        }
    }

    private static void StartFamily(
        List<IPAddress> family,
        int port,
        List<(Task<Socket?> Task, CancellationTokenSource Cts)> attempts,
        CancellationToken cancellationToken,
        TimeSpan perAttemptTimeout)
    {
        foreach (var address in family)
        {
            var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(perAttemptTimeout);
            attempts.Add((ConnectSingleAsync(address, port, attemptCts.Token), attemptCts));
        }
    }

    private static async Task<Stream?> RaceForWinnerAsync(
        List<(Task<Socket?> Task, CancellationTokenSource Cts)> attempts,
        Task delayTask)
    {
        var pending = attempts.Select(attempt => (Task)attempt.Task).Append(delayTask).ToList();
        while (pending.Count > 1)
        {
            var completed = await Task.WhenAny(pending).ConfigureAwait(false);
            if (completed == delayTask)
            {
                return null;
            }

            pending.Remove(completed);
            var attempt = attempts.First(candidate => (Task)candidate.Task == completed);
            var socket = await attempt.Task.ConfigureAwait(false);
            if (socket != null)
            {
                return new NetworkStream(socket, ownsSocket: true);
            }
        }

        return null;
    }

    private static async Task<Socket?> ConnectSingleAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            return null;
        }
    }
}
