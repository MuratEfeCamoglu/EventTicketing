using System.Net;
using System.Text;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Reservations;

public sealed class ReservationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task CreateReservation_AvailableSeats_Returns201AndHoldsSeatsForTenMinutes()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var now = Factory.Clock.GetUtcNow();
        // Balcony A-1 (450) and Floor B-5 (750): seat ids are ordered by section, row and number.
        Guid[] chosen = [seatIds[0], seatIds[^1]];

        var response = await Client.HoldAsync(eventId, chosen);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var reservationId = body.GetProperty("reservationId").GetGuid();
        body.GetProperty("status").GetString().ShouldBe("Pending");
        body.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(now.AddMinutes(10), TimeSpan.FromMilliseconds(1));
        body.GetProperty("totalAmount").GetDecimal().ShouldBe(EventHelper.FloorPrice + EventHelper.BalconyPrice);
        body.GetProperty("seats").EnumerateArray().Select(s => s.GetProperty("eventSeatId").GetGuid()).Order().ShouldBe(chosen.Order());
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/reservations/{reservationId}");

        var seats = await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => chosen.Contains(s.Id)).ToListAsync(Ct));
        seats.ShouldAllBe(s => s.Status == EventSeatStatus.Held && s.HoldReservationId == reservationId);
        seats.ShouldAllBe(s => s.HeldUntil!.Value.Subtract(now.AddMinutes(10)).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    [Trait("AC", "AC-08.2")]
    public async Task CreateReservation_OneSeatTaken_Returns409WithUnavailableIdsAndChangesNothing()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, seatIds[1], seatIds[0], seatIds[2]);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "seat.unavailable");
        body.GetProperty("unavailableSeatIds").EnumerateArray().Select(e => e.GetGuid()).ShouldBe([seatIds[0]]);
        var untouched = await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => s.Id == seatIds[1] || s.Id == seatIds[2]).ToListAsync(Ct));
        untouched.ShouldAllBe(s => s.Status == EventSeatStatus.Available && s.HoldReservationId == null);
        (await WithDbAsync(db => db.Reservations.CountAsync(Ct))).ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task CreateReservation_UnexpiredPendingExists_Returns409AlreadyPending()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);

        var response = await Client.HoldAsync(eventId, seatIds[1]);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.already_pending");
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task CreateReservation_PreviousPendingExpired_Returns201AndExpiresPrevious()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var previous = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var response = await Client.HoldAsync(eventId, seatIds[1], seatIds[2]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == previous, Ct))).Status.ShouldBe(ReservationStatus.Expired);
        var released = await WithDbAsync(db => db.EventSeats.AsNoTracking().SingleAsync(s => s.Id == seatIds[0], Ct));
        (released.Status, released.HoldReservationId).ShouldBe((EventSeatStatus.Available, null));
    }

    [Theory]
    [Trait("AC", "AC-08.5")]
    [InlineData(0)]
    [InlineData(7)]
    public async Task CreateReservation_SeatCountOutOfRange_Returns400(int count)
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, [.. seatIds.Take(count)]);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-08.5")]
    public async Task CreateReservation_SixSeats_Returns201()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, [.. seatIds.Take(6)]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "AC-08.5")]
    public async Task CreateReservation_RepeatedSeatId_Returns400()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, seatIds[0], seatIds[0]);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-08.5")]
    public async Task CreateReservation_SeatOfAnotherEventOrUnknown_Returns400NotInEvent()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var (_, otherSeatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await (await Client.HoldAsync(eventId, seatIds[0], otherSeatIds[0])).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "seat.not_in_event");
        await (await Client.HoldAsync(eventId, Guid.CreateVersion7())).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "seat.not_in_event");
        (await WithDbAsync(db => db.EventSeats.AsNoTracking().SingleAsync(s => s.Id == seatIds[0], Ct))).Status.ShouldBe(EventSeatStatus.Available);
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public async Task CreateReservation_DraftOrCancelledEvent_Returns409NotOnSale()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var draft = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));
        var cancelled = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
        var cancelledSeat = (await Factory.GetEventSeatIdsAsync(cancelled))[0];
        (await Client.PostAsync($"/api/v1/events/{cancelled}/cancel", null, Ct)).EnsureSuccessStatusCode();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await (await Client.HoldAsync(draft, Guid.CreateVersion7())).ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.not_on_sale");
        await (await Client.HoldAsync(cancelled, cancelledSeat)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.not_on_sale");
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public async Task CreateReservation_EventStarted_Returns409NotOnSale()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync(startsIn: TimeSpan.FromMinutes(5));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, seatIds[0]);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.not_on_sale");
    }

    [Fact]
    public async Task CreateReservation_UnknownEvent_Returns404()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-08.7")]
    public async Task CancelReservation_Pending_Returns204AndReleasesSeats()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[1]);

        var response = await Client.DeleteAsync($"/api/v1/reservations/{reservationId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Cancelled);
        var seats = await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => s.Id == seatIds[0] || s.Id == seatIds[1]).ToListAsync(Ct));
        seats.ShouldAllBe(s => s.Status == EventSeatStatus.Available && s.HoldReservationId == null && s.HeldUntil == null);
    }

    [Fact]
    [Trait("AC", "AC-08.7")]
    public async Task CancelReservation_AlreadyCancelledOrExpired_Returns409InvalidState()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var cancelled = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await Client.DeleteAsync($"/api/v1/reservations/{cancelled}", Ct)).EnsureSuccessStatusCode();
        var expired = await Client.HoldOrFailAsync(eventId, seatIds[1]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        await (await Client.DeleteAsync($"/api/v1/reservations/{cancelled}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.invalid_state");
        await (await Client.DeleteAsync($"/api/v1/reservations/{expired}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "reservation.invalid_state");
    }

    [Fact]
    [Trait("AC", "AC-08.7")]
    public async Task CancelReservation_OtherCustomers_Returns404()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.DeleteAsync($"/api/v1/reservations/{reservationId}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Pending);
    }

    [Fact]
    [Trait("AC", "AC-09.1")]
    public async Task CreateReservation_TenMinutesOneSecondAfterHold_AnotherCustomerGetsSeat()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, seatIds[0]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "AC-09.1")]
    public async Task CreateReservation_JustBeforeHoldExpires_AnotherCustomerIsRefused()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.HoldAsync(eventId, seatIds[0]);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "seat.unavailable");
    }

    [Fact]
    [Trait("AC", "AC-09.2")]
    public async Task GetReservation_PendingPastExpiry_ShowsExpired()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var body = await (await Client.GetAsync($"/api/v1/reservations/{reservationId}", Ct)).ReadJsonAsync();

        body.GetProperty("status").GetString().ShouldBe("Expired");
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Pending);
    }

    [Fact]
    [Trait("AC", "AC-13.2")]
    public async Task GetReservation_Owner_Returns200WithSeats()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0], seatIds[^1]);

        var response = await Client.GetAsync($"/api/v1/reservations/{reservationId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("reservationId").GetGuid().ShouldBe(reservationId);
        body.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        body.GetProperty("status").GetString().ShouldBe("Pending");
        var seats = body.GetProperty("seats").EnumerateArray().ToList();
        seats.Select(s => $"{s.GetProperty("section").GetString()}-{s.GetProperty("row").GetString()}-{s.GetProperty("number").GetInt32()}")
            .ShouldBe(["Balcony-A-1", "Floor-B-5"]);
    }

    [Fact]
    [Trait("AC", "AC-13.2")]
    public async Task GetReservation_Admin_Returns200()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.GetAsync($"/api/v1/reservations/{reservationId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-03.3")]
    public async Task GetReservation_OtherCustomers_Returns404()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        await (await Client.GetAsync($"/api/v1/reservations/{reservationId}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        await (await Client.GetAsync($"/api/v1/reservations/{Guid.CreateVersion7()}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-13.1")]
    public async Task ListMyReservations_ReturnsOnlyCallersReservationsPaged()
    {
        var (firstEvent, firstSeats) = await Factory.CreateEventOnSaleAsync();
        var (secondEvent, secondSeats) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(firstEvent, firstSeats[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        var older = await Client.HoldOrFailAsync(firstEvent, firstSeats[1], firstSeats[2]);
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var newer = await Client.HoldOrFailAsync(secondEvent, secondSeats[0]);

        var body = await (await Client.GetAsync("/api/v1/me/reservations?pageSize=1", Ct)).ReadJsonAsync();
        var secondPage = await (await Client.GetAsync("/api/v1/me/reservations?pageSize=1&page=2", Ct)).ReadJsonAsync();

        body.GetProperty("totalCount").GetInt32().ShouldBe(2);
        var item = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("reservationId").GetGuid().ShouldBe(newer);
        item.GetProperty("eventTitle").GetString().ShouldNotBeNullOrEmpty();
        secondPage.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("seatCount").GetInt32().ShouldBe(2);
        secondPage.GetProperty("items")[0].GetProperty("reservationId").GetGuid().ShouldBe(older);
    }

    [Fact]
    public async Task CreateReservation_EmptyBody_Returns400()
    {
        var (eventId, _) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/reservations", new StringContent("{}", Encoding.UTF8, "application/json"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }
}
