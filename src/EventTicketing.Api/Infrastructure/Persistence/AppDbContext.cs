using EventTicketing.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Event = EventTicketing.Api.Domain.Event;

namespace EventTicketing.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Hall> Halls => Set<Hall>();

    public DbSet<Seat> Seats => Set<Seat>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventSectionPrice> EventSectionPrices => Set<EventSectionPrice>();

    public DbSet<EventSeat> EventSeats => Set<EventSeat>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationSeat> ReservationSeats => Set<ReservationSeat>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Ids are created in the domain (Guid.CreateVersion7), never by the database.
            foreach (var key in entityType.GetKeys().SelectMany(k => k.Properties).Where(p => p.ClrType == typeof(Guid)))
            {
                key.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }

            // Domain data is never physically deleted; only a Draft event takes its section prices with it (ISKELET §6).
            foreach (var foreignKey in entityType.GetForeignKeys().Where(fk => fk.DeclaringEntityType.ClrType != typeof(EventSectionPrice)))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        // MassTransit EF outbox tables (ISKELET §5.10); added after the loop above so they keep MassTransit's own keys and
        // delete behaviour.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
        configurationBuilder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EventCategory>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EventStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EventSeatStatus>().HaveConversion<string>().HaveMaxLength(10);
        configurationBuilder.Properties<ReservationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
    }
}
