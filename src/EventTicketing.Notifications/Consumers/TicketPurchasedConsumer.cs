using EventTicketing.Contracts;
using EventTicketing.Notifications.Email;
using EventTicketing.Notifications.Observability;
using EventTicketing.Notifications.Persistence;
using MassTransit;

namespace EventTicketing.Notifications.Consumers;

/// <summary>Sends the ticket e-mail with one QR PNG per ticket (F-12). A failure is logged and rethrown, so the retry policy applies.</summary>
public sealed class TicketPurchasedConsumer(IEmailSender email, ILogger<TicketPurchasedConsumer> logger) : IConsumer<TicketPurchased>
{
    public async Task Consume(ConsumeContext<TicketPurchased> context)
    {
        var purchase = context.Message;
        try
        {
            await email.SendAsync(TicketEmail.Create(purchase), context.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ticket e-mail for reservation {ReservationId} of user {UserId} could not be sent", purchase.ReservationId, purchase.UserId);
            throw;
        }

        logger.LogInformation("Ticket e-mail for reservation {ReservationId} sent to {Email}", purchase.ReservationId, LogMasking.MaskEmail(purchase.Email));
    }
}

/// <summary>Queue <c>ticket-purchased</c>; 3 retries at 1 s, 5 s and 15 s, then <c>ticket-purchased_error</c> (AC-17.4); inbox (AC-17.3).</summary>
public sealed class TicketPurchasedConsumerDefinition : ConsumerDefinition<TicketPurchasedConsumer>
{
    public const string QueueName = "ticket-purchased";

    public static readonly TimeSpan[] RetryIntervals = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    public TicketPurchasedConsumerDefinition() => EndpointName = QueueName;

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<TicketPurchasedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        // Retry outside the inbox, so every attempt runs in a fresh inbox transaction.
        endpointConfigurator.UseMessageRetry(retry => retry.Intervals(RetryIntervals));
        endpointConfigurator.UseEntityFrameworkOutbox<NotificationsDbContext>(context);
    }
}
