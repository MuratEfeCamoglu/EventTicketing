namespace EventTicketing.Api.Infrastructure.Locking;

public static class LockingSetup
{
    public static WebApplicationBuilder AddSeatLocking(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<LockingOptions>()
            .BindConfiguration(LockingOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<RedisSeatLockService>();
        builder.Services.AddSingleton<ISeatLockService>(services => services.GetRequiredService<RedisSeatLockService>());
        return builder;
    }
}
