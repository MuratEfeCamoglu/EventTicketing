using System.Net;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using static EventTicketing.IntegrationTests.Fixtures.VenueHelper;

namespace EventTicketing.IntegrationTests.Features.Venues;

public sealed class VenueTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-04.1")]
    public async Task CreateVenue_AsAdmin_Returns201WithVenue()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.PostAsJsonAsync("/api/v1/venues", new { name = "Zorlu PSM", city = "Istanbul", address = "Koru Sk. No:2" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        body.GetProperty("name").GetString().ShouldBe("Zorlu PSM");
        body.GetProperty("city").GetString().ShouldBe("Istanbul");
        body.GetProperty("address").GetString().ShouldBe("Koru Sk. No:2");
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/venues/{id}");
        (await WithDbAsync(db => db.Venues.AnyAsync(v => v.Id == id, TestContext.Current.CancellationToken))).ShouldBeTrue();
    }

    [Theory]
    [Trait("AC", "AC-04.1")]
    [InlineData(UserRole.Organizer)]
    [InlineData(UserRole.Customer)]
    public async Task CreateVenue_AsNonAdmin_Returns403(UserRole role)
    {
        await Client.AuthenticateAsAsync(Factory, role);

        var response = await Client.PostAsJsonAsync("/api/v1/venues", new { name = "Venue", city = "Ankara", address = "Address" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        (await WithDbAsync(db => db.Venues.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
    }

    [Fact]
    public async Task CreateVenue_MissingFields_Returns400()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.PostAsJsonAsync("/api/v1/venues", new { name = "", city = "Ankara" }, TestContext.Current.CancellationToken);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        body.GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
        body.GetProperty("errors").TryGetProperty("address", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-04.4")]
    public async Task UpdateVenue_AsAdmin_Returns200AndChangesOnlyDescriptiveFields()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Old name", "Izmir");
        var hallId = await Client.CreateHallAsync(venueId, "Main", Section("Floor", ("A", 3)));

        var response = await Client.PutAsJsonAsync($"/api/v1/venues/{venueId}", new { name = "New name", city = "Ankara", address = "New address" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("id").GetGuid().ShouldBe(venueId);
        body.GetProperty("name").GetString().ShouldBe("New name");
        var venue = await WithDbAsync(db => db.Venues.AsNoTracking().SingleAsync(v => v.Id == venueId, TestContext.Current.CancellationToken));
        (venue.Name, venue.City, venue.Address).ShouldBe(("New name", "Ankara", "New address"));
        (await WithDbAsync(db => db.Seats.CountAsync(s => s.HallId == hallId, TestContext.Current.CancellationToken))).ShouldBe(3);
    }

    [Fact]
    [Trait("AC", "AC-04.4")]
    public async Task UpdateVenue_Unknown_Returns404()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.PutAsJsonAsync($"/api/v1/venues/{Guid.CreateVersion7()}", new { name = "Name", city = "City", address = "Address" }, TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task ListVenues_FilterByCityAndPage_ReturnsPagedItems()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        for (var i = 1; i <= 3; i++)
        {
            await Client.CreateVenueAsync($"Istanbul venue {i}", "Istanbul");
        }

        await Client.CreateVenueAsync("Ankara venue", "Ankara");

        var response = await Client.GetAsync("/api/v1/venues?city=istanbul&page=2&pageSize=2", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("page").GetInt32().ShouldBe(2);
        body.GetProperty("pageSize").GetInt32().ShouldBe(2);
        body.GetProperty("totalCount").GetInt32().ShouldBe(3);
        body.GetProperty("totalPages").GetInt32().ShouldBe(2);
        var item = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("name").GetString().ShouldBe("Istanbul venue 3");
        item.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "name", "city", "address"]);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task ListVenues_NoPagingParameters_UsesDefaults()
    {
        var response = await Client.GetAsync("/api/v1/venues", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(20);
        body.GetProperty("totalCount").GetInt32().ShouldBe(0);
        body.GetProperty("totalPages").GetInt32().ShouldBe(0);
    }

    [Theory]
    [Trait("AC", "AC-04.5")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task ListVenues_InvalidPaging_Returns400(string query)
    {
        var response = await Client.GetAsync($"/api/v1/venues?{query}", TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task GetVenue_Existing_Returns200WithHallSeatCounts()
    {
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);
        var venueId = await Client.CreateVenueAsync("Venue", "Bursa");
        var hallId = await Client.CreateHallAsync(venueId, "Main", Section("Floor", ("A", 4), ("B", 3)));

        var response = await Client.GetAsync($"/api/v1/venues/{venueId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("city").GetString().ShouldBe("Bursa");
        var hall = body.GetProperty("halls").EnumerateArray().ShouldHaveSingleItem();
        hall.GetProperty("id").GetGuid().ShouldBe(hallId);
        hall.GetProperty("name").GetString().ShouldBe("Main");
        hall.GetProperty("seatCount").GetInt32().ShouldBe(7);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task GetVenue_Unknown_Returns404()
    {
        var response = await Client.GetAsync($"/api/v1/venues/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }
}
