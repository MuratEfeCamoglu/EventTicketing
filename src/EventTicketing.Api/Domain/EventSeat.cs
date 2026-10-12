using EventTicketing.Api.Common;

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

    /// <summary>A hold whose <see cref="HeldUntil"/> has passed counts as Available (ISKELET §5.5).</summary>
    public EventSeatStatus StatusAt(DateTimeOffset now) => EffectiveStatus(Status, HeldUntil, now);

    /// <summary>The §5.5 read rule for projections that do not load the entity.</summary>
    public static EventSeatStatus EffectiveStatus(EventSeatStatus status, DateTimeOffset? heldUntil, DateTimeOffset now) =>
        status == EventSeatStatus.Held && heldUntil <= now ? EventSeatStatus.Available : status;

    /// <summary>Available (or expired hold) → Held for <paramref name="reservationId"/> until <paramref name="until"/>.</summary>
    public Result Hold(Guid reservationId, DateTimeOffset until, DateTimeOffset now)
    {
        if (StatusAt(now) != EventSeatStatus.Available)
        {
            return DomainErrors.Seats.Unavailable([Id]);
        }

        Status = EventSeatStatus.Held;
        HoldReservationId = reservationId;
        HeldUntil = until;
        return Result.Success();
    }

    /// <summary>Held for <paramref name="reservationId"/> → Sold (ISKELET §5.7 step 5).</summary>
    public Result Sell(Guid reservationId)
    {
        if (Status != EventSeatStatus.Held || HoldReservationId != reservationId)
        {
            return DomainErrors.Seats.Unavailable([Id]);
        }

        Status = EventSeatStatus.Sold;
        HoldReservationId = null;
        HeldUntil = null;
        return Result.Success();
    }

    /// <summary>Held → Available (cancel, declined payment, expiry). A seat no longer held is left as it is.</summary>
    public void Release()
    {
        if (Status != EventSeatStatus.Held)
        {
            return;
        }

        Status = EventSeatStatus.Available;
        HoldReservationId = null;
        HeldUntil = null;
    }
}
