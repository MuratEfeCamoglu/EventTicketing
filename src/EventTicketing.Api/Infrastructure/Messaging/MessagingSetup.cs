using EventTicketing.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Infrastructure.Messaging;

public static class MessagingSetup
{
    /// <summary>
    /// MassTransit over RabbitMQ with the EF Core bus outbox (ISKELET §5.10). The scoped <see cref="IPublishEndpoint"/>
    /// writes to <c>outbox_message</c> through <see cref="AppDbContext"/>, so a message is stored only if the transaction that
    /// published it commits (AC-17.1); the outbox delivery service forwards stored messages to RabbitMQ afterwards.
    /// </summary>
    public static WebApplicationBuilder AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(context.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.RabbitMq));
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
