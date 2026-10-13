using System.Diagnostics.Metrics;
using EventTicketing.Api.Infrastructure.Observability;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>Sums one <see cref="EventTicketingMetrics"/> counter while alive (AC-14.1, AC-09.3, AC-18.3).</summary>
public sealed class CounterListener : IDisposable
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
