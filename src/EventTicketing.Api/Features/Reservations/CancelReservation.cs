using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Reservations;

/// <summary>The owner cancels a Pending reservation and its seats become Available (AC-08.7).</summary>
public static class CancelReservation
{
    public sealed class Handler(AppDbContext db, EventCacheInvalidator cache, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result> HandleAsync(Guid reservationId, Guid userId, CancellationToken cancellationToken)
        {
            var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, cancellationToken);
            if (reservation is null)
            {
                return DomainErrors.NotFound;
            }

            var cancelled = reservation.Cancel(clock.GetUtcNow());
            if (!cancelled.IsSuccess)
            {
                return cancelled;
            }

            var seats = await db.EventSeats
                .Where(s => s.HoldReservationId == reservationId && s.Status == EventSeatStatus.Held)
                .ToListAsync(cancellationToken);
            foreach (var seat in seats)
            {
                seat.Release();
            }

            // A payment confirming the same reservation concurrently changes its xmin: SaveChanges then fails with
            // DbUpdateConcurrencyException (→ 409 concurrency.conflict) instead of releasing sold seats.
            await db.SaveChangesAsync(cancellationToken);
            await cache.SeatsChangedAsync(reservation.EventId, cancellationToken);
            logger.LogInformation("Reservation {ReservationId} cancelled by its owner", reservationId);
            return Result.Success();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapDelete("/reservations/{id:guid}", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), cancellationToken)).ToHttpResult(TypedResults.NoContent))
                .RequireAuthorization(Policies.Customer)
                .WithTags("Reservations");
    }
}
