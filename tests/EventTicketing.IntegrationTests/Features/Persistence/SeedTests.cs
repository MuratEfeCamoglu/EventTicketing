using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence.Seed;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.IntegrationTests.Features.Persistence;

public sealed class SeedTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-03.5")]
    public async Task Startup_RunTwice_CreatesExactlyOneAdmin()
    {
        (await CountAdminsAsync()).ShouldBe(0);

        StartHost();
        StartHost();

        var admins = await WithDbAsync(db => db.Users.AsNoTracking().Where(u => u.Role == UserRole.Admin).ToListAsync(TestContext.Current.CancellationToken));
        var admin = admins.ShouldHaveSingleItem();
        admin.Email.ShouldBe(ApiFactory.AdminEmail);
        new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash, ApiFactory.AdminPassword)
            .ShouldNotBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public async Task Startup_SampleDataEnabledTwice_SeedsOnePublishedEventWith100Seats()
    {
        StartHost(b => b.UseSetting("Seed:SampleData", "true"));
        StartHost(b => b.UseSetting("Seed:SampleData", "true"));

        var events = await WithDbAsync(db => db.Events.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        var sample = events.ShouldHaveSingleItem();
        sample.Status.ShouldBe(EventStatus.Published);
        sample.StartsAt.ShouldBe(Factory.Clock.GetUtcNow().AddDays(DatabaseInitializer.SampleEventStartsInDays), TimeSpan.FromSeconds(1));
        (await WithDbAsync(db => db.EventSeats.CountAsync(s => s.EventId == sample.Id && s.Status == EventSeatStatus.Available, TestContext.Current.CancellationToken)))
            .ShouldBe(100);
        (await WithDbAsync(db => db.Users.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(3);
    }

    [Fact]
    public void Startup_ProductionWithDevOnlySecrets_FailsOptionsValidation()
    {
        using var production = Factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        var exception = Should.Throw<OptionsValidationException>(() => production.CreateClient());

        exception.Failures.ShouldContain(f => f.Contains("dev-only-", StringComparison.Ordinal));
    }

    private void StartHost(Action<IWebHostBuilder>? configure = null)
    {
        using var host = Factory.WithWebHostBuilder(b => configure?.Invoke(b));
        using var client = host.CreateClient();
    }

    private Task<int> CountAdminsAsync() =>
        WithDbAsync(db => db.Users.CountAsync(u => u.Role == UserRole.Admin, TestContext.Current.CancellationToken));
}
