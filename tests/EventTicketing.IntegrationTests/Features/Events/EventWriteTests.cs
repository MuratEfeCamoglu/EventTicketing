using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Events;

public sealed class EventWriteTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-05.1")]
    public async Task CreateEvent_AsOrganizer_Returns201DraftOwnedByCaller()
    {
        var hall = await Factory.CreateHallAsync();
        var organizer = await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.PostAsJsonAsync("/api/v1/events", Factory.EventRequest(hall.HallId, title: "Jazz Night", category: "Concert"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("status").GetString().ShouldBe("Draft");
        body.GetProperty("organizerId").GetGuid().ShouldBe(organizer.Id);
        body.GetProperty("title").GetString().ShouldBe("Jazz Night");
        body.GetProperty("category").GetString().ShouldBe("Concert");
        body.GetProperty("sectionPrices").GetArrayLength().ShouldBe(2);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/events/{id}");
        (await WithDbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == id, Ct))).Status.ShouldBe(EventStatus.Draft);
    }

    [Theory]
    [Trait("AC", "AC-05.1")]
    [InlineData("Concert")]
    [InlineData("Theatre")]
    [InlineData("Sports")]
    [InlineData("Conference")]
    [InlineData("Other")]
    public async Task CreateEvent_EachCategory_Returns201(string category)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.PostAsJsonAsync("/api/v1/events", Factory.EventRequest(hall.HallId, category: category), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [Trait("AC", "AC-05.1")]
    [InlineData("category", "Party")]
    [InlineData("category", "7")]
    [InlineData("title", "")]
    [InlineData("title", "201")]
    [InlineData("description", "4001")]
    public async Task CreateEvent_InvalidField_Returns400(string field, string value)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = ToMutableJson(Factory.EventRequest(hall.HallId));
        request[field] = field switch
        {
            "category" when int.TryParse(value, out var number) => number,
            "title" or "description" when int.TryParse(value, out var length) => new string('x', length),
            _ => value,
        };

        var response = await Client.PostAsJsonAsync("/api/v1/events", request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    public async Task CreateEvent_LongestValidTexts_Returns201()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = ToMutableJson(Factory.EventRequest(hall.HallId));
        request["title"] = new string('t', 200);
        request["description"] = new string('d', 4000);

        var response = await Client.PostAsJsonAsync("/api/v1/events", request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public static TheoryData<string> InvalidSchedulesAndPrices() => ["startsNow", "startsInPast", "endsAtStart", "endsBeforeStart", "zeroPrice", "negativePrice", "priceOverMax"];

    [Theory]
    [Trait("AC", "AC-05.2")]
    [MemberData(nameof(InvalidSchedulesAndPrices))]
    public async Task CreateEvent_InvalidScheduleOrPrice_Returns400(string scenario)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var now = Factory.Clock.GetUtcNow();
        var request = ToMutableJson(Factory.EventRequest(hall.HallId));
        var startsAt = Factory.WholeSecondNow().AddDays(30);
        switch (scenario)
        {
            case "startsNow":
                request["startsAt"] = now;
                break;
            case "startsInPast":
                request["startsAt"] = now.AddMinutes(-1);
                break;
            case "endsAtStart":
                request["endsAt"] = startsAt;
                break;
            case "endsBeforeStart":
                request["endsAt"] = startsAt.AddHours(-1);
                break;
            default:
                var price = scenario switch { "zeroPrice" => 0m, "negativePrice" => -1m, _ => 100_000.01m };
                request["sectionPrices"] = new object[] { new { section = "Floor", price }, new { section = "Balcony", price = 10m } };
                break;
        }

        var response = await Client.PostAsJsonAsync("/api/v1/events", request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    public async Task CreateEvent_MaximumPrice_Returns201()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.PostAsJsonAsync("/api/v1/events", Factory.EventRequest(hall.HallId, sectionPrices: [new { section = "Floor", price = 100_000m }]), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateEvent_UnknownHallOrSection_Returns400()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var unknownHall = await Client.PostAsJsonAsync("/api/v1/events", Factory.EventRequest(Guid.CreateVersion7()), Ct);
        var unknownSection = await Client.PostAsJsonAsync("/api/v1/events", Factory.EventRequest(hall.HallId, sectionPrices: [new { section = "VIP", price = 10m }]), Ct);

        (await unknownHall.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors").TryGetProperty("hallId", out _).ShouldBeTrue();
        (await unknownSection.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors").TryGetProperty("sectionPrices", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    public async Task PublishEvent_AllSectionsPriced_Returns200AndCreatesAvailableEventSeats()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/publish", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Published");
        var seats = await WithDbAsync(db => (
                from es in db.EventSeats.AsNoTracking()
                join s in db.Seats on es.SeatId equals s.Id
                where es.EventId == eventId
                select new { s.Section, es.Price, es.Status })
            .ToListAsync(Ct));
        seats.Count.ShouldBe(EventHelper.HallSeatCount);
        seats.ShouldAllBe(s => s.Status == EventSeatStatus.Available);
        seats.Where(s => s.Section == "Floor").ShouldAllBe(s => s.Price == EventHelper.FloorPrice);
        seats.Where(s => s.Section == "Balcony").ShouldAllBe(s => s.Price == EventHelper.BalconyPrice);
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    public async Task PublishEvent_SectionWithoutPrice_Returns400MissingSectionPrice()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId, sectionPrices: [new { section = "Floor", price = 100m }]));

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/publish", null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "event.missing_section_price");
        (await WithDbAsync(db => db.EventSeats.CountAsync(s => s.EventId == eventId, Ct))).ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    public async Task PublishEvent_NotDraft_Returns409InvalidState()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/publish", null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.invalid_state");
        (await WithDbAsync(db => db.EventSeats.CountAsync(s => s.EventId == eventId, Ct))).ShouldBe(EventHelper.HallSeatCount);
    }

    [Fact]
    [Trait("AC", "AC-05.4")]
    public async Task UpdateEvent_Draft_Returns200AndChangesAllFields()
    {
        var hall = await Factory.CreateHallAsync();
        var otherHall = await Factory.CreateHallAsync(venueName: "Other Venue");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));
        var startsAt = Factory.WholeSecondNow().AddDays(60);

        var response = await Client.PutAsJsonAsync($"/api/v1/events/{eventId}", new
        {
            hallId = otherHall.HallId,
            title = "New title",
            description = "New description",
            category = "Theatre",
            startsAt,
            endsAt = startsAt.AddHours(1),
            sectionPrices = new[] { new { section = "Floor", price = 99.5m } },
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await WithDbAsync(db => db.Events.AsNoTracking().Include(e => e.SectionPrices).SingleAsync(e => e.Id == eventId, Ct));
        (saved.HallId, saved.Title, saved.Description, saved.Category, saved.StartsAt).ShouldBe((otherHall.HallId, "New title", "New description", EventCategory.Theatre, startsAt));
        saved.SectionPrices.Select(p => (p.Section, p.Price)).ShouldBe([("Floor", 99.5m)]);
    }

    [Fact]
    [Trait("AC", "AC-05.4")]
    public async Task UpdateEvent_PublishedTitleAndDescription_Returns200()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = ToMutableJson(Factory.EventRequest(hall.HallId));
        var eventId = await Client.CreatePublishedEventAsync(request);
        request["title"] = "Renamed";
        request["description"] = "Updated description";

        var response = await Client.PutAsJsonAsync($"/api/v1/events/{eventId}", request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await WithDbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId, Ct));
        (saved.Title, saved.Description, saved.Status).ShouldBe(("Renamed", "Updated description", EventStatus.Published));
    }

    [Theory]
    [Trait("AC", "AC-05.4")]
    [InlineData("hallId")]
    [InlineData("startsAt")]
    [InlineData("endsAt")]
    [InlineData("sectionPrices")]
    public async Task UpdateEvent_PublishedScheduleHallOrPrices_Returns409InvalidState(string field)
    {
        var hall = await Factory.CreateHallAsync();
        var otherHall = await Factory.CreateHallAsync(venueName: "Other Venue");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = ToMutableJson(Factory.EventRequest(hall.HallId));
        var eventId = await Client.CreatePublishedEventAsync(request);
        var startsAt = ((JsonElement)request["startsAt"]!).GetDateTimeOffset();
        request[field] = field switch
        {
            "hallId" => otherHall.HallId,
            "startsAt" => startsAt.AddHours(-1),
            "endsAt" => startsAt.AddHours(5),
            _ => new object[] { new { section = "Floor", price = 1m }, new { section = "Balcony", price = 1m } },
        };

        var response = await Client.PutAsJsonAsync($"/api/v1/events/{eventId}", request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.invalid_state");
        (await WithDbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId, Ct))).StartsAt.ShouldBe(startsAt);
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task CancelEvent_WithPendingReservation_Returns200AndReleasesSeats()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
        var customer = await Factory.CreateUserAsync(UserRole.Customer);
        var (reservationId, seatIds) = await HoldSeatsAsync(eventId, customer.Id, 2);

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Cancelled");
        (await WithDbAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, Ct))).Status.ShouldBe(ReservationStatus.Cancelled);
        var seats = await WithDbAsync(db => db.EventSeats.AsNoTracking().Where(s => seatIds.Contains(s.Id)).ToListAsync(Ct));
        seats.ShouldAllBe(s => s.Status == EventSeatStatus.Available && s.HoldReservationId == null && s.HeldUntil == null);
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task CancelEvent_WithSoldTicket_Returns409HasSoldTickets()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
        await WithDbAsync(db => db.EventSeats.Where(s => s.EventId == eventId).Take(1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EventSeatStatus.Sold), Ct));

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.has_sold_tickets");
        (await WithDbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId, Ct))).Status.ShouldBe(EventStatus.Published);
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task CancelEvent_Draft_Returns409InvalidState()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));

        var response = await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.invalid_state");
    }

    [Fact]
    [Trait("AC", "AC-05.6")]
    public async Task DeleteEvent_Draft_Returns204AndRemovesEventWithPrices()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));

        var response = await Client.DeleteAsync($"/api/v1/events/{eventId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.Events.AnyAsync(e => e.Id == eventId, Ct))).ShouldBeFalse();
        (await WithDbAsync(db => db.EventSectionPrices.AnyAsync(p => p.EventId == eventId, Ct))).ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-05.6")]
    public async Task DeleteEvent_Published_Returns409InvalidState()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));

        var response = await Client.DeleteAsync($"/api/v1/events/{eventId}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "event.invalid_state");
        (await WithDbAsync(db => db.Events.AnyAsync(e => e.Id == eventId, Ct))).ShouldBeTrue();
    }

    [Theory]
    [Trait("AC", "AC-03.2")]
    [InlineData("PUT", "")]
    [InlineData("POST", "/publish")]
    [InlineData("POST", "/cancel")]
    [InlineData("DELETE", "")]
    public async Task ChangeEvent_OtherOrganizer_Returns403NotOwner(string method, string suffix)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = Factory.EventRequest(hall.HallId);
        var eventId = suffix == "/cancel" ? await Client.CreatePublishedEventAsync(request) : await Client.CreateEventAsync(request);
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/v1/events/{eventId}{suffix}") { Content = JsonContent.Create(request) }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "event.not_owner");
        var unchanged = await WithDbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId, Ct));
        unchanged.Status.ShouldBe(suffix == "/cancel" ? EventStatus.Published : EventStatus.Draft);
    }

    [Theory]
    [Trait("AC", "AC-03.2")]
    [InlineData("PUT", "")]
    [InlineData("POST", "/publish")]
    [InlineData("POST", "/cancel")]
    [InlineData("DELETE", "")]
    public async Task ChangeEvent_AdminOnOtherOrganizersEvent_Succeeds(string method, string suffix)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = Factory.EventRequest(hall.HallId);
        var eventId = suffix == "/cancel" ? await Client.CreatePublishedEventAsync(request) : await Client.CreateEventAsync(request);
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/v1/events/{eventId}{suffix}") { Content = JsonContent.Create(request) }, Ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("POST", "/publish")]
    [InlineData("POST", "/cancel")]
    [InlineData("DELETE", "")]
    public async Task ChangeEvent_Unknown_Returns404(string method, string suffix)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), $"/api/v1/events/{Guid.CreateVersion7()}{suffix}") { Content = JsonContent.Create(Factory.EventRequest(hall.HallId)) }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    /// <summary>Holds <paramref name="count"/> seats of the event for a new Pending reservation, as the reservation slice will.</summary>
    private Task<(Guid ReservationId, List<Guid> SeatIds)> HoldSeatsAsync(Guid eventId, Guid userId, int count) =>
        WithDbAsync(async db =>
        {
            var now = Factory.Clock.GetUtcNow();
            var seats = await db.EventSeats.Where(s => s.EventId == eventId).OrderBy(s => s.Id).Take(count).ToListAsync(Ct);
            var reservation = new Reservation(userId, eventId, seats, now.AddMinutes(10), now);
            foreach (var seat in seats)
            {
                seat.Hold(reservation.Id, reservation.ExpiresAt, now).IsSuccess.ShouldBeTrue();
            }

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(Ct);
            return (reservation.Id, seats.Select(s => s.Id).ToList());
        });

    private static Dictionary<string, object?> ToMutableJson(object request) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(request, JsonSerializerOptions.Web))!
            .ToDictionary(p => p.Key, p => (object?)p.Value);
}
