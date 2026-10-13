using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Payments;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Events;

public sealed class EventCacheTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-15.1")]
    public async Task GetEvent_SecondCall_SendsNoDatabaseQuery()
    {
        var (eventId, _) = await Factory.CreateEventOnSaleAsync();
        (await Client.GetAsync($"/api/v1/events/{eventId}", Ct)).EnsureSuccessStatusCode();
        var before = Factory.DbCommands.Count;

        var response = await Client.GetAsync($"/api/v1/events/{eventId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.DbCommands.Count.ShouldBe(before);
    }

    [Fact]
    [Trait("AC", "AC-15.2")]
    public async Task GetEvent_AfterUpdate_ReturnsNewTitle()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var request = Factory.EventRequest(hall.HallId, title: "Old title");
        var eventId = await Client.CreatePublishedEventAsync(request);
        (await GetJsonAsync($"/api/v1/events/{eventId}")).GetProperty("title").GetString().ShouldBe("Old title");
        (await GetJsonAsync("/api/v1/events")).GetProperty("items")[0].GetProperty("title").GetString().ShouldBe("Old title");
        var update = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(request, JsonSerializerOptions.Web))!;
        update["title"] = "New title";

        (await Client.PutAsJsonAsync($"/api/v1/events/{eventId}", update, Ct)).EnsureSuccessStatusCode();

        (await GetJsonAsync($"/api/v1/events/{eventId}")).GetProperty("title").GetString().ShouldBe("New title");
        (await GetJsonAsync("/api/v1/events")).GetProperty("items")[0].GetProperty("title").GetString().ShouldBe("New title");
    }

    [Fact]
    [Trait("AC", "AC-15.2")]
    public async Task GetEvent_CachedAsMissingThenPublished_Returns200()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreateEventAsync(Factory.EventRequest(hall.HallId));
        (await Client.GetAsync($"/api/v1/events/{eventId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await Client.PostAsync($"/api/v1/events/{eventId}/publish", null, Ct)).EnsureSuccessStatusCode();

        (await Client.GetAsync($"/api/v1/events/{eventId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-15.3")]
    public async Task GetSeatMap_AfterHoldCancelAndPayment_ReturnsCurrentStatus()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Available");

        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Held");

        (await Client.DeleteAsync($"/api/v1/reservations/{reservationId}", Ct)).EnsureSuccessStatusCode();
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Available");

        var declined = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Held");
        (await Client.PayAsync(declined, MockPaymentGateway.DeclinedToken)).StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Available");

        await Client.BuyAsync(eventId, seatIds[0]);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Sold");
    }

    [Fact]
    [Trait("AC", "AC-15.3")]
    public async Task GetSeatMap_CachedHoldExpires_ReturnsAvailable()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);
        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Held");

        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        (await SeatStatusAsync(eventId, seatIds[0])).ShouldBe("Available");
    }

    [Fact]
    [Trait("AC", "AC-15.3")]
    public async Task GetSeatMap_AfterEventCancelled_Returns404()
    {
        var hall = await Factory.CreateHallAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Organizer);
        var eventId = await Client.CreatePublishedEventAsync(Factory.EventRequest(hall.HallId));
        (await Client.GetAsync($"/api/v1/events/{eventId}/seats", Ct)).EnsureSuccessStatusCode();

        (await Client.PostAsync($"/api/v1/events/{eventId}/cancel", null, Ct)).EnsureSuccessStatusCode();

        (await Client.GetAsync($"/api/v1/events/{eventId}/seats", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/v1/events/{eventId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "AC-15.4")]
    public async Task EventReads_RedisUnreachable_StillReturn200()
    {
        await using var noRedisApi = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Redis", $"localhost:{ClosedPort.Find()},abortConnect=false,connectTimeout=1000,syncTimeout=1000"));
        using var client = noRedisApi.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();

        (await client.GetAsync("/api/v1/events", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/events/{eventId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/events/{eventId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/events/{eventId}/seats", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Writes invalidate the cache; with Redis down they must still succeed.
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        (await client.HoldAsync(eventId, seatIds[0])).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<string?> SeatStatusAsync(Guid eventId, Guid eventSeatId) =>
        (await GetJsonAsync($"/api/v1/events/{eventId}/seats")).EnumerateArray()
            .Single(s => s.GetProperty("eventSeatId").GetGuid() == eventSeatId)
            .GetProperty("status").GetString();

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }
}
