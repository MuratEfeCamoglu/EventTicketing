using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Features.Payments;

/// <summary>F-12 end to end in v3: API → outbox → RabbitMQ → Notification service → Mailpit (ISKELET §5.10).</summary>
public sealed class TicketEmailTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-12.1")]
    public async Task PayReservation_Succeeded_SendsOneEmailWithOnePngPerTicket()
    {
        await using var notifications = await NotificationsService.StartAsync(Factory);
        await using var api = Factory.WithOutboxDelivery();
        using var client = api.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var title = await WithDbAsync(db => Task.FromResult(db.Events.Single(e => e.Id == eventId).Title));
        var customer = await client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await client.BuyAsync(eventId, seatIds[0], seatIds[1], seatIds[2]);

        var message = (await Mailpit.WaitForMessagesToAsync(customer.Email, 1, DeliveryTimeout, Ct)).ShouldHaveSingleItem();
        message.Subject.ShouldContain(title);
        message.Attachments.Count.ShouldBe(3);
        message.Attachments.ShouldAllBe(a => a.ContentType == "image/png");
    }

    [Fact]
    [Trait("AC", "AC-12.1")]
    public async Task PayReservation_RepeatedWithSameKey_DoesNotSendAgain()
    {
        await using var notifications = await NotificationsService.StartAsync(Factory);
        await using var api = Factory.WithOutboxDelivery();
        using var client = api.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var customer = await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);
        var key = Guid.NewGuid();

        (await client.PayAsync(reservationId, idempotencyKey: key)).EnsureSuccessStatusCode();
        await Mailpit.WaitForMessagesToAsync(customer.Email, 1, DeliveryTimeout, Ct);
        (await client.PayAsync(reservationId, idempotencyKey: key)).EnsureSuccessStatusCode();

        // The replay publishes nothing: one message was ever stored, and it has been delivered.
        (await WithDbAsync(db => Task.FromResult(db.Payments.Count(p => p.ReservationId == reservationId)))).ShouldBe(1);
        (await Mailpit.MessagesToAsync(customer.Email, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-12.2")]
    public async Task PayReservation_SmtpUnreachable_Returns200AndLogsErrorWithReservationId()
    {
        await using var notifications = await NotificationsService.StartAsync(Factory, new Dictionary<string, string?> { ["Smtp:Port"] = ClosedPort.Find().ToString() });
        await using var api = Factory.WithOutboxDelivery();
        using var client = api.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await client.PayAsync(reservationId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await Eventually.WaitForAsync(
            () => Task.FromResult(Factory.Logs.Events.Any(e => e.Level == LogEventLevel.Error && HasReservationId(e, reservationId))),
            logged => logged,
            DeliveryTimeout,
            "an Error log line with the reservation id");
    }

    private static bool HasReservationId(LogEvent logEvent, Guid reservationId) =>
        logEvent.Properties.TryGetValue("ReservationId", out var value) && value.ToString().Contains(reservationId.ToString(), StringComparison.Ordinal);
}
