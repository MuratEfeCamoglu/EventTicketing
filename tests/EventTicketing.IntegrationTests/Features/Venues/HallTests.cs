using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using static EventTicketing.IntegrationTests.Fixtures.VenueHelper;

namespace EventTicketing.IntegrationTests.Features.Venues;

public sealed class HallTests(ApiFactory factory) : ApiTestBase(factory)
{
    public static TheoryData<string, string> InvalidLayouts() => new()
    {
        { "no sections", """{"name":"Hall","sections":[]}""" },
        { "5001 seats", """{"name":"Hall","sections":[{"name":"Floor","rows":[{"label":"A","seatCount":5000},{"label":"B","seatCount":1}]}]}""" },
        { "duplicate section", """{"name":"Hall","sections":[{"name":"Floor","rows":[{"label":"A","seatCount":5}]},{"name":"Floor","rows":[{"label":"B","seatCount":5}]}]}""" },
        { "duplicate row", """{"name":"Hall","sections":[{"name":"Floor","rows":[{"label":"A","seatCount":5},{"label":"A","seatCount":3}]}]}""" },
        { "zero seat row", """{"name":"Hall","sections":[{"name":"Floor","rows":[{"label":"A","seatCount":0}]}]}""" },
    };

    [Fact]
    [Trait("AC", "AC-04.2")]
    public async Task CreateHall_ValidLayout_Returns201AndCreatesNumberedSeats()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/venues/{venueId}/halls",
            new { name = "Main Hall", sections = new[] { Section("Floor", ("A", 10), ("B", 8)), Section("Balcony", ("A", 5)) } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var hallId = body.GetProperty("id").GetGuid();
        body.GetProperty("seatCount").GetInt32().ShouldBe(23);
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/halls/{hallId}");

        var seats = await WithDbAsync(db => db.Seats.AsNoTracking().Where(s => s.HallId == hallId).ToListAsync(TestContext.Current.CancellationToken));
        seats.Count.ShouldBe(23);
        foreach (var row in seats.GroupBy(s => (s.Section, s.Row)))
        {
            row.Select(s => s.Number).Order().ShouldBe(Enumerable.Range(1, row.Count()));
        }

        seats.Count(s => s is { Section: "Floor", Row: "B" }).ShouldBe(8);
    }

    [Theory]
    [Trait("AC", "AC-04.3")]
    [MemberData(nameof(InvalidLayouts))]
    public async Task CreateHall_InvalidLayout_Returns400AndCreatesNothing(string scenario, string json)
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");

        var response = await Client.PostAsync($"/api/v1/venues/{venueId}/halls", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        (await WithDbAsync(db => db.Halls.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0, scenario);
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    public async Task CreateHall_ExactlyMaxSeats_Returns201()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/venues/{venueId}/halls",
            new { name = "Arena", sections = new[] { Section("Floor", ("A", 2500), ("B", 2500)) } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("seatCount").GetInt32().ShouldBe(5000);
    }

    [Fact]
    public async Task CreateHall_DuplicateNameInVenue_Returns400()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");
        await Client.CreateHallAsync(venueId, "Main", Section("Floor", ("A", 2)));

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/venues/{venueId}/halls",
            new { name = "Main", sections = new[] { Section("Floor", ("A", 2)) } },
            TestContext.Current.CancellationToken);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateHall_UnknownVenue_Returns404()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/venues/{Guid.CreateVersion7()}/halls",
            new { name = "Main", sections = new[] { Section("Floor", ("A", 2)) } },
            TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-04.4")]
    public async Task UpdateHallLayout_NoSuchEndpoint_Returns405AndLayoutUnchanged()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");
        var hallId = await Client.CreateHallAsync(venueId, "Main", Section("Floor", ("A", 2)));

        var response = await Client.PutAsJsonAsync(
            $"/api/v1/halls/{hallId}",
            new { name = "Main", sections = new[] { Section("Floor", ("A", 9)) } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await WithDbAsync(db => db.Seats.CountAsync(s => s.HallId == hallId, TestContext.Current.CancellationToken))).ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task GetHall_Existing_Returns200WithSectionsRowsAndSeats()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Istanbul");
        var hallId = await Client.CreateHallAsync(venueId, "Main", Section("Floor", ("A", 3), ("B", 2)), Section("Balcony", ("A", 1)));

        var response = await Client.GetAsync($"/api/v1/halls/{hallId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("id").GetGuid().ShouldBe(hallId);
        body.GetProperty("venueId").GetGuid().ShouldBe(venueId);
        body.GetProperty("name").GetString().ShouldBe("Main");
        var sections = body.GetProperty("sections").EnumerateArray().ToDictionary(s => s.GetProperty("name").GetString()!);
        sections.Keys.Order().ShouldBe(["Balcony", "Floor"]);
        var floorRows = sections["Floor"].GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("label").GetString()!);
        floorRows["A"].GetProperty("seats").EnumerateArray().Select(s => s.GetProperty("number").GetInt32()).ShouldBe([1, 2, 3]);
        floorRows["B"].GetProperty("seats").EnumerateArray().First().GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task GetHall_Unknown_Returns404()
    {
        var response = await Client.GetAsync($"/api/v1/halls/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }
}
