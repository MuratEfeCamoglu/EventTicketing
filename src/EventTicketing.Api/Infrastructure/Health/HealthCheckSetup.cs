using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EventTicketing.Api.Infrastructure.Health;

/// <summary>
/// <c>/health/live</c> and <c>/health/ready</c> (bonus AC-20.3). Readiness covers PostgreSQL, Redis and RabbitMQ; the
/// RabbitMQ check is the bus health check that <c>AddMassTransit</c> registers with the <c>ready</c> tag.
/// </summary>
public static class HealthCheckSetup
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    private const string ReadyTag = "ready";

    public static WebApplicationBuilder AddHealthChecks(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(services => services.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Postgres, name: "postgres", tags: [ReadyTag])
            .AddRedis(services => services.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: [ReadyTag]);
        return builder;
    }

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness: the process answers; no dependency is checked.
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

        // Readiness: any dependency that is not fully healthy makes the instance not ready.
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        }).AllowAnonymous();

        return app;
    }
}
