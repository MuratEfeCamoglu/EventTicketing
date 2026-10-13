using EventTicketing.Api.Domain;
using EventTicketing.Api.Features.Reservations;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Features.Reservations;

public sealed class ReservationExpiryTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ReservationExpiryService Sweeper => Factory.Services.GetRequiredService<ReservationExpiryService>();

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task SweepOnceAsync_ExpiredPending_ExpiresAndFreesOnlyItsOwnSeats()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var expired = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        // Another customer takes over one of the expired seats before the sweep runs.
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var takeover = await Client.HoldOrFailAsync(eventId, seatIds[1]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var active = await Client.HoldOrFailAsync(eventId, seatIds[2]);
        using var counter = new CounterListener("reservations_expired_total");

        var swept = await Sweeper.SweepOnceAsync(Ct);

        swept.ShouldBe(1);
        counter.Total.ShouldBe(1);
        var statuses = await WithDbAsync(db => db.Reservations.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Status, Ct));
        statuses[expired].ShouldBe(ReservationStatus.Expired);
        statuses[takeover].ShouldBe(ReservationStatus.Pending);
        statuses[active].ShouldBe(ReservationStatus.Pending);

        Guid[] touched = [seatIds[0], seatIds[1], seatIds[2]];
        var seats = await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => touched.Contains(s.Id)).ToDictionaryAsync(s => s.Id, Ct));
        (seats[seatIds[0]].Status, seats[seatIds[0]].HoldReservationId, seats[seatIds[0]].HeldUntil).ShouldBe((EventSeatStatus.Available, null, null));
        (seats[seatIds[1]].Status, seats[seatIds[1]].HoldReservationId).ShouldBe((EventSeatStatus.Held, takeover));
        (seats[seatIds[2]].Status, seats[seatIds[2]].HoldReservationId).ShouldBe((EventSeatStatus.Held, active));
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task SweepOnceAsync_NothingExpired_ChangesNothing()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        (await Sweeper.SweepOnceAsync(Ct)).ShouldBe(0);

        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Pending);
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task SweepOnceAsync_MoreThanOneBatch_RepeatsUntilAllExpired()
    {
        const int count = ReservationExpiryService.BatchSize + 1;
        var (eventId, _) = await Factory.CreateEventOnSaleAsync();
        await WithDbAsync(async db =>
        {
            var now = Factory.Clock.GetUtcNow();
            for (var i = 0; i < count; i++)
            {
                var user = new User($"sweep-{i}@example.com", "Sweep", UserRole.Customer, now);
                user.SetPasswordHash("not-a-real-hash");
                db.Users.Add(user);
                db.Reservations.Add(new Reservation(user.Id, eventId, [], now.AddMinutes(-1), now.AddMinutes(-11)));
            }

            return await db.SaveChangesAsync(Ct);
        });

        var swept = await Sweeper.SweepOnceAsync(Ct);

        swept.ShouldBe(count);
        (await WithDbAsync(db => db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending, Ct))).ShouldBe(0);
    }
}
