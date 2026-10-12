using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventTicketing.IntegrationTests.Fixtures;

public static class ReservationHelper
{
    /// <summary>Event seat ids of <paramref name="eventId"/> in a stable order (section, row, number).</summary>
    public static async Task<IReadOnlyList<Guid>> GetEventSeatIdsAsync(this ApiFactory factory, Guid eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await (
                from es in db.EventSeats
                join s in db.Seats on es.SeatId equals s.Id
                where es.EventId == eventId
                orderby s.Section, s.Row, s.Number
                select es.Id)
            .ToListAsync();
    }

    /// <summary>A Published event in a fresh hall, created by a fresh organizer; returns its seat ids too.</summary>
    public static async Task<(Guid EventId, IReadOnlyList<Guid> SeatIds)> CreateEventOnSaleAsync(this ApiFactory factory, TimeSpan? startsIn = null)
    {
        var organizer = await factory.CreateUserAsync(UserRole.Organizer);
        var hall = await factory.CreateHallAsync();
        var eventId = (await factory.InsertPublishedEventsAsync(organizer.Id, hall.HallId, 1, factory.WholeSecondNow().Add(startsIn ?? TimeSpan.FromDays(30))))[0];
        return (eventId, await factory.GetEventSeatIdsAsync(eventId));
    }

    public static Task<HttpResponseMessage> HoldAsync(this HttpClient client, Guid eventId, params Guid[] eventSeatIds) =>
        client.PostAsJsonAsync($"/api/v1/events/{eventId}/reservations", new { eventSeatIds });

    /// <summary>Holds seats and returns the reservation id; fails the test if the hold is refused.</summary>
    public static async Task<Guid> HoldOrFailAsync(this HttpClient client, Guid eventId, params Guid[] eventSeatIds)
    {
        var response = await client.HoldAsync(eventId, eventSeatIds);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("reservationId").GetGuid();
    }
}
