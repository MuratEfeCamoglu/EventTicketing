using System.Security.Cryptography;
using EventTicketing.Api.Features.Reservations;
using EventTicketing.Api.Infrastructure.Persistence;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    /// <summary>HybridCache wildcard: invalidating it invalidates every entry.</summary>
    private const string HybridCacheAllTags = "*";

    private static readonly Guid RunId = Guid.NewGuid();

    /// <summary>Generated per test run (ISKELET §5.14).</summary>
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>
    /// Local Redis from <c>EVENTTICKETING_TEST_REDIS</c> (default <c>localhost:6379</c>) with the fail-fast options of
    /// ISKELET §5.14.
    /// </summary>
    public static string RedisConnectionString { get; } =
        $"{Environment.GetEnvironmentVariable("EVENTTICKETING_TEST_REDIS") ?? "localhost:6379"},abortConnect=false,connectTimeout=1000,syncTimeout=1000";

    /// <summary>Local RabbitMQ from <c>EVENTTICKETING_TEST_RABBITMQ</c> (default: the compose broker with its dev-only user).</summary>
    public static Uri RabbitMqServer { get; } =
        new(Environment.GetEnvironmentVariable("EVENTTICKETING_TEST_RABBITMQ") ?? "amqp://eventticketing:dev-only-rabbitmq@localhost:5672/");

    /// <summary>
    /// A virtual host of this test run, created before and deleted after it: test messages never meet a running development
    /// Notification service, nor leftovers of another run.
    /// </summary>
    public static string RabbitMqVirtualHost { get; } = $"eventticketing-test-{RunId:N}";

    public static string RabbitMqConnectionString { get; } =
        new UriBuilder(RabbitMqServer) { Path = "/" + Uri.EscapeDataString(RabbitMqVirtualHost) }.Uri.ToString();

    /// <summary>Counts SQL commands, so a test can prove a read was served from the cache (AC-15.1).</summary>
    public DbCommandCounter DbCommands { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await RabbitMqManagement.CreateVirtualHostAsync(RabbitMqVirtualHost);
    }

    /// <summary>Empty database, empty log sink and a cache in which every earlier entry is invalid.</summary>
    public async Task ResetAsync()
    {
        Logs.Clear();
        await Postgres.ResetAsync();
        await Services.GetRequiredService<HybridCache>().RemoveByTagAsync(HybridCacheAllTags);

        // HybridCache stamps entries and tag invalidations with the (fake) TimeProvider; an entry created at the very
        // instant of the invalidation would count as invalidated, so time moves on by a tick.
        Clock.Advance(TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// A host of this factory with the outbox delivery service running, so <c>TicketPurchased</c> reaches RabbitMQ (F-17).
    /// The shared host keeps it off; see <see cref="ConfigureWebHost"/>.
    /// </summary>
    public WebApplicationFactory<Program> WithOutboxDelivery(Action<IWebHostBuilder>? configure = null) =>
        WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services => services.AddHostedService<BusOutboxDeliveryService<AppDbContext>>());
            configure?.Invoke(builder);
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Postgres.DisposeAsync();
        await RabbitMqManagement.DeleteVirtualHostAsync(RabbitMqVirtualHost);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Postgres", Postgres.ConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", RedisConnectionString);
        builder.UseSetting("ConnectionStrings:RabbitMq", RabbitMqConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:SampleData", "false");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("MockPayment:LatencyMs", "0");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ILogEventSink>(Logs);
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(DbCommands));

            // The sweeper would run whenever a test advances the fake clock; tests call SweepOnceAsync themselves (AC-09.3).
            foreach (var sweeper in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ReservationExpiryService)).ToList())
            {
                services.Remove(sweeper);
            }

            services.AddSingleton<ReservationExpiryService>();

            // Messages stay in outbox_message, where tests can inspect them (AC-17.1); delivery to RabbitMQ is not under test.
            foreach (var delivery in services.Where(d => d.ImplementationType is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(BusOutboxDeliveryService<>)).ToList())
            {
                services.Remove(delivery);
            }

            // Keys of this test run never meet those of another run or of the development API in the same Redis.
            services.Configure<RedisCacheOptions>(options => options.InstanceName = $"eventticketing-test:{RunId}:");
        });
    }
}
