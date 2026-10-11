using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Serilog.Core;

namespace EventTicketing.IntegrationTests.Fixtures;

/// <summary>Single host for the whole <c>Api</c> collection: one test database, a shared <see cref="FakeTimeProvider"/> and a log sink.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string EnvironmentName = "Testing";
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Dev-only-Passw0rd";

    public PostgresFixture Postgres { get; } = new();

    // Starts at the real UTC time so tokens and timestamps look realistic (ISKELET §3.2).
    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    public CollectingLogSink Logs { get; } = new();

    public async ValueTask InitializeAsync() => await Postgres.InitializeAsync();

    public Task ResetAsync()
    {
        Logs.Clear();
        return Postgres.ResetAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Postgres", Postgres.ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:SampleData", "false");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("MockPayment:LatencyMs", "0");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ILogEventSink>(Logs);
        });
    }
}
