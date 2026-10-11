using EventTicketing.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventTicketing.Api.Infrastructure.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.Property(v => v.Name).HasMaxLength(200);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.HasIndex(v => v.City).HasDatabaseName("ix_venues_city");
    }
}

internal sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(100);
        builder.HasOne<Venue>().WithMany().HasForeignKey(h => h.VenueId);
        builder.HasMany(h => h.Seats).WithOne().HasForeignKey(s => s.HallId);
        builder.HasIndex(h => new { h.VenueId, h.Name }).IsUnique().HasDatabaseName("ux_halls_venue_id_name");
    }
}

internal sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.Property(s => s.Section).HasMaxLength(50);
        builder.Property(s => s.Row).HasMaxLength(10);
        builder.HasIndex(s => new { s.HallId, s.Section, s.Row, s.Number }).IsUnique().HasDatabaseName("ux_seats_hall_section_row_number");
    }
}
