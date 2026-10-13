using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence.Seed;
using EventTicketing.IntegrationTests.Fixtures;

namespace EventTicketing.IntegrationTests.Features.Platform;

/// <summary>F-16. The end-to-end check of AC-16.1 is <c>scripts/smoke.sh</c> against <c>dotnet run</c>.</summary>
public sealed class LocalRunTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-16.1")]
    public async Task Startup_WithSampleData_ListsSeedEventThirtyDaysAhead()
    {
        await using var api = Factory.WithWebHostBuilder(builder => builder.UseSetting("Seed:SampleData", "true"));
        using var client = api.CreateClient();

        var response = await client.GetAsync("/api/v1/events", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = (await response.ReadJsonAsync()).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("startsAt").GetDateTimeOffset()
            .ShouldBe(Factory.Clock.GetUtcNow().AddDays(DatabaseInitializer.SampleEventStartsInDays), TimeSpan.FromSeconds(1));
    }

    [Fact]
    [Trait("AC", "AC-16.2")]
    public async Task Restart_ReservationCreatedBefore_IsStillReadable()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        var (_, tokens) = await Factory.CreateUserAndLoginAsync(Client, UserRole.Customer);
        Client.WithBearerToken(tokens.AccessToken);
        var reservationId = await Client.HoldOrFailAsync(eventId, seatIds[0]);
        var before = await (await Client.GetAsync($"/api/v1/reservations/{reservationId}", Ct)).ReadJsonAsync();

        // A second, freshly started host on the same database stands in for stopping and starting the API.
        await using var restarted = Factory.WithWebHostBuilder(_ => { });
        using var client = restarted.CreateClient().WithBearerToken(tokens.AccessToken);
        var response = await client.GetAsync($"/api/v1/reservations/{reservationId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetRawText().ShouldBe(before.GetRawText());
    }
}
