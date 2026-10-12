using QRCoder;

namespace EventTicketing.Api.Infrastructure.Qr;

/// <summary>Renders text as a PNG QR code (QRCoder).</summary>
public static class QrCodeGenerator
{
    private const int PixelsPerModule = 10;

    public static byte[] GeneratePng(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
