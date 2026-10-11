using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>In-memory Serilog sink; the app picks it up through <c>ReadFrom.Services</c>.</summary>
public sealed class CollectingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public void Clear() => _events.Clear();
}
