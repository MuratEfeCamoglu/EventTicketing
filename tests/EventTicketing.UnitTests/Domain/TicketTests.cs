using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Qr;

namespace EventTicketing.UnitTests.Domain;

public sealed class TicketTests
{
    [Fact]
    [Trait("AC", "AC-11.2")]
    public void QrContent_PrefixesTicketCode() => Ticket.QrContent("0123456789ABCDEFGHJKMNPQRS").ShouldBe("ETKT:0123456789ABCDEFGHJKMNPQRS");

    [Fact]
    [Trait("AC", "AC-11.2")]
    public void GenerateCode_Returns26CrockfordCharacters()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => Ticket.GenerateCode()).ToList();

        codes.ShouldAllBe(c => c.Length == 26 && c.All(ch => Ticket.CrockfordAlphabet.Contains(ch)));
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Fact]
    [Trait("AC", "AC-11.1")]
    public void GeneratePng_StartsWithPngSignature() =>
        QrCodeGenerator.GeneratePng(Ticket.QrContent(Ticket.GenerateCode())).Take(8).ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
}
