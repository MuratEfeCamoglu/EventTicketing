using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace EventTicketing.Notifications.Observability;

public static class TelemetrySetup
{
    public const string ServiceName = "eventticketing-notifications";

    /// <summary>Standard OpenTelemetry variable; empty means no OTLP export at all (ISKELET §5.14).</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Name of the ActivitySource and the Meter of MassTransit.</summary>
    private const string MassTransitSource = "MassTransit";

    /// <summary>
    /// Traces (MassTransit consume, Npgsql of the inbox) and metrics (runtime, MassTransit) over OTLP, like the API
    /// (ISKELET §5.15). The consume span continues the trace whose context MassTransit carried in the message (AC-18.2).
    /// </summary>
    public static HostApplicationBuilder AddTelemetry(this HostApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddNpgsql()
                .AddSource(MassTransitSource))
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddMeter(MassTransitSource))
            .UseOtlpExporter();

        return builder;
    }
}
