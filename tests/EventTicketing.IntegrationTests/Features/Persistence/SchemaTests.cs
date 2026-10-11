using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.IntegrationTests.Features.Persistence;

public sealed class SchemaTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>Constraint and index names from ISKELET §6, created by the migrations on an empty database.</summary>
    public static readonly TheoryData<string> IskeletIndexes =
    [
        "ux_users_email",
        "ux_refresh_tokens_token_hash",
        "ix_refresh_tokens_family_id",
        "ix_venues_city",
        "ux_halls_venue_id_name",
        "ux_seats_hall_section_row_number",
        "ix_events_status_starts_at",
        "ix_events_organizer_id",
        "ux_event_seats_event_id_seat_id",
        "ix_event_seats_event_id_status",
        "ix_event_seats_hold_reservation_id",
        "ix_reservations_user_event_pending",
        "ix_reservations_status_expires_at",
        "ux_payments_idempotency_key",
        "ux_payments_reservation_succeeded",
        "ux_tickets_event_seat_id",
        "ux_tickets_code",
        "ix_tickets_user_id",
    ];

    [Fact]
    public async Task Migrations_EmptyDatabase_AreAllApplied()
    {
        var (applied, pending) = await WithDbAsync(async db =>
            (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken),
             await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)));

        applied.ShouldContain(m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
        pending.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(IskeletIndexes))]
    public async Task Migrations_EmptyDatabase_CreateIskeletIndex(string indexName)
    {
        var count = await WithDbAsync(db => db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND indexname = {indexName}")
            .SingleAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(1);
    }

    [Theory]
    [InlineData("ix_reservations_user_event_pending", "WHERE ((status)::text = 'Pending'::text)")]
    [InlineData("ux_payments_reservation_succeeded", "WHERE ((status)::text = 'Succeeded'::text)")]
    public async Task Migrations_EmptyDatabase_CreatePartialUniqueIndex(string indexName, string expectedFilter)
    {
        var definition = await WithDbAsync(db => db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND indexname = {indexName}")
            .SingleAsync(TestContext.Current.CancellationToken));

        definition.ShouldStartWith("CREATE UNIQUE INDEX");
        definition.ShouldEndWith(expectedFilter);
    }
}
