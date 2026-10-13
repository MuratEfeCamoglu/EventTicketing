using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EventTicketing.Api.Infrastructure.Caching;

public static class CachingSetup
{
    /// <summary>Prefix of every Redis key the API writes, so a shared Redis keeps environments apart.</summary>
    public const string RedisInstanceName = "eventticketing:";

    /// <summary>
    /// <c>HybridCache</c> with Redis as L2 (ISKELET §5.9). One <see cref="IConnectionMultiplexer"/> is shared by the cache
    /// and, from V2-2, the seat locks. The connection string carries <c>abortConnect=false</c>, so a Redis that is down
    /// does not stop the API: the cache falls back to L1 and the database (AC-15.4).
    /// </summary>
    public static WebApplicationBuilder AddCaching(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IConnectionMultiplexer>(services =>
            ConnectionMultiplexer.Connect(services.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Redis));

        builder.Services.AddStackExchangeRedisCache(_ => { });
        builder.Services.AddOptions<RedisCacheOptions>().Configure<IServiceProvider>((options, services) =>
        {
            options.InstanceName = RedisInstanceName;
            options.ConnectionMultiplexerFactory = () => Task.FromResult(services.GetRequiredService<IConnectionMultiplexer>());
        });

        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<EventCacheInvalidator>();
        return builder;
    }
}
