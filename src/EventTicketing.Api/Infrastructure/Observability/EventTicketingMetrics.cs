using System.Diagnostics.Metrics;

namespace EventTicketing.Api.Infrastructure.Observability;

/// <summary>
/// Custom counters of ISKELET AC-18.3 (<c>System.Diagnostics.Metrics</c>; exported over OTLP from V3-3). Each counter is
/// introduced by the task that first needs it.
/// </summary>
public sealed class EventTicketingMetrics
{
    public const string MeterName = "EventTicketing";

    private readonly Counter<long> _seatHoldConflicts;
    private readonly Counter<long> _seatHoldConcurrencyConflicts;

    public EventTicketingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _seatHoldConflicts = meter.CreateCounter<long>("seat_hold_conflicts_total", description: "Seat hold requests refused with seat.unavailable.");
        _seatHoldConcurrencyConflicts = meter.CreateCounter<long>(
            "seat_hold_concurrency_conflicts_total", description: "Seat holds that lost an optimistic concurrency race (DbUpdateConcurrencyException).");
    }

    public void SeatHoldConflict() => _seatHoldConflicts.Add(1);

    public void SeatHoldConcurrencyConflict() => _seatHoldConcurrencyConflicts.Add(1);
}
