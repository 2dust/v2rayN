namespace ServiceLib.Models.Dto;

public sealed class SubscriptionRefreshResult
{
    public IReadOnlyList<string> UpdatedSubscriptionIds { get; init; } = [];
    public IReadOnlyList<string> UnavailableActiveProfileIds { get; init; } = [];
    public bool Success => UpdatedSubscriptionIds.Count > 0;
    public bool ActiveProfileUnavailable => UnavailableActiveProfileIds.Count > 0;
}
