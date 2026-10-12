using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Events;

public sealed class SeatMapTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-07.1")]
    public async Task GetSeatMap_Published_ReturnsEverySeatWithStatus()
    {
        var eventId = await CreatePublishedEventAsync();
        var (held, sold) = await HoldOneAndSellOneAsync(eventId, holdFor: TimeSpan.FromMinutes(10));

        var response = await Client.GetAsync($"/api/v1/events/{eventId}/seats", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seats = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        seats.Count.ShouldBe(EventHelper.HallSeatCount);
        seats[0].EnumerateObject().Select(p => p.Name).ShouldBe(["eventSeatId", "section", "row", "number", "price", "status"]);
        var byId = seats.ToDictionary(s => s.GetProperty("eventSeatId").GetGuid(), s => s.GetProperty("status").GetString());
        byId[held].ShouldBe("Held");
        byId[sold].ShouldBe("Sold");
        byId.Values.Count(s => s == "Available").ShouldBe(EventHelper.HallSeatCount - 2);
        var floorA1 = seats.Single(s => s.GetProperty("section").GetString() == "Floor" && s.GetProperty("row").GetString() == "A" && s.GetProperty("number").GetInt32() == 1);
        floorA1.GetProperty("price").GetDecimal().ShouldBe(EventHelper.FloorPrice);
    }

    [Fact]
    [Trait("AC", "AC-07.2")]
    public async Task GetSeatMap_HoldExpired_ReturnsAvailable()
    {
        var eventId = await CreatePublishedEventAsync();
        var (held, _) = await HoldOneAndSellOneAsync(eventId, holdFor: TimeSpan.FromMinutes(10));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var seats = (await (await Client.GetAsync($"/api/v1/events/{eventId}/seats", Ct)).ReadJsonAsync()).EnumerateArray();

        seats.Single(s => s.GetProperty("eventSeatId").GetGuid() == held).GetProperty("status").GetString().ShouldBe("Available");
        (await WithDbAsync(db => db.EventSeats.AsNoTracking().SingleAsync(s => s.Id == held, Ct))).Status.ShouldBe(EventSeatStatus.Held);
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public async Task GetSeatMap_DraftOrUnknownEvent_Returns404()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var draft = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));

        await (await Client.GetAsync($"/api/v1/events/{draft}/seats", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        await (await Client.GetAsync($"/api/v1/events/{Guid.CreateVersion7()}/seats", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public async Task GetSeatMap_CancelledEvent_Returns404()
    {
        var eventId = await CreatePublishedEventAsync();
        (await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct)).EnsureSuccessStatusCode();

        await (await Client.GetAsync($"/api/v1/events/{eventId}/seats", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    private async Task<Guid> CreatePublishedEventAsync()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        return await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
    }

    private async Task<(Guid Held, Guid Sold)> HoldOneAndSellOneAsync(Guid eventId, TimeSpan holdFor)
    {
        var customer = await Factory.CreateUserAsync(UserRole.Customer);
        return await WithDbAsync(async db =>
        {
            var now = Factory.Clock.GetUtcNow();
            var seats = await db.EventSeats.Where(s => s.EventId == eventId).OrderBy(s => s.Id).Take(2).ToListAsync(Ct);
            var reservation = new Reservation(customer.Id, eventId, [seats[0]], now.Add(holdFor), now);
            seats[0].Hold(reservation.Id, reservation.ExpiresAt, now).IsSuccess.ShouldBeTrue();
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(Ct);
            await db.EventSeats.Where(s => s.Id == seats[1].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EventSeatStatus.Sold), Ct);
            return (seats[0].Id, seats[1].Id);
        });
    }
}
