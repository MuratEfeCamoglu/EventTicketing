using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Features.Reservations;

/// <summary>
/// Holds 1–6 seats for <c>HoldMinutes</c> (ISKELET §5.6). Double booking is prevented by PostgreSQL, not by this code
/// path: every seat update is <c>UPDATE … WHERE xmin = @read</c>, so of two requests that read the same Available seat
/// only the first commit wins and the second gets <see cref="DbUpdateConcurrencyException"/> → 409 (AC-08.3).
/// </summary>
public static class CreateReservation
{
    public sealed record Request(IReadOnlyList<Guid> EventSeatIds);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator(IOptions<ReservationOptions> options)
        {
            var max = options.Value.MaxSeatsPerReservation;
            RuleLevelCascadeMode = CascadeMode.Stop;
            RuleFor(r => r.EventSeatIds)
                .NotEmpty()
                .Must(ids => ids.Count <= max).WithMessage($"At most {max} seats can be reserved at once.")
                .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Seat ids must not repeat.");
            RuleForEach(r => r.EventSeatIds).NotEmpty();
        }
    }

    public sealed class Handler(AppDbContext db, EventCacheInvalidator cache, TimeProvider clock, IOptions<ReservationOptions> options, ILogger<Handler> logger)
    {
        public async Task<Result<ReservationResponse>> HandleAsync(Guid eventId, Guid userId, Request request, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Shared row lock: a concurrent CancelEvent (FOR UPDATE) either waits for this hold to commit or is seen here.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM events WHERE id = {eventId} FOR SHARE", cancellationToken);
            var target = await db.Events.AsNoTracking()
                .Where(e => e.Id == eventId)
                .Select(e => new { e.Status, e.StartsAt })
                .SingleOrDefaultAsync(cancellationToken);
            if (target is null)
            {
                return DomainErrors.NotFound;
            }

            if (!Event.IsOnSale(target.Status, target.StartsAt, now))
            {
                return DomainErrors.Events.NotOnSale;
            }

            if (await ExpirePreviousAsync(eventId, userId, now, cancellationToken) is { } pending)
            {
                return pending;
            }

            var seats = await db.EventSeats.Where(s => request.EventSeatIds.Contains(s.Id)).ToListAsync(cancellationToken);
            if (seats.Count != request.EventSeatIds.Count || seats.Any(s => s.EventId != eventId))
            {
                return DomainErrors.Seats.NotInEvent;
            }

            var reservation = new Reservation(userId, eventId, seats, now.AddMinutes(options.Value.HoldMinutes), now);
            var unavailable = seats.Where(s => !s.Hold(reservation.Id, reservation.ExpiresAt, now).IsSuccess).Select(s => s.Id).ToList();
            if (unavailable.Count > 0)
            {
                return DomainErrors.Seats.Unavailable(unavailable);
            }

            db.Reservations.Add(reservation);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // Another request changed one of the seats after we read it (ISKELET §5.4 layer 2).
                var lost = exception.Entries.Select(e => e.Entity).OfType<EventSeat>().Select(s => s.Id).ToList();
                logger.LogInformation("Seat hold lost a concurrency race on event {EventId}", eventId);
                return DomainErrors.Seats.Unavailable(lost.Count > 0 ? lost : request.EventSeatIds);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation("ix_reservations_user_event_pending"))
            {
                // A concurrent request of the same user created the Pending reservation first.
                return DomainErrors.Reservations.AlreadyPending;
            }

            await cache.SeatsChangedAsync(eventId, cancellationToken);
            logger.LogInformation("Reservation {ReservationId} holds {SeatCount} seats of event {EventId} until {ExpiresAt}", reservation.Id, seats.Count, eventId, reservation.ExpiresAt);
            return new ReservationResponse(
                reservation.Id,
                eventId,
                reservation.Status,
                reservation.ExpiresAt,
                reservation.TotalAmount,
                reservation.CreatedAt,
                await ReservedSeats.LoadAsync(db, reservation.Id, cancellationToken));
        }

        /// <summary>
        /// One Pending reservation per user and event (AC-08.4): an unexpired one blocks the request; an expired one becomes
        /// Expired and releases the seats it still holds. Saved before the new reservation is inserted, so the partial
        /// unique index never sees two Pending rows.
        /// </summary>
        private async Task<Error?> ExpirePreviousAsync(Guid eventId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var previous = await db.Reservations.SingleOrDefaultAsync(
                r => r.UserId == userId && r.EventId == eventId && r.Status == ReservationStatus.Pending, cancellationToken);
            if (previous is null)
            {
                return null;
            }

            if (!previous.Expire(now).IsSuccess)
            {
                return DomainErrors.Reservations.AlreadyPending;
            }

            var stillHeld = await db.EventSeats
                .Where(s => s.HoldReservationId == previous.Id && s.Status == EventSeatStatus.Held)
                .ToListAsync(cancellationToken);
            foreach (var seat in stillHeld)
            {
                seat.Release();
            }

            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/events/{id:guid}/reservations", async (Guid id, Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), request, cancellationToken))
                        .ToHttpResult(response => TypedResults.Created($"{EndpointRegistration.ApiPrefix}/reservations/{response.ReservationId}", response)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.Customer)
                .WithTags("Reservations");
    }
}
