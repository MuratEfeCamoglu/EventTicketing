using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.Api.Infrastructure.Persistence;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Features.Payments;

public sealed class PaymentTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-10.1")]
    public async Task PayReservation_SuccessToken_Returns200ConfirmsAndIssuesOneTicketPerSeat()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var customer = await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);
        var charges = Factory.PaymentGateway().ChargeCount;

        var response = await Client.PayAsync(reservationId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("status").GetString().ShouldBe("Succeeded");
        var paymentId = body.GetProperty("paymentId").GetGuid();
        var tickets = body.GetProperty("tickets").EnumerateArray().ToList();
        tickets.Count.ShouldBe(2);
        tickets.Select(t => t.GetProperty("seatLabel").GetString()).ShouldBe(["Balcony-A-1", "Balcony-A-2"]);
        Factory.PaymentGateway().ChargeCount.ShouldBe(charges + 1);

        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Confirmed);
        (await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => s.Id == seatIds[0] || s.Id == seatIds[1]).ToListAsync(Ct)))
            .ShouldAllBe(s => s.Status == EventSeatStatus.Sold);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId, Ct));
        (payment.Status, payment.Amount).ShouldBe((PaymentStatus.Succeeded, 2 * EventHelper.BalconyPrice));
        payment.ProviderReference.ShouldStartWith("mock_");
        (await WithDbAsync(db => db.Tickets.AsNoTracking().Where(t => t.ReservationId == reservationId).ToListAsync(Ct)))
            .ShouldAllBe(t => t.UserId == customer.Id && t.Code.Length == Ticket.CodeLength);
    }

    [Fact]
    [Trait("AC", "AC-10.2")]
    public async Task PayReservation_DeclinedToken_Returns402AndReleasesSeats()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await Client.PayAsync(reservationId, MockPaymentGateway.DeclinedToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.PaymentRequired, "payment.declined");
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.PaymentFailed);
        var seat = await WithDbAsync(db => db.EventSeats.AsNoTracking().SingleAsync(s => s.Id == seatIds[0], Ct));
        (seat.Status, seat.HoldReservationId, seat.HeldUntil).ShouldBe((EventSeatStatus.Available, null, null));
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.ReservationId == reservationId, Ct));
        (payment.Status, payment.FailureCode).ShouldBe((PaymentStatus.Failed, MockPaymentGateway.DeclinedFailureCode));
        (await WithDbAsync(db => db.Tickets.CountAsync(Ct))).ShouldBe(0);
    }

    [Theory]
    [Trait("AC", "AC-10.3")]
    [InlineData(MockPaymentGateway.SuccessToken, HttpStatusCode.OK)]
    [InlineData(MockPaymentGateway.DeclinedToken, HttpStatusCode.PaymentRequired)]
    public async Task PayReservation_SameKeyRepeated_ReturnsFirstResponseWithoutCharging(string token, HttpStatusCode expected)
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);
        var key = Guid.NewGuid();
        var charges = Factory.PaymentGateway().ChargeCount;

        var first = await Client.PayAsync(reservationId, token, key);
        var second = await Client.PayAsync(reservationId, token, key);

        first.StatusCode.ShouldBe(expected);
        second.StatusCode.ShouldBe(expected);
        var firstBody = await first.ReadJsonAsync();
        var secondBody = await second.ReadJsonAsync();
        if (expected == HttpStatusCode.OK)
        {
            secondBody.GetRawText().ShouldBe(firstBody.GetRawText());
        }
        else
        {
            secondBody.GetProperty("code").GetString().ShouldBe(firstBody.GetProperty("code").GetString());
        }

        Factory.PaymentGateway().ChargeCount.ShouldBe(charges + 1);
        (await WithDbAsync(db => db.Payments.CountAsync(p => p.IdempotencyKey == key, Ct))).ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-10.3")]
    public async Task PayReservation_SameKeyOtherReservation_Returns422()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var (otherEventId, otherSeatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var first = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        var second = await Client.HoldOrFailAsync(otherEventId, otherSeatIds[0]);
        var key = Guid.NewGuid();
        (await Client.PayAsync(first, idempotencyKey: key)).EnsureSuccessStatusCode();

        var response = await Client.PayAsync(second, idempotencyKey: key);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "payment.idempotency_key_reused");
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == second, Ct))).Status.ShouldBe(ReservationStatus.Pending);
    }

    [Fact]
    [Trait("AC", "AC-10.3")]
    public async Task PayReservation_SameKeyConcurrently_BothGetSameResultAndOnePaymentRow()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);
        Client.WithBearerToken(tokens.AccessToken);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);
        var key = Guid.NewGuid();

        using var barrier = new Barrier(2);
        var requests = Enumerable.Range(0, 2).Select(_ => Task.Factory.StartNew(
                async () =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{reservationId}/payments")
                    {
                        Content = JsonContent.Create(new { paymentToken = MockPaymentGateway.SuccessToken }),
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                    request.Headers.Add("Idempotency-Key", key.ToString());
                    barrier.SignalAndWait(Ct);
                    using var response = await Client.SendAsync(request, Ct);
                    return (response.StatusCode, Body: (await response.ReadJsonAsync()).GetRawText());
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())
            .ToList();
        var results = await Task.WhenAll(requests);

        results.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        results[1].Body.ShouldBe(results[0].Body);
        (await WithDbAsync(db => db.Payments.CountAsync(p => p.IdempotencyKey == key, Ct))).ShouldBe(1);
        (await WithDbAsync(db => db.Tickets.CountAsync(t => t.ReservationId == reservationId, Ct))).ShouldBe(2);
    }

    [Theory]
    [Trait("AC", "AC-10.4")]
    [InlineData(null)]
    [InlineData("not-a-uuid")]
    [InlineData("")]
    public async Task PayReservation_MissingOrInvalidIdempotencyKey_Returns400(string? key)
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        var charges = Factory.PaymentGateway().ChargeCount;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{reservationId}/payments")
        {
            Content = JsonContent.Create(new { paymentToken = MockPaymentGateway.SuccessToken }),
        };
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        var response = await Client.SendAsync(request, Ct);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").TryGetProperty("idempotencyKey", out _).ShouldBeTrue();
        Factory.PaymentGateway().ChargeCount.ShouldBe(charges);
    }

    [Fact]
    public async Task PayReservation_UnknownPaymentToken_Returns400()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await Client.PayAsync(reservationId, "tok_unknown");

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").TryGetProperty("paymentToken", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-10.5")]
    public async Task PayReservation_NotPending_Returns409InvalidStateWithoutCharging()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var cancelled = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await Client.DeleteAsync($"/api/v1/reservations/{cancelled}", Ct)).EnsureSuccessStatusCode();
        var confirmed = await Client.BuyAsync(eventId, seatIds[1]);
        var charges = Factory.PaymentGateway().ChargeCount;

        await (await Client.PayAsync(cancelled)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.invalid_state");
        await (await Client.PayAsync(confirmed)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.invalid_state");
        Factory.PaymentGateway().ChargeCount.ShouldBe(charges);
    }

    [Fact]
    [Trait("AC", "AC-10.5")]
    [Trait("AC", "AC-09.2")]
    public async Task PayReservation_Expired_Returns409ExpiredWithoutCharging()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var charges = Factory.PaymentGateway().ChargeCount;

        var response = await Client.PayAsync(reservationId);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.expired");
        Factory.PaymentGateway().ChargeCount.ShouldBe(charges);
        (await WithDbAsync(db => db.Payments.CountAsync(Ct))).ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-10.6")]
    public async Task PayReservation_ExpiresDuringCharge_RefundsRecordsRefundedAndReturns409Expired()
    {
        await using var expiringApi = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPaymentGateway>(provider => new ExpiringDuringChargeGateway(
                provider.GetRequiredService<MockPaymentGateway>(), provider.GetRequiredService<IServiceScopeFactory>(), Factory.Clock))));
        using var client = expiringApi.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);
        var key = Guid.NewGuid();
        var gateway = expiringApi.Services.GetRequiredService<MockPaymentGateway>();

        var response = await client.PayAsync(reservationId, idempotencyKey: key);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.expired");
        gateway.RefundCount.ShouldBe(1);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().SingleAsync(p => p.IdempotencyKey == key, Ct));
        (payment.Status, payment.ReservationId).ShouldBe((PaymentStatus.Refunded, reservationId));
        (await WithDbAsync(db => db.Tickets.CountAsync(Ct))).ShouldBe(0);
        (await WithDbAsync(db => db.EventSeats.AsNoTracking().SingleAsync(s => s.Id == seatIds[0], Ct))).Status.ShouldBe(EventSeatStatus.Available);

        var replay = await client.PayAsync(reservationId, idempotencyKey: key);

        await replay.ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.expired");
        gateway.ChargeCount.ShouldBe(1);
    }

    [Fact]
    public async Task PayReservation_OtherCustomersReservation_Returns404()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.PayAsync(reservationId);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    /// <summary>
    /// Charges successfully, but meanwhile moves every Pending reservation's expiry into the past and runs the sweeper's
    /// statements (ISKELET §5.8 step 2), as a real expiry racing the payment would.
    /// </summary>
    private sealed class ExpiringDuringChargeGateway(MockPaymentGateway inner, IServiceScopeFactory scopes, TimeProvider clock) : IPaymentGateway
    {
        public async Task<ChargeResult> ChargeAsync(decimal amount, string paymentToken, Guid idempotencyKey, CancellationToken cancellationToken)
        {
            var result = await inner.ChargeAsync(amount, paymentToken, idempotencyKey, cancellationToken);
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var past = clock.GetUtcNow().AddSeconds(-1);
            await db.Reservations.Where(r => r.Status == ReservationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, past), cancellationToken);
            var expired = await db.Reservations.Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= past).Select(r => (Guid?)r.Id).ToListAsync(cancellationToken);
            await db.Reservations.Where(r => expired.Contains(r.Id) && r.Status == ReservationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired), cancellationToken);
            await db.EventSeats.Where(s => expired.Contains(s.HoldReservationId) && s.Status == EventSeatStatus.Held)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Status, EventSeatStatus.Available).SetProperty(x => x.HoldReservationId, (Guid?)null).SetProperty(x => x.HeldUntil, (DateTimeOffset?)null),
                    cancellationToken);
            return result;
        }

        public Task RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken) =>
            inner.RefundAsync(providerReference, amount, cancellationToken);
    }
}
