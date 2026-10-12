using EventTicketing.Api.Common;

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

    /// <summary>A Pending reservation past <see cref="ExpiresAt"/> reads as Expired (ISKELET §5.5).</summary>
    public ReservationStatus StatusAt(DateTimeOffset now) => EffectiveStatus(Status, ExpiresAt, now);

    /// <summary>The §5.5 read rule for projections that do not load the entity.</summary>
    public static ReservationStatus EffectiveStatus(ReservationStatus status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == ReservationStatus.Pending && expiresAt <= now ? ReservationStatus.Expired : status;

    /// <summary>Pending (not yet expired) → Cancelled: by its owner (AC-08.7) or with its event (AC-05.5).</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (StatusAt(now) != ReservationStatus.Pending)
        {
            return DomainErrors.Reservations.InvalidState;
        }

        Status = ReservationStatus.Cancelled;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Pending (not yet expired) → Confirmed after a successful payment (ISKELET §5.7 step 5).</summary>
    public Result Confirm(DateTimeOffset now)
    {
        var status = StatusAt(now);
        if (status != ReservationStatus.Pending)
        {
            return status == ReservationStatus.Expired ? DomainErrors.Reservations.Expired : DomainErrors.Reservations.InvalidState;
        }

        Status = ReservationStatus.Confirmed;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Why a payment cannot start: Expired → <c>reservation.expired</c>, any other non-Pending state → <c>reservation.invalid_state</c>.</summary>
    public Result EnsurePayable(DateTimeOffset now) => StatusAt(now) switch
    {
        ReservationStatus.Pending => Result.Success(),
        ReservationStatus.Expired => DomainErrors.Reservations.Expired,
        _ => DomainErrors.Reservations.InvalidState,
    };

    /// <summary>Pending past <see cref="ExpiresAt"/> → Expired (lazy expiry, ISKELET §5.8).</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Pending || ExpiresAt > now)
        {
            return DomainErrors.Reservations.InvalidState;
        }

        Status = ReservationStatus.Expired;
        UpdatedAt = now;
        return Result.Success();
    }
}
