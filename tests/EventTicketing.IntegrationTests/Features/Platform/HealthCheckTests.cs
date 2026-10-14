using System.Net;
using EventTicketing.IntegrationTests.Fixtures;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EventTicketing.IntegrationTests.Features.Platform;

/// <summary>AC-20.3 (bonus).</summary>
public sealed class HealthCheckTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly TimeSpan BusConnectTimeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-20.3")]
    public async Task Ready_AllDependenciesReachable_Returns200()
    {
        // The bus connects to RabbitMQ in the background after the host starts.
        var status = await Eventually.WaitForAsync(
            async () => (await Client.GetAsync("/health/ready", Ct)).StatusCode,
            code => code == HttpStatusCode.OK,
            BusConnectTimeout,
            "readiness once the bus is connected");

        status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-20.3")]
    public async Task Live_DependenciesDown_Returns200()
    {
        await using var api = WithRedisAndRabbitMqDown();
        using var client = api.CreateClient();

        (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "AC-20.3")]
    public async Task Ready_PostgresUnreachable_Returns503()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(Factory.Postgres.ConnectionString) { Port = ClosedPort.Find(), Timeout = 2 }.ConnectionString;
        await using var api = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                var postgres = options.Registrations.Single(r => r.Name == "postgres");
                options.Registrations.Remove(postgres);
                options.Registrations.Add(new HealthCheckRegistration(
                    postgres.Name, _ => new NpgSqlHealthCheck(new NpgSqlHealthCheckOptions(unreachable)), postgres.FailureStatus, postgres.Tags));
            })));
        using var client = api.CreateClient();

        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ShouldBeUnhealthyAsync(api, "postgres");
    }

    [Fact]
    [Trait("AC", "AC-20.3")]
    public async Task Ready_RedisUnreachable_Returns503()
    {
        await using var api = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Redis", $"localhost:{ClosedPort.Find()},abortConnect=false,connectTimeout=1000,syncTimeout=1000"));
        using var client = api.CreateClient();

        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ShouldBeUnhealthyAsync(api, "redis");
    }

    [Fact]
    [Trait("AC", "AC-20.3")]
    public async Task Ready_RabbitMqUnreachable_Returns503()
    {
        await using var api = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:RabbitMq", $"amqp://eventticketing:dev-only-rabbitmq@localhost:{ClosedPort.Find()}/"));
        using var client = api.CreateClient();

        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var report = await api.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Ct);
        (report.Entries["postgres"].Status, report.Entries["redis"].Status).ShouldBe((HealthStatus.Healthy, HealthStatus.Healthy));
    }

    /// <summary>
    /// Proves the cause of the 503: <paramref name="check"/> is unhealthy while the database and Redis checks that do not
    /// depend on it report on their own (the bus may still be connecting in a fresh host, so it is not asserted).
    /// </summary>
    private static async Task ShouldBeUnhealthyAsync(WebApplicationFactory<Program> api, string check)
    {
        var report = await api.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(r => r.Name is "postgres" or "redis", Ct);
        report.Entries[check].Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries.Where(e => e.Key != check).ShouldAllBe(e => e.Value.Status == HealthStatus.Healthy);
    }

    private WebApplicationFactory<Program> WithRedisAndRabbitMqDown() =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Redis", $"localhost:{ClosedPort.Find()},abortConnect=false,connectTimeout=1000,syncTimeout=1000");
            builder.UseSetting("ConnectionStrings:RabbitMq", $"amqp://eventticketing:dev-only-rabbitmq@localhost:{ClosedPort.Find()}/");
        });
}
