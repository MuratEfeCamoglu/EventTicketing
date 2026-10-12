using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Reservations;

/// <summary>Owner or Admin; anyone else gets 404 so the reservation's existence does not leak (AC-03.3, AC-13.2).</summary>
public static class GetReservation
{
    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public async Task<Result<ReservationResponse>> HandleAsync(Guid reservationId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
        {
            var reservation = await db.Reservations.AsNoTracking()
                .Where(r => r.Id == reservationId && (isAdmin || r.UserId == userId))
                .Select(r => new { r.Id, r.EventId, r.Status, r.ExpiresAt, r.TotalAmount, r.CreatedAt })
                .SingleOrDefaultAsync(cancellationToken);
            if (reservation is null)
            {
                return DomainErrors.NotFound;
            }

            return new ReservationResponse(
                reservation.Id,
                reservation.EventId,
                Reservation.EffectiveStatus(reservation.Status, reservation.ExpiresAt, clock.GetUtcNow()),
                reservation.ExpiresAt,
                reservation.TotalAmount,
                reservation.CreatedAt,
                await ReservedSeats.LoadAsync(db, reservation.Id, cancellationToken));
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/reservations/{id:guid}", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), cancellationToken)).ToHttpResult(TypedResults.Ok))
                .RequireAuthorization(Policies.CustomerOrAdmin)
                .WithTags("Reservations");
    }
}
