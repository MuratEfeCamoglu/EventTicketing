using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>
/// Published → Cancelled (AC-05.5): refused once a ticket is sold; otherwise the Pending reservations of the event end
/// and their seats are released.
/// </summary>
public static class CancelEvent
{
    public sealed class Handler(AppDbContext db, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<EventResponse>> HandleAsync(Guid eventId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Exclusive row lock: seat holds take FOR SHARE on the event, so none can slip in between loading the
            // Pending reservations below and committing the cancellation.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM events WHERE id = {eventId} FOR UPDATE", cancellationToken);
            var published = await db.Events.Include(e => e.SectionPrices).SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (published is null)
            {
                return DomainErrors.NotFound;
            }

            if (published.EnsureCanBeChangedBy(userId, isAdmin) is { IsSuccess: false } denied)
            {
                return denied.Error;
            }

            var now = clock.GetUtcNow();
            var cancelled = published.Cancel(now);
            if (!cancelled.IsSuccess)
            {
                return cancelled.Error;
            }

            // Pending reservations are loaded before the sold-seat check: a payment that commits after this point changes
            // a loaded reservation, so SaveChanges fails on its xmin instead of cancelling an event with a sold ticket.
            var pending = await db.Reservations.Where(r => r.EventId == eventId && r.Status == ReservationStatus.Pending).ToListAsync(cancellationToken);
            if (await db.EventSeats.AnyAsync(s => s.EventId == eventId && s.Status == EventSeatStatus.Sold, cancellationToken))
            {
                return DomainErrors.Events.HasSoldTickets;
            }

            foreach (var reservation in pending)
            {
                _ = reservation.StatusAt(now) == ReservationStatus.Pending ? reservation.Cancel(now) : reservation.Expire(now);
            }

            var reservationIds = pending.Select(r => (Guid?)r.Id).ToList();
            var heldSeats = await db.EventSeats
                .Where(s => s.Status == EventSeatStatus.Held && reservationIds.Contains(s.HoldReservationId))
                .ToListAsync(cancellationToken);
            foreach (var seat in heldSeats)
            {
                seat.Release();
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Event {EventId} cancelled; {ReservationCount} pending reservations ended", eventId, pending.Count);
            return EventResponse.From(published);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/events/{id:guid}/cancel", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), cancellationToken)).ToHttpResult(TypedResults.Ok))
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
