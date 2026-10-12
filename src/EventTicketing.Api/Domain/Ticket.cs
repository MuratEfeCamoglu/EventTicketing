using System.Security.Cryptography;

namespace EventTicketing.Api.Domain;

public sealed class Ticket
{
    public const int CodeLength = 26;
    public const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

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

    /// <summary>Text encoded in a ticket's QR code (AC-11.2).</summary>
    public static string QrContent(string code) => $"ETKT:{code}";

    /// <summary>26 random Crockford Base32 characters (130 bits) from <see cref="RandomNumberGenerator"/> (AC-11.2).</summary>
    public static string GenerateCode() => new(RandomNumberGenerator.GetItems<char>(CrockfordAlphabet, CodeLength));
}
