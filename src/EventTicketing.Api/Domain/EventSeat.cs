namespace EventTicketing.Api.Domain;

public sealed class EventSeat
{
    private EventSeat()
    {
    }

    internal EventSeat(Guid eventId, Guid seatId, decimal price)
    {
        Id = Guid.CreateVersion7();
        EventId = eventId;
        SeatId = seatId;
        Price = price;
        Status = EventSeatStatus.Available;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid SeatId { get; private set; }

    public decimal Price { get; private set; }

    public EventSeatStatus Status { get; private set; }

    public Guid? HoldReservationId { get; private set; }

    public DateTimeOffset? HeldUntil { get; private set; }

    /// <summary>Mapped to PostgreSQL <c>xmin</c>; the optimistic concurrency guard against double booking (ISKELET §5.4).</summary>
    public uint RowVersion { get; private set; }
}
