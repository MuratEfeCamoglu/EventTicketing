using QRCoder;

namespace EventTicketing.Notifications.Qr;

/// <summary>Renders the ticket QR code as PNG (QRCoder); a copy of the API's generator, as ISKELET §7 keeps no shared library.</summary>
public static class QrCodeGenerator
{
    private const int PixelsPerModule = 10;

    /// <summary>QR content of a ticket, <c>ETKT:{ticketCode}</c> (AC-11.2); the same format as the API's <c>Ticket.QrContent</c>.</summary>
    public static string TicketContent(string ticketCode) => $"ETKT:{ticketCode}";

    public static byte[] GeneratePng(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
