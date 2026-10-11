using EventTicketing.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventTicketing.Api.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OrganizerId);
        builder.HasOne<Hall>().WithMany().HasForeignKey(e => e.HallId);
        builder.HasMany(e => e.SectionPrices).WithOne().HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.Status, e.StartsAt }).HasDatabaseName("ix_events_status_starts_at");
        builder.HasIndex(e => e.OrganizerId).HasDatabaseName("ix_events_organizer_id");
    }
}

internal sealed class EventSectionPriceConfiguration : IEntityTypeConfiguration<EventSectionPrice>
{
    public void Configure(EntityTypeBuilder<EventSectionPrice> builder)
    {
        builder.HasKey(p => new { p.EventId, p.Section });
        builder.Property(p => p.Section).HasMaxLength(50);
    }
}

internal sealed class EventSeatConfiguration : IEntityTypeConfiguration<EventSeat>
{
    public void Configure(EntityTypeBuilder<EventSeat> builder)
    {
        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.HasOne<Event>().WithMany().HasForeignKey(s => s.EventId);
        builder.HasOne<Seat>().WithMany().HasForeignKey(s => s.SeatId);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(s => s.HoldReservationId);
        builder.HasIndex(s => new { s.EventId, s.SeatId }).IsUnique().HasDatabaseName("ux_event_seats_event_id_seat_id");
        builder.HasIndex(s => new { s.EventId, s.Status }).HasDatabaseName("ix_event_seats_event_id_status");
        builder.HasIndex(s => s.HoldReservationId).HasDatabaseName("ix_event_seats_hold_reservation_id");
    }
}
