using Microsoft.Extensions.Caching.Hybrid;

namespace EventTicketing.Api.Infrastructure.Caching;

/// <summary>
/// Removes stale event entries after a committed write (ISKELET §5.9). Fail-open: when Redis is unreachable the write has
/// already succeeded in PostgreSQL, so the failure is logged and the remaining entries age out by their TTL instead of
/// failing the request (ISKELET §5.4, AC-14.3, AC-15.4).
/// </summary>
public sealed class EventCacheInvalidator(HybridCache cache, ILogger<EventCacheInvalidator> logger)
{
    /// <summary>After an event write: its list entries, detail and seat map are stale.</summary>
    public Task EventChangedAsync(Guid eventId, CancellationToken cancellationToken) =>
        RemoveAsync(eventId, [EventCache.EventsTag, EventCache.EventTag(eventId), EventCache.SeatsTag(eventId)], cancellationToken);

    /// <summary>After a seat state change of the event (hold, release, sale, expiry).</summary>
    public Task SeatsChangedAsync(Guid eventId, CancellationToken cancellationToken) =>
        RemoveAsync(eventId, [EventCache.SeatsTag(eventId)], cancellationToken);

    private async Task RemoveAsync(Guid eventId, string[] tags, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveByTagAsync(tags, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache invalidation for event {EventId} failed; entries expire by their TTL", eventId);
        }
    }
}
