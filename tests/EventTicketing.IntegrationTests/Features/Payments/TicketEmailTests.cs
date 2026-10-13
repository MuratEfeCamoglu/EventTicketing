using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Features.Payments;

public sealed class TicketEmailTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-12.1")]
    public async Task PayReservation_Succeeded_SendsOneEmailWithOnePngPerTicket()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var title = await WithDbAsync(db => Task.FromResult(db.Events.Single(e => e.Id == eventId).Title));
        var customer = await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await Client.BuyAsync(eventId, seatIds[0], seatIds[1], seatIds[2]);

        var message = (await Mailpit.MessagesToAsync(customer.Email, Ct)).ShouldHaveSingleItem();
        message.Subject.ShouldContain(title);
        message.Attachments.Count.ShouldBe(3);
        message.Attachments.ShouldAllBe(a => a.ContentType == "image/png");
    }

    [Fact]
    [Trait("AC", "AC-12.1")]
    public async Task PayReservation_RepeatedWithSameKey_DoesNotSendAgain()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var customer = await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        var key = Guid.NewGuid();

        (await Client.PayAsync(reservationId, idempotencyKey: key)).EnsureSuccessStatusCode();
        (await Client.PayAsync(reservationId, idempotencyKey: key)).EnsureSuccessStatusCode();

        (await Mailpit.MessagesToAsync(customer.Email, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-12.2")]
    public async Task PayReservation_SmtpUnreachable_Returns200AndLogsErrorWithReservationId()
    {
        await using var noSmtpApi = Factory.WithWebHostBuilder(builder => builder.UseSetting("Smtp:Port", ClosedPort.Find().ToString()));
        using var client = noSmtpApi.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await client.PayAsync(reservationId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Logs.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Error
            && e.Properties.ContainsKey("ReservationId")
            && e.Properties["ReservationId"].ToString().Contains(reservationId.ToString(), StringComparison.Ordinal));
    }
}
