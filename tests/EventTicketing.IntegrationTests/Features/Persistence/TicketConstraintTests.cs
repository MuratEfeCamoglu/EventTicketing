using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventTicketing.IntegrationTests.Features.Persistence;

public sealed class TicketConstraintTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-10.7")]
    public async Task InsertTicket_SecondTicketForSameEventSeat_Fails23505()
    {
        var now = Factory.Clock.GetUtcNow();
        var customer = new User("buyer@example.com", "Buyer", UserRole.Customer, now);
        customer.SetPasswordHash("not-a-real-hash");
        var organizer = new User("org@example.com", "Organizer", UserRole.Organizer, now);
        organizer.SetPasswordHash("not-a-real-hash");
        var venue = new Venue("Venue", "Istanbul", "Address", now);
        var hall = new Hall(venue.Id, "Hall", now);
        hall.AddRow("A", "1", 2);
        var @event = new Event(organizer.Id, hall.Id, "Event", "", EventCategory.Concert, now.AddDays(1), now.AddDays(1).AddHours(2), [("A", 100m)], now);
        var eventSeats = @event.Publish(hall.Seats, now).Value;
        var seat = eventSeats[0];
        var reservation = new Reservation(customer.Id, @event.Id, [seat], now.AddMinutes(10), now);

        await WithDbAsync(async db =>
        {
            db.AddRange(customer, organizer, venue, hall, @event, reservation);
            db.EventSeats.AddRange(eventSeats);
            db.Tickets.Add(new Ticket(reservation.Id, seat.Id, customer.Id, "00000000000000000000000001", now));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var exception = await Should.ThrowAsync<DbUpdateException>(() => WithDbAsync(db =>
        {
            db.Tickets.Add(new Ticket(reservation.Id, seat.Id, customer.Id, "00000000000000000000000002", now));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));

        var postgres = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgres.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.ShouldBe("ux_tickets_event_seat_id");
    }
}
