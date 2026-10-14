using EventTicketing.Contracts;
using EventTicketing.Notifications;
using EventTicketing.Notifications.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Core;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>
/// The Notification service hosted in-process against the test database (<c>notifications</c> schema), the compose
/// RabbitMQ and Mailpit; its logs go to <see cref="ApiFactory.Logs"/>.
/// </summary>
public sealed class NotificationsService : IAsyncDisposable
{
    private readonly IHost _host;

    private NotificationsService(IHost host) => _host = host;

    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Starts the service and returns once its bus is connected, so the <c>ticket-purchased</c> queue is bound to the
    /// <c>TicketPurchased</c> exchange before anything is published. <paramref name="settings"/> override configuration keys.
    /// </summary>
    public static async Task<NotificationsService> StartAsync(ApiFactory factory, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = NotificationsHost.CreateBuilder(new HostApplicationBuilderSettings { EnvironmentName = ApiFactory.EnvironmentName, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = factory.Postgres.ConnectionString,
            ["ConnectionStrings:RabbitMq"] = ApiFactory.RabbitMqConnectionString,
            ["Database:MigrateOnStartup"] = "true",
        });
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        builder.Services.AddSingleton<ILogEventSink>(factory.Logs);
        builder.Services.Configure<MassTransitHostOptions>(options => options.WaitUntilStarted = true);

        var host = builder.Build();
        await NotificationsHost.InitializeDatabaseAsync(host.Services);
        await host.StartAsync();
        return new NotificationsService(host);
    }

    /// <summary>Publishes straight to RabbitMQ through this service's bus, bypassing the API.</summary>
    public Task PublishAsync(TicketPurchased message, Guid? messageId = null) =>
        Services.GetRequiredService<IBus>().Publish(message, context =>
        {
            if (messageId is { } id)
            {
                context.MessageId = id;
            }
        });

    public async Task<T> WithDbAsync<T>(Func<NotificationsDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<NotificationsDbContext>());
    }

    /// <summary>A purchase of two tickets for a recipient no other test uses.</summary>
    public static TicketPurchased SamplePurchase(DateTimeOffset now)
    {
        var reservationId = Guid.NewGuid();
        return new TicketPurchased(
            reservationId,
            Guid.NewGuid(),
            $"notify-{reservationId:N}@example.com",
            "Notify Test",
            "Sample Concert",
            now.AddDays(30),
            "Main Hall",
            [new PurchasedTicket(Guid.NewGuid(), "Floor-A-1", "0123456789ABCDEFGHJKMNPQRS"), new PurchasedTicket(Guid.NewGuid(), "Floor-A-2", "0123456789ABCDEFGHJKMNPQRT")],
            now);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
