using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace EventTicketing.Api.Features.Events;

/// <summary>Seat map of a Published event; an expired hold reads as Available (F-07).</summary>
public static class GetSeatMap
{
    public sealed record SeatDto(Guid EventSeatId, string Section, string Row, int Number, decimal Price, EventSeatStatus Status);

    /// <summary>Stored status and hold end; the expiry rule is applied after reading, so a cached hold still expires on time.</summary>
    public sealed record CachedSeat(Guid EventSeatId, string Section, string Row, int Number, decimal Price, EventSeatStatus Status, DateTimeOffset? HeldUntil);

    public sealed class Handler(AppDbContext db, HybridCache cache, TimeProvider clock)
    {
        public async Task<Result<IReadOnlyList<SeatDto>>> HandleAsync(Guid eventId, CancellationToken cancellationToken)
        {
            var seats = await cache.GetOrCreateAsync(
                EventCache.SeatsKey(eventId),
                ct => LoadAsync(eventId, ct),
                EventCache.Seats,
                [EventCache.EventsTag, EventCache.EventTag(eventId), EventCache.SeatsTag(eventId)],
                cancellationToken);
            if (seats is null)
            {
                return DomainErrors.NotFound;
            }

            var now = clock.GetUtcNow();
            return seats
                .Select(s => new SeatDto(s.EventSeatId, s.Section, s.Row, s.Number, s.Price, EventSeat.EffectiveStatus(s.Status, s.HeldUntil, now)))
                .ToList();
        }

        /// <summary>Null when the event does not exist or is not Published.</summary>
        private async ValueTask<List<CachedSeat>?> LoadAsync(Guid eventId, CancellationToken cancellationToken)
        {
            if (!await db.Events.AnyAsync(e => e.Id == eventId && e.Status == EventStatus.Published, cancellationToken))
            {
                return null;
            }

            return await (
                    from es in db.EventSeats.AsNoTracking()
                    join s in db.Seats on es.SeatId equals s.Id
                    where es.EventId == eventId
                    orderby s.Section, s.Row, s.Number
                    select new CachedSeat(es.Id, s.Section, s.Row, s.Number, es.Price, es.Status, es.HeldUntil))
                .ToListAsync(cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/events/{id:guid}/seats", async (Guid id, Handler handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AllowAnonymous()
                .WithTags("Events");
    }
}
