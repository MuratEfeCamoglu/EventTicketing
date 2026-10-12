using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Reservations;

public sealed record ReservedSeatDto(Guid EventSeatId, string Section, string Row, int Number, decimal Price);

public static class ReservedSeats
{
    /// <summary>The seats of a reservation with their hall position, ordered by section, row and number.</summary>
    public static Task<List<ReservedSeatDto>> LoadAsync(AppDbContext db, Guid reservationId, CancellationToken cancellationToken) => (
            from rs in db.ReservationSeats.AsNoTracking()
            join es in db.EventSeats on rs.EventSeatId equals es.Id
            join s in db.Seats on es.SeatId equals s.Id
            where rs.ReservationId == reservationId
            orderby s.Section, s.Row, s.Number
            select new ReservedSeatDto(es.Id, s.Section, s.Row, s.Number, rs.Price))
        .ToListAsync(cancellationToken);
}

/// <summary>Reservation as its owner sees it; the status applies the expiry read rule (ISKELET §5.5).</summary>
public sealed record ReservationResponse(
    Guid ReservationId,
    Guid EventId,
    ReservationStatus Status,
    DateTimeOffset ExpiresAt,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReservedSeatDto> Seats);
