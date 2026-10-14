using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventTicketing.IntegrationTests.Features.Platform;

/// <summary>AC-18.3: the custom counters move with the operations they count (the sweeper counter: ReservationExpiryTests).</summary>
public sealed class MetricsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-18.3")]
    public async Task PayReservation_Succeeds_AddsOneToTicketsSoldPerTicket()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        using var ticketsSold = new CounterListener("tickets_sold_total");

        await Client.BuyAsync(eventId, seatIds[0], seatIds[1]);

        ticketsSold.Total.ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-18.3")]
    public async Task CreateReservation_SeatAlreadyHeld_AddsOneToSeatHoldConflicts()
    {
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        await Client.HoldOrFailAsync(eventId, seatIds[0]);
        await Client.AuthenticateAsAsync(Factory, UserRole.Customer);
        using var conflicts = new CounterListener("seat_hold_conflicts_total");

        var response = await Client.HoldAsync(eventId, seatIds[0]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        conflicts.Total.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-18.3")]
    public async Task CreateReservation_SeatChangedBeforeSave_AddsOneToConcurrencyConflicts()
    {
        var interceptor = new ChangeSeatBeforeSaveInterceptor(Factory.Postgres.ConnectionString);
        await using var api = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Api.Infrastructure.Persistence.AppDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = api.CreateClient();
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        await client.AuthenticateAsAsync(Factory, UserRole.Customer);
        using var concurrencyConflicts = new CounterListener("seat_hold_concurrency_conflicts_total");
        using var conflicts = new CounterListener("seat_hold_conflicts_total");

        var response = await client.HoldAsync(eventId, seatIds[0]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        interceptor.Fired.ShouldBeTrue();
        concurrencyConflicts.Total.ShouldBe(1);
        conflicts.Total.ShouldBe(1);
    }

    /// <summary>
    /// Once, right before a seat hold is saved, rewrites that seat row from another connection: the row gets a new
    /// <c>xmin</c> and the hold loses on optimistic concurrency, as against a concurrent request.
    /// </summary>
    private sealed class ChangeSeatBeforeSaveInterceptor(string connectionString) : SaveChangesInterceptor
    {
        private int _fired;

        public bool Fired => Volatile.Read(ref _fired) == 1;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var seat = eventData.Context?.ChangeTracker.Entries<EventSeat>().FirstOrDefault(e => e.State == EntityState.Modified)?.Entity;
            if (seat is not null && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("UPDATE event_seats SET price = price WHERE id = @id", connection);
                command.Parameters.AddWithValue("id", seat.Id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
