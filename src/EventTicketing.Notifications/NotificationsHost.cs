using EventTicketing.Notifications.Consumers;
using EventTicketing.Notifications.Email;
using EventTicketing.Notifications.Observability;
using EventTicketing.Notifications.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.Notifications;

/// <summary>
/// Composition of the Notification service (ISKELET §5.10): consumes <c>TicketPurchased</c> from RabbitMQ and sends the
/// ticket e-mail. Shared by <c>Program.cs</c> and the integration tests, which host the service in-process.
/// </summary>
public static class NotificationsHost
{
    private const string DevOnlyPrefix = "dev-only-";

    public static HostApplicationBuilder CreateBuilder(HostApplicationBuilderSettings settings)
    {
        var builder = Host.CreateApplicationBuilder(settings);
        builder.AddLogging();

        var isProduction = builder.Environment.IsProduction();
        builder.Services.AddOptions<ConnectionStringOptions>()
            .BindConfiguration(ConnectionStringOptions.Section)
            .ValidateDataAnnotations()
            .Validate(
                o => !isProduction || !(IsDevOnly(o.Postgres) || IsDevOnly(o.RabbitMq)),
                "dev-only- values are for local use and are not allowed in Production.")
            .ValidateOnStart();
        builder.Services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.Section)
            .ValidateOnStart();
        builder.Services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddDbContext<NotificationsDbContext>((services, options) => options
            .UseNpgsql(
                services.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Postgres,
                npgsql => npgsql.MigrationsHistoryTable(NotificationsDbContext.MigrationsHistoryTable, NotificationsDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

        builder.Services.AddMassTransit(bus =>
        {
            // Inbox only: the consumer outbox de-duplicates by MessageId (AC-17.3); nothing is published from here.
            bus.AddEntityFrameworkOutbox<NotificationsDbContext>(outbox => outbox.UsePostgres());
            bus.AddConsumer<TicketPurchasedConsumer, TicketPurchasedConsumerDefinition>();

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(context.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.RabbitMq));
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }

    /// <summary>Applies the <c>notifications</c> schema migrations when <c>Database:MigrateOnStartup</c> is set.</summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync(cancellationToken);
    }

    private static bool IsDevOnly(string value) => value.Contains(DevOnlyPrefix, StringComparison.OrdinalIgnoreCase);
}
