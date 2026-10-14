namespace EventTicketing.Contracts;

/// <summary>
/// Published by the API in the payment transaction through the EF outbox (ISKELET §5.10, AC-17.1); the Notification
/// service turns it into the ticket e-mail.
/// </summary>
public sealed record TicketPurchased(
    Guid ReservationId,
    Guid UserId,
    string Email,
    string FullName,
    string EventTitle,
    DateTimeOffset EventStartsAt,
    string HallName,
    IReadOnlyList<PurchasedTicket> Tickets,
    DateTimeOffset OccurredAt);

public sealed record PurchasedTicket(Guid TicketId, string SeatLabel, string TicketCode);
