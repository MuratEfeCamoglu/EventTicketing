using System.Diagnostics;
using System.Net;
using EventTicketing.Api.Domain;
using EventTicketing.IntegrationTests.Fixtures;
using EventTicketing.Notifications.Consumers;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace EventTicketing.IntegrationTests.Features.Notifications;

/// <summary>F-17: delivery through the outbox, inbox de-duplication and retry of the Notification service (ISKELET §5.10).</summary>
public sealed class NotificationServiceTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string ErrorQueue = TicketPurchasedConsumerDefinition.QueueName + "_error";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-17.2")]
    public async Task PayReservation_BrokerDown_Returns200AndEmailArrivesOnceBrokerIsBack()
    {
        await using var notifications = await NotificationsService.StartAsync(Factory);
        var (eventId, seatIds) = await Factory.CreateEventOnSaleAsync();
        TestUser customer;

        // "RabbitMQ stopped": this API instance points at a port nothing listens on.
        await using (var brokerDown = Factory.WithOutboxDelivery(builder =>
            builder.UseSetting("ConnectionStrings:RabbitMq", $"amqp://eventticketing:dev-only-rabbitmq@localhost:{ClosedPort.Find()}/")))
        {
            using var client = brokerDown.CreateClient();
            customer = await client.AuthenticateAsAsync(Factory, UserRole.Customer);
            var reservationId = await client.HoldOrFailAsync(eventId, seatIds[0]);

            (await client.PayAsync(reservationId)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await WithDbAsync(db => db.Set<OutboxMessage>().CountAsync(Ct))).ShouldBe(1);
        }

        // "RabbitMQ restarted": an API instance that reaches the broker delivers what waited in the outbox.
        var restarted = Stopwatch.GetTimestamp();
        await using var brokerUp = Factory.WithOutboxDelivery();
        _ = brokerUp.Services;

        (await Mailpit.WaitForMessagesToAsync(customer.Email, 1, DeliveryTimeout, Ct)).ShouldHaveSingleItem();
        Stopwatch.GetElapsedTime(restarted).ShouldBeLessThan(DeliveryTimeout);
        (await WithDbAsync(db => db.Set<OutboxMessage>().CountAsync(Ct))).ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-17.3")]
    public async Task Consume_SameMessageIdTwice_SendsOneEmail()
    {
        await using var notifications = await NotificationsService.StartAsync(Factory);
        var purchase = NotificationsService.SamplePurchase(Factory.Clock.GetUtcNow());
        var messageId = NewId.NextGuid();

        await notifications.PublishAsync(purchase, messageId);
        await Mailpit.WaitForMessagesToAsync(purchase.Email, 1, DeliveryTimeout, Ct);
        var first = await WaitForInboxAsync(notifications, messageId, state => state.Consumed is not null);

        await notifications.PublishAsync(purchase, messageId);

        // The redelivered copy reaches the inbox (its receive count grows) but not the consumer.
        await WaitForInboxAsync(notifications, messageId, state => state.ReceiveCount > first.ReceiveCount);
        (await Mailpit.MessagesToAsync(purchase.Email, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-17.4")]
    public async Task Consume_ConsumerKeepsFailing_RetriesThreeTimesThenMovesToErrorQueue()
    {
        await RabbitMqManagement.PurgeAsync(ErrorQueue, Ct);
        await using var notifications = await NotificationsService.StartAsync(Factory, new Dictionary<string, string?> { ["Smtp:Port"] = ClosedPort.Find().ToString() });
        var purchase = NotificationsService.SamplePurchase(Factory.Clock.GetUtcNow());
        var published = Stopwatch.GetTimestamp();

        await notifications.PublishAsync(purchase);

        var faulted = (await Eventually.WaitForAsync(
                () => RabbitMqManagement.TakeAsync(ErrorQueue, 10, Ct),
                messages => messages.Any(m => m.Payload.Contains(purchase.ReservationId.ToString(), StringComparison.Ordinal)),
                DeliveryTimeout + DeliveryTimeout,
                "the message in the error queue"))
            .Single(m => m.Payload.Contains(purchase.ReservationId.ToString(), StringComparison.Ordinal));

        // 1 s + 5 s + 15 s between the four attempts.
        Stopwatch.GetElapsedTime(published).ShouldBeGreaterThanOrEqualTo(TicketPurchasedConsumerDefinition.RetryIntervals.Aggregate(TimeSpan.Zero, (sum, interval) => sum + interval));
        faulted.Headers.GetProperty("MT-Fault-RetryCount").GetInt32().ShouldBe(3);
        Factory.Logs.Events.Count(e => e.Level == LogEventLevel.Error
            && e.Properties.TryGetValue("ReservationId", out var value)
            && value.ToString().Contains(purchase.ReservationId.ToString(), StringComparison.Ordinal)).ShouldBe(4);
    }

    private static async Task<InboxState> WaitForInboxAsync(NotificationsService notifications, Guid messageId, Func<InboxState, bool> done) =>
        (await Eventually.WaitForAsync(
            () => notifications.WithDbAsync(db => db.Set<InboxState>().AsNoTracking().SingleOrDefaultAsync(i => i.MessageId == messageId, Ct)),
            state => state is not null && done(state),
            DeliveryTimeout,
            "the inbox state of the message"))!;
}
