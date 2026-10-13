using EventTicketing.Api.Infrastructure.Locking;
using EventTicketing.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EventTicketing.IntegrationTests.Features.Reservations;

public sealed class SeatLockTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("AC", "AC-14.2")]
    public async Task ReleaseAsync_ForeignToken_ReturnsFalseAndKeepsLock()
    {
        var locks = Factory.Services.GetRequiredService<RedisSeatLockService>();
        var redis = Factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var seatId = Guid.CreateVersion7();

        await using (var held = await locks.AcquireAsync([seatId]))
        {
            held.Acquired.ShouldBeTrue();

            (await locks.ReleaseAsync(seatId, Guid.NewGuid().ToString("N"))).ShouldBeFalse();

            (await redis.KeyExistsAsync(RedisSeatLockService.Key(seatId))).ShouldBeTrue();
            var second = await locks.AcquireAsync([seatId]);
            (second.Acquired, second.RefusedSeatId).ShouldBe((false, seatId));
        }

        (await redis.KeyExistsAsync(RedisSeatLockService.Key(seatId))).ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-14.2")]
    public async Task AcquireAsync_OneSeatTaken_ReleasesTheOthersItTook()
    {
        var locks = Factory.Services.GetRequiredService<RedisSeatLockService>();
        var redis = Factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        Guid[] seats = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        var ordered = seats.Order().ToArray();
        await using var blocker = await locks.AcquireAsync([ordered[2]]);

        var attempt = await locks.AcquireAsync(seats);

        (attempt.Acquired, attempt.RefusedSeatId).ShouldBe((false, ordered[2]));
        (await redis.KeyExistsAsync(RedisSeatLockService.Key(ordered[0]))).ShouldBeFalse();
        (await redis.KeyExistsAsync(RedisSeatLockService.Key(ordered[1]))).ShouldBeFalse();
    }
}
