using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Seat map of a Published event; an expired hold reads as Available (F-07).</summary>
public static class GetSeatMap
{
    public sealed record SeatDto(Guid EventSeatId, string Section, string Row, int Number, decimal Price, EventSeatStatus Status);

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public async Task<Result<IReadOnlyList<SeatDto>>> HandleAsync(Guid eventId, CancellationToken cancellationToken)
        {
            if (!await db.Events.AnyAsync(e => e.Id == eventId && e.Status == EventStatus.Published, cancellationToken))
            {
                return DomainErrors.NotFound;
            }

            var seats = await (
                    from es in db.EventSeats.AsNoTracking()
                    join s in db.Seats on es.SeatId equals s.Id
                    where es.EventId == eventId
                    orderby s.Section, s.Row, s.Number
                    select new { es.Id, s.Section, s.Row, s.Number, es.Price, es.Status, es.HeldUntil })
                .ToListAsync(cancellationToken);

            var now = clock.GetUtcNow();
            return seats
                .Select(s => new SeatDto(s.Id, s.Section, s.Row, s.Number, s.Price, EventSeat.EffectiveStatus(s.Status, s.HeldUntil, now)))
                .ToList();
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
