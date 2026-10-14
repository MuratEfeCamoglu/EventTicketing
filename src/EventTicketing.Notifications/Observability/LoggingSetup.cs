using Serilog;
using Serilog.Formatting.Compact;

namespace EventTicketing.Notifications.Observability;

public static class LoggingSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>Serilog console logging like the API (ISKELET §5.15); the static logger is left untouched for in-process tests.</summary>
    public static HostApplicationBuilder AddLogging(this HostApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
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
            },
            preserveStaticLogger: true);

        return builder;
    }
}
