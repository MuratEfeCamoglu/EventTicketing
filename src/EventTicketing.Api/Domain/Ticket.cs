namespace EventTicketing.Api.Domain;

public sealed class Ticket
{
    private Ticket()
    {
    }

    public Ticket(Guid reservationId, Guid eventSeatId, Guid userId, string code, DateTimeOffset issuedAt)
    {
        Id = Guid.CreateVersion7(issuedAt);
        ReservationId = reservationId;
        EventSeatId = eventSeatId;
        UserId = userId;
        Code = code;
        IssuedAt = issuedAt;
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    /// <summary>Unique in the database (<c>ux_tickets_event_seat_id</c>): the last line of defence against double selling (AC-10.7).</summary>
    public Guid EventSeatId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>26-character Crockford Base32 (AC-11.2).</summary>
    public string Code { get; private set; } = null!;

    public DateTimeOffset IssuedAt { get; private set; }
}
