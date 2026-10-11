using System.Diagnostics;
using EventTicketing.Api.Infrastructure.Observability;
using Serilog.Events;
using Serilog.Parsing;

namespace EventTicketing.UnitTests.Infrastructure;

public sealed class TraceIdEnricherTests
{
    [Fact]
    public void Enrich_EventWithTraceAndSpan_AddsHexProperties()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var logEvent = new LogEvent(
            TimeProvider.System.GetUtcNow(), LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("test"), [], traceId, spanId);

        new TraceIdEnricher().Enrich(logEvent, new PropertyFactory());

        logEvent.Properties["TraceId"].ShouldBeOfType<ScalarValue>().Value.ShouldBe(traceId.ToHexString());
        logEvent.Properties["SpanId"].ShouldBeOfType<ScalarValue>().Value.ShouldBe(spanId.ToHexString());
    }

    private sealed class PropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
