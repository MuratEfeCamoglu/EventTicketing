using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace EventTicketing.Notifications.Observability;

public static class LoggingSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Serilog console logging like the API (ISKELET §5.15), plus OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; the static
    /// logger is left untouched for in-process tests.
    /// </summary>
    public static HostApplicationBuilder AddLogging(this HostApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        var otlpEndpoint = builder.Configuration[TelemetrySetup.OtlpEndpointKey];
        builder.Services.AddSerilog(
            (services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

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
}
