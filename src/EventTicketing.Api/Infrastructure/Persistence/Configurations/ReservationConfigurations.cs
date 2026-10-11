using EventTicketing.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventTicketing.Api.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId);
        builder.HasOne<Event>().WithMany().HasForeignKey(r => r.EventId);
        builder.HasMany(r => r.Seats).WithOne().HasForeignKey(s => s.ReservationId);

        // One active reservation per user and event, enforced by the database (ISKELET §5.4 layer 3).
        builder.HasIndex(r => new { r.UserId, r.EventId })
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_reservations_user_event_pending");
        builder.HasIndex(r => new { r.Status, r.ExpiresAt }).HasDatabaseName("ix_reservations_status_expires_at");
    }
}

internal sealed class ReservationSeatConfiguration : IEntityTypeConfiguration<ReservationSeat>
{
    public void Configure(EntityTypeBuilder<ReservationSeat> builder)
    {
        builder.HasKey(s => new { s.ReservationId, s.EventSeatId });
        builder.HasOne<EventSeat>().WithMany().HasForeignKey(s => s.EventSeatId);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.ProviderReference).HasMaxLength(100);
        builder.Property(p => p.FailureCode).HasMaxLength(50);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(p => p.ReservationId);
        builder.HasIndex(p => p.IdempotencyKey).IsUnique().HasDatabaseName("ux_payments_idempotency_key");
        builder.HasIndex(p => p.ReservationId)
            .IsUnique()
            .HasFilter("status = 'Succeeded'")
            .HasDatabaseName("ux_payments_reservation_succeeded");
    }
}

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(t => t.Code).HasMaxLength(26).IsFixedLength();
        builder.HasOne<Reservation>().WithMany().HasForeignKey(t => t.ReservationId);
        builder.HasOne<EventSeat>().WithMany().HasForeignKey(t => t.EventSeatId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
        builder.HasIndex(t => t.EventSeatId).IsUnique().HasDatabaseName("ux_tickets_event_seat_id");
        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("ux_tickets_code");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_tickets_user_id");
    }
}
