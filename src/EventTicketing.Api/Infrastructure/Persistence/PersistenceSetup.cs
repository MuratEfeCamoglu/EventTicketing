using EventTicketing.Api.Common;
using EventTicketing.Api.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Infrastructure.Persistence;

public static class PersistenceSetup
{
    public static WebApplicationBuilder AddPersistence(this WebApplicationBuilder builder)
    {
        var isProduction = builder.Environment.IsProduction();

        builder.Services.AddOptions<ConnectionStringOptions>()
            .BindConfiguration(ConnectionStringOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => !isProduction || !DevOnlyValues.Contains(o.Postgres), DevOnlyValues.RejectionMessage)
            .ValidateOnStart();
        builder.Services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.Section)
            .ValidateOnStart();
        builder.Services.AddOptions<SeedOptions>()
            .BindConfiguration(SeedOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => !isProduction || !DevOnlyValues.Contains(o.AdminPassword), DevOnlyValues.RejectionMessage)
            .ValidateOnStart();

        builder.Services.AddDbContext<AppDbContext>((services, options) => options
            .UseNpgsql(services.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Postgres)
            .UseSnakeCaseNamingConvention());

        builder.Services.AddScoped<DatabaseInitializer>();

        return builder;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}
