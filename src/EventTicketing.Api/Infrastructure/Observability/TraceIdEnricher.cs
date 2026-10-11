using Serilog.Core;
using Serilog.Events;

namespace EventTicketing.Api.Infrastructure.Observability;

/// <summary>Exposes the Activity trace/span ids Serilog captures as the <c>TraceId</c>/<c>SpanId</c> properties.</summary>
public sealed class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.TraceId is { } traceId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId.ToHexString()));
        }

        if (logEvent.SpanId is { } spanId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId.ToHexString()));
        }
    }
}
