using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Features.Reservations;

/// <summary>
/// AC-08.3: 50 customers request the same seat at the same instant; exactly one wins, in each of 10 rounds. Run with the
/// Redis lock (AC-14.1), with the database alone (lock disabled) and with Redis unreachable (AC-14.3).
/// </summary>
public sealed class SeatHoldConcurrencyTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int Customers = 50;
    private const int Rounds = 10;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-08.3")]
    [Trait("AC", "AC-14.1")]
    public async Task CreateReservation_FiftyParallelRequestsWithLock_ExactlyOneSucceedsAndNoDatabaseConflict()
    {
        using var concurrencyConflicts = new CounterListener("seat_hold_concurrency_conflicts_total");

        await RunRoundsAsync(Client);

        concurrencyConflicts.Total.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-08.3")]
    public async Task CreateReservation_FiftyParallelRequestsWithoutLock_DatabaseAloneAllowsExactlyOne()
    {
        await using var api = Factory.WithWebHostBuilder(builder => builder.UseSetting("Locking:Enabled", "false"));
        using var client = api.CreateClient();
        using var concurrencyConflicts = new CounterListener("seat_hold_concurrency_conflicts_total");

        await RunRoundsAsync(client);

        // Without the lock, the losers are stopped by the seat status or by xmin; the latter shows in the counter.
        TestContext.Current.SendDiagnosticMessage($"Concurrency conflicts without lock: {concurrencyConflicts.Total}");
    }

    [Fact]
    [Trait("AC", "AC-14.3")]
    public async Task CreateReservation_RedisUnreachable_StillExactlyOneAndWarnsLockSkipped()
    {
        await using var api = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Redis", $"localhost:{ClosedPort.Find()},abortConnect=false,connectTimeout=1000,syncTimeout=1000"));
        using var client = api.CreateClient();

        await RunRoundsAsync(client);

        Factory.Logs.Events.Count(e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.StartsWith("Seat lock skipped", StringComparison.Ordinal))
            .ShouldBeGreaterThanOrEqualTo(Customers * Rounds);
    }

    private async Task RunRoundsAsync(HttpClient client)
    {
        var tokens = new List<string>();
        for (var i = 0; i < Customers; i++)
        {
            tokens.Add((await Factory.CreateUserAndLoginAsync(client, UserRole.Customer)).Tokens.AccessToken);
        }

        for (var round = 1; round <= Rounds; round++)
        {
            var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
            var seatId = seatIds[0];

            var statuses = await SendAtOnceAsync(client, tokens, eventId, seatId);

            statuses.Count(s => s == HttpStatusCode.Created).ShouldBe(1, $"round {round}");
            statuses.Count(s => s == HttpStatusCode.Conflict).ShouldBe(Customers - 1, $"round {round}");
            var pending = await WithDbAsync(db => db.ReservationSeats
                .Where(rs => rs.EventSeatId == seatId)
                .Join(db.Reservations, rs => rs.ReservationId, r => r.Id, (_, r) => r)
                .CountAsync(r => r.Status == ReservationStatus.Pending, Ct));
            pending.ShouldBe(1, $"round {round}");
        }
    }

    /// <summary>Every request waits on a <see cref="Barrier"/> in its own thread, so all 50 are released together (R-02).</summary>
    private static async Task<HttpStatusCode[]> SendAtOnceAsync(HttpClient client, IReadOnlyList<string> tokens, Guid eventId, Guid seatId)
    {
        using var barrier = new Barrier(tokens.Count);
        var requests = tokens.Select(token => Task.Factory.StartNew(
                async () =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/events/{eventId}/reservations")
                    {
                        Content = JsonContent.Create(new { eventSeatIds = new[] { seatId } }),
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    barrier.SignalAndWait(Ct);
                    using var response = await client.SendAsync(request, Ct);
                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "seat.unavailable");
                    }

                    return response.StatusCode;
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())
            .ToList();

        return await Task.WhenAll(requests);
    }

    /// <summary>Sums one <see cref="EventTicketingMetrics"/> counter while alive.</summary>
    private sealed class CounterListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private long _total;

        public CounterListener(string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == EventTicketingMetrics.MeterName && instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref _total, value));
            _listener.Start();
        }

        public long Total => Interlocked.Read(ref _total);

        public void Dispose() => _listener.Dispose();
    }
}
