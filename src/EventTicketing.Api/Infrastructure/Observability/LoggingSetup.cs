using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace EventTicketing.Api.Infrastructure.Observability;

public static class LoggingSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Serilog console logging (ISKELET §5.15): readable text in Development, JSON elsewhere; (v3) also OTLP when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// The static <c>Log.Logger</c> is left untouched so that several hosts in one process (tests) do not share a logger.
    /// </summary>
    public static WebApplicationBuilder AddLogging(this WebApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        var otlpEndpoint = builder.Configuration[TelemetrySetup.OtlpEndpointKey];
        builder.Services.AddSerilog(
            (services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<TraceIdEnricher>();

            if (isDevelopment)
            {
                configuration.WriteTo.Console(outputTemplate: DevelopmentTemplate);
            }
            else
            {
                configuration.WriteTo.Console(new CompactJsonFormatter());
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                configuration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = TelemetrySetup.ServiceName };
                });
            }
        },
            preserveStaticLogger: true);

        return builder;
    }

    /// <summary>One log line per request (AC-18.1), written through this host's logger rather than the static one.</summary>
    public static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
        return app;
    }
}
