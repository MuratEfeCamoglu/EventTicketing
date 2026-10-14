using System.Net;
using System.Text.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.Api.Infrastructure.Persistence;
using EventTicketing.IntegrationTests.Fixtures;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Features.Payments;

/// <summary>F-17: <c>TicketPurchased</c> goes into <c>outbox_message</c> in the payment transaction (ISKELET §5.10).</summary>
public sealed class TicketPurchasedOutboxTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string TicketPurchasedUrn = "urn:message:EventTicketing.Contracts:TicketPurchased";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-17.1")]
    public async Task PayReservation_Succeeds_StoresTicketPurchasedInOutbox()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var customer = await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[1], seatIds[0]);

        var response = await Client.PayAsync(reservationId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var outbox = (await WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct))).ShouldHaveSingleItem();
        outbox.MessageType.ShouldContain(TicketPurchasedUrn);
        var message = JsonDocument.Parse(outbox.Body).RootElement.GetProperty("message");
        message.GetProperty("reservationId").GetGuid().ShouldBe(reservationId);
        message.GetProperty("userId").GetGuid().ShouldBe(customer.Id);
        message.GetProperty("email").GetString().ShouldBe(customer.Email);
        message.GetProperty("eventTitle").GetString().ShouldBe("Event 00");

        var tickets = await WithDbAsync(db => db.Tickets.AsNoTracking().Where(t => t.ReservationId == reservationId).ToDictionaryAsync(t => t.Id, t => t.Code, Ct));
        var published = message.GetProperty("tickets").EnumerateArray()
            .Select(t => (Id: t.GetProperty("ticketId").GetGuid(), Label: t.GetProperty("seatLabel").GetString(), Code: t.GetProperty("ticketCode").GetString()))
            .ToList();
        published.Select(t => t.Label).ShouldBe(["Balcony-A-1", "Balcony-A-2"]);
        published.ShouldAllBe(t => tickets[t.Id] == t.Code);
    }

    [Fact]
    [Trait("AC", "AC-17.1")]
    public async Task PayReservation_ConfirmationRolledBack_LeavesOutboxEmpty()
    {
        // The reservation row changes during the charge: the confirmation publishes, then loses on xmin and rolls back.
        await using var racingApi = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPaymentGateway>(provider => new TouchingReservationDuringChargeGateway(
                provider.GetRequiredService<MockPaymentGateway>(), provider.GetRequiredService<IServiceScopeFactory>()))));
        using var client = racingApi.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await client.PayAsync(reservationId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Factory.Logs.Events.ShouldContain(e => e.MessageTemplate.Text == "Confirmation of reservation {ReservationId} lost a concurrency race");
        (await WithDbAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.ReservationId == reservationId, Ct))).Status.ShouldBe(PaymentStatus.Refunded);
        (await WithDbAsync(db => db.Tickets.CountAsync(Ct))).ShouldBe(0);
        (await WithDbAsync(db => db.Set<OutboxMessage>().CountAsync(Ct))).ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-17.1")]
    public async Task PayReservation_Declined_LeavesOutboxEmpty()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await Client.PayAsync(reservationId, MockPaymentGateway.DeclinedToken);

        response.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
        (await WithDbAsync(db => db.Set<OutboxMessage>().CountAsync(Ct))).ShouldBe(0);
    }

    /// <summary>Charges successfully and meanwhile rewrites every reservation row, which gives each a new <c>xmin</c>.</summary>
    private sealed class TouchingReservationDuringChargeGateway(MockPaymentGateway inner, IServiceScopeFactory scopes) : IPaymentGateway
    {
        public async Task<ChargeResult> ChargeAsync(decimal amount, string paymentToken, Guid idempotencyKey, CancellationToken cancellationToken)
        {
            var result = await inner.ChargeAsync(amount, paymentToken, idempotencyKey, cancellationToken);
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Reservations.ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAt, r => r.UpdatedAt), cancellationToken);
            return result;
        }

        public Task RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken) =>
            inner.RefundAsync(providerReference, amount, cancellationToken);
    }
}
