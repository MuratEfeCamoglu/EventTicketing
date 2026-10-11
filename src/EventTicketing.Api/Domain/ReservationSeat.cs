namespace EventTicketing.Api.Domain;

public sealed class ReservationSeat
{
    private ReservationSeat()
    {
    }

    internal ReservationSeat(Guid reservationId, Guid eventSeatId, decimal price)
    {
        ReservationId = reservationId;
        EventSeatId = eventSeatId;
        Price = price;
    }

    public Guid ReservationId { get; private set; }

    public Guid EventSeatId { get; private set; }

    public decimal Price { get; private set; }
}
