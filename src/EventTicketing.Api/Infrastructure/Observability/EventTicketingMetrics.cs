using System.Diagnostics.Metrics;

namespace EventTicketing.Api.Infrastructure.Observability;

/// <summary>
/// Custom counters of ISKELET AC-18.3 (<c>System.Diagnostics.Metrics</c>), exported over OTLP by <see cref="TelemetrySetup"/>.
/// </summary>
public sealed class EventTicketingMetrics
{
    public const string MeterName = "EventTicketing";

    private readonly Counter<long> _seatHoldConflicts;
    private readonly Counter<long> _seatHoldConcurrencyConflicts;
    private readonly Counter<long> _reservationsExpired;
    private readonly Counter<long> _ticketsSold;

    public EventTicketingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _seatHoldConflicts = meter.CreateCounter<long>("seat_hold_conflicts_total", description: "Seat hold requests refused with seat.unavailable.");
        _seatHoldConcurrencyConflicts = meter.CreateCounter<long>(
            "seat_hold_concurrency_conflicts_total", description: "Seat holds that lost an optimistic concurrency race (DbUpdateConcurrencyException).");
        _reservationsExpired = meter.CreateCounter<long>("reservations_expired_total", description: "Pending reservations turned Expired by the sweeper.");
        _ticketsSold = meter.CreateCounter<long>("tickets_sold_total", description: "Tickets issued by successful payments.");
    }

    public void SeatHoldConflict() => _seatHoldConflicts.Add(1);

    public void SeatHoldConcurrencyConflict() => _seatHoldConcurrencyConflicts.Add(1);

    public void ReservationsExpired(int count) => _reservationsExpired.Add(count);

    public void TicketsSold(int count) => _ticketsSold.Add(count);
}
