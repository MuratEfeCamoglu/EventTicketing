using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Notifications.Persistence;

/// <summary>MassTransit inbox/outbox tables of the Notification service, in their own schema (ISKELET §5.10, §6).</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
