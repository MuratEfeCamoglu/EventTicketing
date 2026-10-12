using System.Net;
using System.Text.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Events;

public sealed class EventQueryTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-06.1")]
    public async Task ListEvents_ReturnsOnlyPublishedFutureEvents()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var upcoming = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Upcoming"));
        await Client.CreateEventAsync(Factory.EventRequest(hall.HallId, title: "Draft"));
        var cancelled = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Cancelled"));
        (await Client.PostAsync($"/api/v1/events/{cancelled}/cancel", null, Ct)).EnsureSuccessStatusCode();
        var started = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Started"));
        var now = Factory.Clock.GetUtcNow();
        await WithDbAsync(db => db.Events.Where(e => e.Id == started).ExecuteUpdateAsync(s => s.SetProperty(e => e.StartsAt, now), Ct));

        var body = await GetJsonAsync("/api/v1/events");

        Ids(body).ShouldBe([upcoming]);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["items", "page", "pageSize", "totalCount", "totalPages"]);
    }

    [Fact]
    [Trait("AC", "AC-06.2")]
    public async Task ListEvents_NoPageSize_Returns20PerPage()
    {
        var (organizer, hall) = await CreateOrganizerAndHallAsync();
        await Factory.InsertPublishedEventsAsync(organizer, hall.HallId, 25, Factory.WholeSecondNow().AddDays(1));

        var body = await GetJsonAsync("/api/v1/events");

        body.GetProperty("pageSize").GetInt32().ShouldBe(20);
        body.GetProperty("items").GetArrayLength().ShouldBe(20);
        body.GetProperty("totalCount").GetInt32().ShouldBe(25);
        body.GetProperty("totalPages").GetInt32().ShouldBe(2);
    }

    [Theory]
    [Trait("AC", "AC-06.2")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    public async Task ListEvents_InvalidPaging_Returns400(string query)
    {
        var response = await Client.GetAsync($"/api/v1/events?{query}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-06.2")]
    public async Task ListEvents_PageSize100_Returns200()
    {
        var response = await Client.GetAsync("/api/v1/events?pageSize=100", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_CityFilter_ReturnsEventsInCity()
    {
        var istanbul = await Factory.CreateHallAsync(city: "Istanbul");
        var ankara = await Factory.CreateHallAsync(city: "Ankara", venueName: "Ankara Venue");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var inIstanbul = await Client.CreatePublishedEventAsync(Factory.EventRequest(istanbul.HallId));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(ankara.HallId));

        Ids(await GetJsonAsync("/api/v1/events?city=ISTANBUL")).ShouldBe([inIstanbul]);
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_CategoryFilter_ReturnsEventsInCategory()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var sports = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, category: "Sports"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, category: "Concert"));

        Ids(await GetJsonAsync("/api/v1/events?category=Sports")).ShouldBe([sports]);
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_UnknownCategory_Returns400()
    {
        var response = await Client.GetAsync("/api/v1/events?category=Party", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_FromToFilter_IncludesBothEnds()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var ids = new List<Guid>();
        for (var day = 1; day <= 4; day++)
        {
            ids.Add(await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, startsIn: TimeSpan.FromDays(day))));
        }

        var from = Factory.WholeSecondNow().AddDays(2).ToString("O");
        var to = Factory.WholeSecondNow().AddDays(3).ToString("O");

        Ids(await GetJsonAsync($"/api/v1/events?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}")).ShouldBe([ids[1], ids[2]]);
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_TextSearch_MatchesTitleCaseInsensitively()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var jazz = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Istanbul Jazz Festival"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Rock Night"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "100% Pop"));

        Ids(await GetJsonAsync("/api/v1/events?q=JAZZ")).ShouldBe([jazz]);
        (await GetJsonAsync("/api/v1/events?q=%25")).GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-06.3")]
    public async Task ListEvents_CombinedFilters_AreAnded()
    {
        var istanbul = await Factory.CreateHallAsync(city: "Istanbul");
        var ankara = await Factory.CreateHallAsync(city: "Ankara", venueName: "Ankara Venue");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var match = await Client.CreatePublishedEventAsync(Factory.EventRequest(istanbul.HallId, title: "Jazz", category: "Concert"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(istanbul.HallId, title: "Jazz", category: "Theatre"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(ankara.HallId, title: "Jazz", category: "Concert"));
        await Client.CreatePublishedEventAsync(Factory.EventRequest(istanbul.HallId, title: "Rock", category: "Concert"));

        Ids(await GetJsonAsync("/api/v1/events?city=Istanbul&category=Concert&q=jazz")).ShouldBe([match]);
    }

    [Theory]
    [Trait("AC", "AC-06.4")]
    [InlineData(null)]
    [InlineData("startsAt")]
    [InlineData("-startsAt")]
    [InlineData("title")]
    public async Task ListEvents_Sort_OrdersItems(string? sort)
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var b = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "B", startsIn: TimeSpan.FromDays(1)));
        var a = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "A", startsIn: TimeSpan.FromDays(3)));
        var c = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "C", startsIn: TimeSpan.FromDays(2)));

        var ids = Ids(await GetJsonAsync(sort is null ? "/api/v1/events" : $"/api/v1/events?sort={sort}"));

        ids.ShouldBe(sort switch
        {
            "-startsAt" => [a, c, b],
            "title" => [a, b, c],
            _ => [b, c, a],
        });
    }

    [Fact]
    [Trait("AC", "AC-06.4")]
    public async Task ListEvents_InvalidSort_Returns400()
    {
        var response = await Client.GetAsync("/api/v1/events?sort=price", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Theory]
    [Trait("AC", "AC-06.4")]
    [InlineData("startsAt")]
    [InlineData("-startsAt")]
    [InlineData("title")]
    public async Task ListEvents_ConsecutivePages_NoRepeatsOrGaps(string sort)
    {
        var (organizer, hall) = await CreateOrganizerAndHallAsync();
        // Identical start times and titles force the id tie-breaker to decide the order.
        var created = await Factory.InsertPublishedEventsAsync(organizer, hall.HallId, 45, Factory.WholeSecondNow().AddDays(5), titlePrefix: "Same");
        await WithDbAsync(db => db.Events.ExecuteUpdateAsync(s => s.SetProperty(e => e.Title, "Same"), Ct));

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange(Ids(await GetJsonAsync($"/api/v1/events?sort={sort}&page={page}&pageSize=20")));
        }

        seen.Count.ShouldBe(45);
        seen.Distinct().Count().ShouldBe(45);
        seen.Order().ShouldBe(created.Order());
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task ListOrganizerEvents_Organizer_SeesOwnEventsInAllStates()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Other organizer"));
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var draft = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId, title: "Draft", startsIn: TimeSpan.FromDays(1)));
        var published = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Published", startsIn: TimeSpan.FromDays(2)));
        var cancelled = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Cancelled", startsIn: TimeSpan.FromDays(3)));
        (await Client.PostAsync($"/api/v1/events/{cancelled}/cancel", null, Ct)).EnsureSuccessStatusCode();

        var body = await GetJsonAsync("/api/v1/organizer/events");

        Ids(body).ShouldBe([draft, published, cancelled]);
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("status").GetString()).ShouldBe(["Draft", "Published", "Cancelled"]);
    }

    [Fact]
    [Trait("AC", "AC-06.5")]
    public async Task ListOrganizerEvents_Admin_SeesAllEvents()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var first = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId, startsIn: TimeSpan.FromDays(1)));
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var second = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, startsIn: TimeSpan.FromDays(2)));
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var body = await GetJsonAsync("/api/v1/organizer/events?pageSize=10");

        Ids(body).ShouldBe([first, second]);
        body.GetProperty("pageSize").GetInt32().ShouldBe(10);
    }

    [Fact]
    [Trait("AC", "AC-06.6")]
    public async Task ListEvents_Item_HasDocumentedFields()
    {
        var hall = await Factory.CreateHallAsync(city: "Izmir", venueName: "Arena", hallName: "Hall 1");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Show"));

        var item = (await GetJsonAsync("/api/v1/events")).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();

        item.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "title", "category", "startsAt", "endsAt", "venueName", "city", "hallName", "minPrice"]);
        item.GetProperty("id").GetGuid().ShouldBe(eventId);
        item.GetProperty("venueName").GetString().ShouldBe("Arena");
        item.GetProperty("city").GetString().ShouldBe("Izmir");
        item.GetProperty("hallName").GetString().ShouldBe("Hall 1");
        item.GetProperty("minPrice").GetDecimal().ShouldBe(EventHelper.BalconyPrice);
    }

    [Fact]
    [Trait("AC", "AC-06.6")]
    public async Task GetEvent_Published_Returns200WithDetail()
    {
        var hall = await Factory.CreateHallAsync(city: "Izmir", venueName: "Arena", hallName: "Hall 1");
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId, title: "Show"));

        var body = await GetJsonAsync($"/api/v1/events/{eventId}");

        body.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "title", "description", "category", "startsAt", "endsAt", "status", "venue", "hall", "sectionPrices"]);
        body.GetProperty("status").GetString().ShouldBe("Published");
        body.GetProperty("venue").GetProperty("id").GetGuid().ShouldBe(hall.VenueId);
        body.GetProperty("venue").GetProperty("address").GetString().ShouldBe("Arena address");
        body.GetProperty("hall").GetProperty("name").GetString().ShouldBe("Hall 1");
        body.GetProperty("sectionPrices").EnumerateArray().Select(p => (p.GetProperty("section").GetString(), p.GetProperty("price").GetDecimal()))
            .ShouldBe([("Balcony", EventHelper.BalconyPrice), ("Floor", EventHelper.FloorPrice)]);
    }

    [Fact]
    [Trait("AC", "AC-06.6")]
    public async Task GetEvent_DraftOrUnknown_Returns404()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var draft = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));

        await (await Client.GetAsync($"/api/v1/events/{draft}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
        await (await Client.GetAsync($"/api/v1/events/{Guid.CreateVersion7()}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    private static List<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private async Task<(Guid OrganizerId, TestHall Hall)> CreateOrganizerAndHallAsync()
    {
        var organizer = await Factory.CreateUserAsync(UserRole.Organizer);
        return (organizer.Id, await Factory.CreateHallAsync());
    }
}
