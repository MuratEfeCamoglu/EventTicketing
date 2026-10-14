using EventTicketing.Contracts;
using EventTicketing.Notifications.Qr;

namespace EventTicketing.Notifications.Email;

/// <summary>The ticket e-mail (F-12): subject with the event title, one <c>image/png</c> QR attachment per ticket.</summary>
public static class TicketEmail
{
    public static EmailMessage Create(TicketPurchased purchase)
    {
        var lines = purchase.Tickets.Select(t => $"- {t.SeatLabel}: {t.TicketCode}");
        return new EmailMessage(
            purchase.Email,
            purchase.FullName,
            $"Your tickets for {purchase.EventTitle}",
            $"Hello {purchase.FullName},{Environment.NewLine}{Environment.NewLine}"
                + $"{purchase.EventTitle} · {purchase.EventStartsAt:u} · {purchase.HallName}{Environment.NewLine}"
                + string.Join(Environment.NewLine, lines),
            purchase.Tickets
                .Select(t => new EmailAttachment($"ticket-{t.SeatLabel}.png", "image/png", QrCodeGenerator.GeneratePng(QrCodeGenerator.TicketContent(t.TicketCode))))
                .ToList());
    }
}
