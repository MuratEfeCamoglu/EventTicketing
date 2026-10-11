namespace EventTicketing.Api.Domain;

public sealed class Reservation
{
    private readonly List<ReservationSeat> _seats = [];

    private Reservation()
    {
    }

    public Reservation(Guid userId, Guid eventId, IReadOnlyCollection<EventSeat> seats, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        UserId = userId;
        EventId = eventId;
        Status = ReservationStatus.Pending;
        ExpiresAt = expiresAt;
        CreatedAt = now;
        UpdatedAt = now;
        foreach (var seat in seats)
        {
            _seats.Add(new ReservationSeat(Id, seat.Id, seat.Price));
        }

        TotalAmount = _seats.Sum(s => s.Price);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid EventId { get; private set; }

    public ReservationStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Mapped to PostgreSQL <c>xmin</c> (ISKELET §6).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<ReservationSeat> Seats => _seats;
}
