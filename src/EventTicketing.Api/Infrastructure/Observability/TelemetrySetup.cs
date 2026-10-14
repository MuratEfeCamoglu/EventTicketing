using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EventTicketing.Api.Infrastructure.Observability;

public static class TelemetrySetup
{
    public const string ServiceName = "eventticketing-api";

    /// <summary>Standard OpenTelemetry variable; empty means no OTLP export at all (ISKELET §5.14).</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Name of the ActivitySource and the Meter of MassTransit.</summary>
    private const string MassTransitSource = "MassTransit";

    /// <summary>
    /// Traces (ASP.NET Core, HttpClient, Npgsql, MassTransit) and metrics (ASP.NET Core, HttpClient, runtime, MassTransit and
    /// the AC-18.3 counters) over OTLP to the Aspire Dashboard (ISKELET §5.15). MassTransit carries the trace context in the
    /// message headers, so the consume span of the Notification service joins the trace of the payment (AC-18.2).
    /// </summary>
    public static WebApplicationBuilder AddTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(MassTransitSource))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(EventTicketingMetrics.MeterName, MassTransitSource))
            .UseOtlpExporter();

        return builder;
    }
}
