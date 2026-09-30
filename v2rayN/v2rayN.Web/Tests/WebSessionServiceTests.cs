using v2rayN.Web.Security;

namespace v2rayN.Web.Tests;

public class WebSessionServiceTests
{
    [Test]
    public async Task SessionTokensAreRandomUrlSafeAndManagementKeysAreNotSessions()
    {
        using var sessions = new WebSessionService();
        var first = sessions.CreateSession();
        var second = sessions.CreateSession();
        const string managementKey = "this-is-a-management-key";

        await (first.Token.Length == 43).Should().BeTrue();
        await first.Token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Should().BeTrue();
        await (first.Token != second.Token).Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(first.Token, out _).Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(managementKey, out _).Should().BeFalse();
    }

    [Test]
    public async Task SessionsSlideForSevenDaysAndExpireAfterIdleTimeout()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        var created = sessions.CreateSession();

        time.Advance(TimeSpan.FromDays(6));
        await sessions.TryValidateAndRenew(created.Token, out var renewed).Should().BeTrue();
        await (renewed!.LastSeenAt == time.GetUtcNow()).Should().BeTrue();
        await (renewed.ExpiresAt == time.GetUtcNow() + WebSessionService.SlidingLifetime).Should().BeTrue();

        time.Advance(TimeSpan.FromDays(8));
        await sessions.TryValidateWithoutRenewal(created.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task SessionHasThirtyDayAbsoluteLifetimeEvenWhenActive()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        var startedAt = time.GetUtcNow();
        var created = sessions.CreateSession();

        for (var day = 0; day < 5; day++)
        {
            time.Advance(TimeSpan.FromDays(5));
            await sessions.TryValidateAndRenew(created.Token, out _).Should().BeTrue();
        }

        await (time.GetUtcNow() == startedAt + TimeSpan.FromDays(25)).Should().BeTrue();
        await sessions.TryValidateAndRenew(created.Token, out var active).Should().BeTrue();
        await (active!.AbsoluteExpiresAt == startedAt + WebSessionService.AbsoluteLifetime).Should().BeTrue();
        time.Advance(TimeSpan.FromDays(5));
        await sessions.TryValidateWithoutRenewal(created.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task RevocationCancelsActiveConnectionsAndOnlyRevokesThatSession()
    {
        using var sessions = new WebSessionService();
        var first = sessions.CreateSession();
        var second = sessions.CreateSession();
        await sessions.TryValidateAndRenew(first.Token, out var activeSession).Should().BeTrue();

        await sessions.Revoke(first.Token).Should().BeTrue();
        await activeSession!.RevocationToken.IsCancellationRequested.Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(first.Token, out _).Should().BeFalse();
        await sessions.TryValidateWithoutRenewal(second.Token, out _).Should().BeTrue();
    }

    [Test]
    public async Task SseTicketsAreRandomShortLivedOneTimeAndCannotAuthenticateRest()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-20T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        var session = sessions.CreateSession();
        time.Advance(TimeSpan.FromDays(6));

        await sessions.TryCreateSseTicket(session.Token, out var ticket).Should().BeTrue();
        await (ticket!.Token.Length == 43).Should().BeTrue();
        await ticket.Token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Should().BeTrue();
        await (ticket.Token != session.Token).Should().BeTrue();
        await (ticket.ExpiresAt == time.GetUtcNow() + WebSessionService.SseTicketLifetime).Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(ticket.Token, out _).Should().BeFalse();

        await sessions.TryConsumeSseTicket(ticket.Token, out var consumed).Should().BeTrue();
        await (consumed!.LastSeenAt == session.ExpiresAt - WebSessionService.SlidingLifetime).Should().BeTrue();
        await sessions.TryConsumeSseTicket(ticket.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task ExpiredTicketsAndRevokedSessionsCannotOpenOrCreateSseTickets()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-25T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        var session = sessions.CreateSession();
        await sessions.TryCreateSseTicket(session.Token, out var ticket).Should().BeTrue();

        await sessions.Revoke(session.Token).Should().BeTrue();
        await sessions.TryCreateSseTicket(session.Token, out _).Should().BeFalse();
        await sessions.TryConsumeSseTicket(ticket!.Token, out _).Should().BeFalse();

        var secondSession = sessions.CreateSession();
        await sessions.TryCreateSseTicket(secondSession.Token, out var expiringTicket).Should().BeTrue();
        time.Advance(WebSessionService.SseTicketLifetime + TimeSpan.FromSeconds(1));
        await sessions.TryConsumeSseTicket(expiringTicket!.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task CleanupRemovesExpiredSessions()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-01T00:00:00Z"));
        using var sessions = new WebSessionService(time);
        var session = sessions.CreateSession();
        await sessions.TryValidateAndRenew(session.Token, out var active).Should().BeTrue();

        time.Advance(WebSessionService.SlidingLifetime + TimeSpan.FromSeconds(1));
        await (sessions.CleanupExpiredSessions() == 1).Should().BeTrue();
        await active!.RevocationToken.IsCancellationRequested.Should().BeTrue();
        await sessions.TryValidateWithoutRenewal(session.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task SessionsDoNotSurviveServiceRestart()
    {
        var firstProcess = new WebSessionService();
        var session = firstProcess.CreateSession();
        firstProcess.Dispose();

        using var restartedProcess = new WebSessionService();
        await restartedProcess.TryValidateWithoutRenewal(session.Token, out _).Should().BeFalse();
    }

    [Test]
    public async Task PassiveHeartbeatValidationDoesNotRenewAndExpiresTheSession()
    {
        var startedAt = DateTimeOffset.Parse("2026-03-15T00:00:00Z");
        var time = new ManualTimeProvider(startedAt);
        using var sessions = new WebSessionService(time);
        var created = sessions.CreateSession();
        WebSessionSnapshot? lastValidSnapshot = null;

        for (var minute = 1; minute < 7 * 24 * 60; minute++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            await sessions.TryValidateWithoutRenewal(created.Token, out lastValidSnapshot).Should().BeTrue();
        }

        time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        await sessions.TryValidateWithoutRenewal(created.Token, out _).Should().BeFalse();
        await (sessions.CleanupExpiredSessions() == 0).Should().BeTrue();
        await (lastValidSnapshot!.LastSeenAt == startedAt).Should().BeTrue();
        await (lastValidSnapshot.ExpiresAt == created.ExpiresAt).Should().BeTrue();
        await lastValidSnapshot.RevocationToken.IsCancellationRequested.Should().BeTrue();
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _now = initialTime;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }
}
