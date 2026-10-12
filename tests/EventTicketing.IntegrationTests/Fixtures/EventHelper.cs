using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Fixtures;

public sealed record TestHall(Guid VenueId, Guid HallId, string City);

public static class EventHelper
{
    public const decimal FloorPrice = 750m;
    public const decimal BalconyPrice = 450m;

    /// <summary>Floor A–B × 5 seats and Balcony A × 5 seats: 15 seats in two sections.</summary>
    public const int HallSeatCount = 15;

    /// <summary>Inserts a venue with one two-section hall directly into the database.</summary>
    public static async Task<TestHall> CreateHallAsync(this ApiFactory factory, string city = "Istanbul", string venueName = "Test Venue", string hallName = "Main Hall")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();

        var venue = new Venue(venueName, city, $"{venueName} address", now);
        var hall = new Hall(venue.Id, hallName, now);
        hall.AddRow("Floor", "A", 5);
        hall.AddRow("Floor", "B", 5);
        hall.AddRow("Balcony", "A", 5);
        db.Venues.Add(venue);
        db.Halls.Add(hall);
        await db.SaveChangesAsync();

        return new TestHall(venue.Id, hall.Id, city);
    }

    /// <summary>A valid <c>POST /events</c> body for <paramref name="hallId"/>, starting <paramref name="startsIn"/> from now (default 30 days).</summary>
    public static object EventRequest(
        this ApiFactory factory,
        Guid hallId,
        string title = "Concert",
        string category = "Concert",
        TimeSpan? startsIn = null,
        object[]? sectionPrices = null)
    {
        var startsAt = factory.WholeSecondNow().Add(startsIn ?? TimeSpan.FromDays(30));
        return new
        {
            hallId,
            title,
            description = $"{title} description",
            category,
            startsAt,
            endsAt = startsAt.AddHours(3),
            sectionPrices = sectionPrices ?? [new { section = "Floor", price = FloorPrice }, new { section = "Balcony", price = BalconyPrice }],
        };
    }

    /// <summary>Current fake time without sub-second digits, so values survive the <c>timestamptz</c> round trip unchanged.</summary>
    public static DateTimeOffset WholeSecondNow(this ApiFactory factory)
    {
        var now = factory.Clock.GetUtcNow();
        return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    /// <summary>Inserts <paramref name="count"/> Published events directly (faster than the API for list tests).</summary>
    public static async Task<IReadOnlyList<Guid>> InsertPublishedEventsAsync(
        this ApiFactory factory, Guid organizerId, Guid hallId, int count, DateTimeOffset startsAt, string titlePrefix = "Event")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        var hallSeats = db.Seats.Where(s => s.HallId == hallId).ToList();

        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var created = new Event(organizerId, hallId, $"{titlePrefix} {i:D2}", string.Empty, EventCategory.Concert, startsAt, startsAt.AddHours(2), [("Floor", FloorPrice), ("Balcony", BalconyPrice)], now);
            db.Events.Add(created);
            db.EventSeats.AddRange(created.Publish(hallSeats, now).Value);
            ids.Add(created.Id);
        }

        await db.SaveChangesAsync();
        return ids;
    }

    /// <summary>Creates a Draft event through the API; the client must be authenticated as Organizer or Admin.</summary>
    public static async Task<Guid> CreateEventAsync(this HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/api/v1/events", request);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    /// <summary>Creates and publishes an event through the API; the client must be authenticated as Organizer or Admin.</summary>
    public static async Task<Guid> CreatePublishedEventAsync(this HttpClient client, object request)
    {
        var eventId = await client.CreateEventAsync(request);
        (await client.PostAsync($"/api/v1/events/{eventId}/publish", null)).EnsureSuccessStatusCode();
        return eventId;
    }
}
