using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Events;

/// <summary>AC-21.1: <c>GET /organizer/events/{id}</c> returns every field the edit form needs, for events in any status.</summary>
public sealed class OrganizerEventTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-21.1")]
    public async Task GetOrganizerEvent_OwnDraft_Returns200WithEditableFields()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId, title: "Hamlet", category: "Theatre"));

        var response = await Client.GetAsync($"/api/v1/organizer/events/{eventId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("status").GetString().ShouldBe("Draft");
        body.GetProperty("title").GetString().ShouldBe("Hamlet");
        body.GetProperty("description").GetString().ShouldBe("Hamlet description");
        body.GetProperty("category").GetString().ShouldBe("Theatre");
        body.GetProperty("hall").GetProperty("id").GetGuid().ShouldBe(hall.HallId);
        body.GetProperty("venue").GetProperty("id").GetGuid().ShouldBe(hall.VenueId);
        var prices = body.GetProperty("sectionPrices").EnumerateArray()
            .ToDictionary(p => p.GetProperty("section").GetString()!, p => p.GetProperty("price").GetDecimal());
        prices.ShouldBe(new Dictionary<string, decimal> { ["Balcony"] = EventHelper.BalconyPrice, ["Floor"] = EventHelper.FloorPrice }, ignoreOrder: true);
    }

    [Fact]
    [Trait("AC", "AC-21.1")]
    public async Task GetOrganizerEvent_OtherOrganizersEvent_Returns404()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);

        var response = await Client.GetAsync($"/api/v1/organizer/events/{eventId}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "resource.not_found");
    }

    [Fact]
    [Trait("AC", "AC-21.1")]
    public async Task GetOrganizerEvent_Admin_Returns200ForAnyEvent()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));
        await Client.AuthenticateAsAsync(Factory, UserRole.Admin);

        var response = await Client.GetAsync($"/api/v1/organizer/events/{eventId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-21.1")]
    public async Task GetOrganizerEvent_Cancelled_Returns200()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
        (await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct)).EnsureSuccessStatusCode();

        var response = await Client.GetAsync($"/api/v1/organizer/events/{eventId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("Cancelled");
    }
}
