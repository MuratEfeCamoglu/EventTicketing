using EventTicketing.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Infrastructure.Persistence.Seed;

/// <summary>Runs at startup: optional migration, the Admin user (AC-03.5) and optional sample data (ISKELET §5.14).</summary>
public sealed class DatabaseInitializer(
    AppDbContext db,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<SeedOptions> seedOptions,
    IPasswordHasher<User> passwordHasher,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger)
{
    public const string OrganizerEmail = "organizer@example.com";
    public const string CustomerEmail = "customer@example.com";
    public const int SampleEventStartsInDays = 30;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (databaseOptions.Value.MigrateOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await SeedAdminAsync(cancellationToken);

        if (seedOptions.Value.SampleData)
        {
            await SeedSampleDataAsync(cancellationToken);
        }
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        var email = User.NormalizeEmail(seedOptions.Value.AdminEmail);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Seed:AdminEmail belongs to an existing non-admin user.");
        }

        var admin = CreateUser(email, "Administrator", UserRole.Admin, clock.GetUtcNow());
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded admin user {UserId}", admin.Id);
    }

    private async Task SeedSampleDataAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == OrganizerEmail, cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var organizer = CreateUser(OrganizerEmail, "Sample Organizer", UserRole.Organizer, now);
        var customer = CreateUser(CustomerEmail, "Sample Customer", UserRole.Customer, now);
        var venue = new Venue("Sample Arena", "Istanbul", "1 Sample Street, Istanbul", now);

        // 2 sections × 5 rows × 10 seats = 100 seats.
        var hall = new Hall(venue.Id, "Main Hall", now);
        foreach (var row in new[] { "A", "B", "C", "D", "E" })
        {
            hall.AddRow("Floor", row, 10);
            hall.AddRow("Balcony", row, 10);
        }

        var startsAt = now.AddDays(SampleEventStartsInDays);
        var sampleEvent = new Event(
            organizer.Id,
            hall.Id,
            "Sample Concert",
            "Seeded sample event for local development.",
            EventCategory.Concert,
            startsAt,
            startsAt.AddHours(3),
            [("Floor", 750m), ("Balcony", 450m)],
            now);
        var eventSeats = sampleEvent.Publish(hall.Seats, now).Value;

        db.AddRange(organizer, customer, venue, hall, sampleEvent);
        db.EventSeats.AddRange(eventSeats);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded sample event {EventId} with {SeatCount} seats", sampleEvent.Id, eventSeats.Count);
    }

    private User CreateUser(string email, string fullName, UserRole role, DateTimeOffset now)
    {
        var user = new User(email, fullName, role, now);
        user.SetPasswordHash(passwordHasher.HashPassword(user, seedOptions.Value.AdminPassword));
        return user;
    }
}
