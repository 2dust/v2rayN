using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace v2rayN.Web.Security;

public sealed class WebSessionService : IDisposable
{
    public const string SessionSnapshotContextKey = "v2rayn.web.session.snapshot";
    public static readonly TimeSpan SlidingLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan SseTicketLifetime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SseTicketCleanupInterval = TimeSpan.FromMinutes(1);
    private const int TokenByteLength = 32;
    private const int TokenEncodedLength = 43;

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SseTicketEntry> _sseTickets = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly Timer _cleanupTimer;
    private readonly Timer _sseTicketCleanupTimer;
    private bool _disposed;

    public WebSessionService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cleanupTimer = new Timer(
            static state => ((WebSessionService)state!).CleanupExpiredSessions(),
            this,
            CleanupInterval,
            CleanupInterval);
        _sseTicketCleanupTimer = new Timer(
            static state => ((WebSessionService)state!).CleanupExpiredSseTickets(),
            this,
            SseTicketCleanupInterval,
            SseTicketCleanupInterval);
    }

    public WebSessionToken CreateSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (true)
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
            var token = EncodeBase64Url(tokenBytes);
            var digest = GetDigest(tokenBytes);
            CryptographicOperations.ZeroMemory(tokenBytes);

            var now = _timeProvider.GetUtcNow();
            var absoluteExpiresAt = now + AbsoluteLifetime;
            var entry = new SessionEntry(
                now,
                now,
                Min(now + SlidingLifetime, absoluteExpiresAt),
                absoluteExpiresAt,
                new CancellationTokenSource());
            if (_sessions.TryAdd(digest, entry))
            {
                return new WebSessionToken(token, entry.ExpiresAt);
            }
            entry.Revoked.Dispose();
        }
    }

    public bool TryValidateAndRenew(string? token, out WebSessionSnapshot? session) =>
        TryValidateSessionCore(token, renew: true, out session);

    public bool TryValidateWithoutRenewal(string? token, out WebSessionSnapshot? session) =>
        TryValidateSessionCore(token, renew: false, out session);

    private bool TryValidateSessionCore(string? token, bool renew, out WebSessionSnapshot? session)
    {
        session = null;
        if (_disposed || !TryGetDigest(token, out var digest))
        {
            return false;
        }

        while (_sessions.TryGetValue(digest, out var current))
        {
            var now = _timeProvider.GetUtcNow();
            if (now >= current.ExpiresAt || now >= current.AbsoluteExpiresAt)
            {
                if (TryRemove(digest, current))
                {
                    return false;
                }
                continue;
            }

            if (!renew)
            {
                session = new WebSessionSnapshot(
                    current.CreatedAt,
                    current.LastSeenAt,
                    current.ExpiresAt,
                    current.AbsoluteExpiresAt,
                    current.Revoked.Token,
                    digest);
                return true;
            }

            var renewed = current with
            {
                LastSeenAt = now,
                ExpiresAt = Min(now + SlidingLifetime, current.AbsoluteExpiresAt),
            };
            if (_sessions.TryUpdate(digest, renewed, current))
            {
                session = new WebSessionSnapshot(
                    renewed.CreatedAt,
                    renewed.LastSeenAt,
                    renewed.ExpiresAt,
                    renewed.AbsoluteExpiresAt,
                    renewed.Revoked.Token,
                    digest);
                return true;
            }
        }

        return false;
    }

    private bool TryValidateSessionDigestWithoutRenewal(string digest, out WebSessionSnapshot? session)
    {
        session = null;
        if (_disposed)
        {
            return false;
        }

        while (_sessions.TryGetValue(digest, out var current))
        {
            var now = _timeProvider.GetUtcNow();
            if (now >= current.ExpiresAt || now >= current.AbsoluteExpiresAt)
            {
                if (TryRemove(digest, current))
                {
                    return false;
                }
                continue;
            }

            session = new WebSessionSnapshot(
                current.CreatedAt,
                current.LastSeenAt,
                current.ExpiresAt,
                current.AbsoluteExpiresAt,
                current.Revoked.Token,
                digest);
            return true;
        }

        return false;
    }

    internal bool TryValidateSseSessionWithoutRenewal(string sessionDigest, out WebSessionSnapshot? session) =>
        TryValidateSessionDigestWithoutRenewal(sessionDigest, out session);

    public bool Revoke(string? token)
    {
        if (_disposed || !TryGetDigest(token, out var digest))
        {
            return false;
        }

        while (_sessions.TryGetValue(digest, out var entry))
        {
            if (TryRemove(digest, entry))
            {
                return true;
            }
        }
        return false;
    }

    public bool TryCreateSseTicket(string? sessionToken, out WebSseTicket? ticket)
    {
        ticket = null;
        if (_disposed || !TryGetDigest(sessionToken, out var sessionDigest)
            || !TryValidateSessionDigestWithoutRenewal(sessionDigest, out _))
        {
            return false;
        }

        CleanupExpiredSseTickets();
        while (true)
        {
            var ticketBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
            var ticketToken = EncodeBase64Url(ticketBytes);
            var ticketDigest = GetDigest(ticketBytes);
            CryptographicOperations.ZeroMemory(ticketBytes);

            var expiresAt = _timeProvider.GetUtcNow() + SseTicketLifetime;
            if (_sseTickets.TryAdd(ticketDigest, new SseTicketEntry(sessionDigest, expiresAt)))
            {
                // Close the race with logout/expiry while the ticket was being minted.
                if (!TryValidateSessionDigestWithoutRenewal(sessionDigest, out _))
                {
                    _sseTickets.TryRemove(ticketDigest, out _);
                    return false;
                }

                ticket = new WebSseTicket(ticketToken, expiresAt);
                return true;
            }
        }
    }

    public bool TryConsumeSseTicket(string? ticketToken, out WebSessionSnapshot? session)
    {
        session = null;
        if (_disposed || !TryGetDigest(ticketToken, out var ticketDigest)
            || !_sseTickets.TryRemove(ticketDigest, out var ticket))
        {
            return false;
        }

        if (_timeProvider.GetUtcNow() >= ticket.ExpiresAt)
        {
            return false;
        }

        return TryValidateSessionDigestWithoutRenewal(ticket.SessionDigest, out session);
    }

    public int CleanupExpiredSessions()
    {
        if (_disposed)
        {
            return 0;
        }

        var now = _timeProvider.GetUtcNow();
        var removed = 0;
        foreach (var (digest, entry) in _sessions)
        {
            if ((now >= entry.ExpiresAt || now >= entry.AbsoluteExpiresAt)
                && TryRemove(digest, entry))
            {
                removed++;
            }
        }
        return removed;
    }

    private void CleanupExpiredSseTickets()
    {
        if (_disposed)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        foreach (var (digest, ticket) in _sseTickets)
        {
            if (now >= ticket.ExpiresAt)
            {
                _sseTickets.TryRemove(digest, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer.Dispose();
        _sseTicketCleanupTimer.Dispose();
        _sseTickets.Clear();
        foreach (var entry in _sessions.Values)
        {
            Cancel(entry.Revoked);
        }
        _sessions.Clear();
    }

    public static string ExtractPresentedToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization[7..].Trim();
        }

        return string.Empty;
    }

    private bool TryRemove(string digest, SessionEntry entry)
    {
        var removed = ((ICollection<KeyValuePair<string, SessionEntry>>)_sessions)
            .Remove(new KeyValuePair<string, SessionEntry>(digest, entry));
        if (removed)
        {
            Cancel(entry.Revoked);
            RemoveTicketsForSession(digest);
        }
        return removed;
    }

    private void RemoveTicketsForSession(string sessionDigest)
    {
        foreach (var (ticketDigest, ticket) in _sseTickets)
        {
            if (ticket.SessionDigest == sessionDigest)
            {
                _sseTickets.TryRemove(ticketDigest, out _);
            }
        }
    }

    private static void Cancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool TryGetDigest(string? token, out string digest)
    {
        digest = string.Empty;
        if (token is null || token.Length != TokenEncodedLength
            || token.Any(character => !(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-' or '_')))
        {
            return false;
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/') + "=";
            var tokenBytes = Convert.FromBase64String(base64);
            if (tokenBytes.Length != TokenByteLength || EncodeBase64Url(tokenBytes) != token)
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
                return false;
            }

            digest = GetDigest(tokenBytes);
            CryptographicOperations.ZeroMemory(tokenBytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string GetDigest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;

    private sealed record SessionEntry(
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSeenAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset AbsoluteExpiresAt,
        CancellationTokenSource Revoked);

    private sealed record SseTicketEntry(string SessionDigest, DateTimeOffset ExpiresAt);
}

public sealed record WebSessionToken(string Token, DateTimeOffset ExpiresAt);

public sealed record WebSseTicket(string Token, DateTimeOffset ExpiresAt);

public sealed record WebSessionSnapshot(
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    CancellationToken RevocationToken,
    string SessionDigest);
