using Microsoft.Extensions.Caching.Hybrid;

namespace EventTicketing.Api.Infrastructure.Caching;

/// <summary>
/// Keys, tags and lifetimes of the cached event reads (ISKELET §5.9). The cache only serves read endpoints; no write
/// decision is ever taken from it (AGENT.md §5).
/// </summary>
public static class EventCache
{
    /// <summary>Every list and detail entry; invalidated by any event write.</summary>
    public const string EventsTag = "events";

    public static readonly HybridCacheEntryOptions List = new() { Expiration = TimeSpan.FromSeconds(60), LocalCacheExpiration = TimeSpan.FromSeconds(10) };

    public static readonly HybridCacheEntryOptions Detail = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromSeconds(10) };

    public static readonly HybridCacheEntryOptions Seats = new() { Expiration = TimeSpan.FromSeconds(5), LocalCacheExpiration = TimeSpan.FromSeconds(2) };

    public static string ListKey(string normalizedQuery) => $"events:list:{normalizedQuery}";

    public static string DetailKey(Guid eventId) => $"events:{eventId}";

    public static string SeatsKey(Guid eventId) => $"events:{eventId}:seats";

    public static string EventTag(Guid eventId) => $"event:{eventId}";

    /// <summary>Seat map of one event; invalidated by holds, cancellations, payments, expiry and event cancellation.</summary>
    public static string SeatsTag(Guid eventId) => $"event:{eventId}:seats";
}
