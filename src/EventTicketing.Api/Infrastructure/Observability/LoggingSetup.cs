using Serilog;
using Serilog.Formatting.Compact;

namespace EventTicketing.Api.Infrastructure.Observability;

public static class LoggingSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>Serilog console logging (ISKELET §5.15): readable text in Development, JSON elsewhere.</summary>
    public static WebApplicationBuilder AddLogging(this WebApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Services.AddSerilog((services, configuration) =>
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
        });

        return builder;
    }
}
