using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EventTicketing.Api.Infrastructure.Locking;

/// <summary><c>Locking:*</c> keys (ISKELET §5.14).</summary>
public sealed class LockingOptions
{
    public const string Section = "Locking";

    public bool Enabled { get; set; } = true;

    [Range(1, 60)]
    public int TtlSeconds { get; set; } = 5;
}

/// <summary>
/// Outcome of a lock attempt. <see cref="Acquired"/> is also true when locking was skipped (disabled or Redis
/// unreachable), because correctness never depends on the lock (ISKELET §5.4). Disposing releases the held locks.
/// </summary>
public sealed class SeatLocks : IAsyncDisposable
{
    private readonly RedisSeatLockService? _owner;
    private readonly IReadOnlyList<Guid> _lockedSeatIds;
    private readonly string _token;

    private SeatLocks(RedisSeatLockService? owner, IReadOnlyList<Guid> lockedSeatIds, string token, Guid? refusedSeatId)
    {
        _owner = owner;
        _lockedSeatIds = lockedSeatIds;
        _token = token;
        RefusedSeatId = refusedSeatId;
    }

    public static SeatLocks Skipped { get; } = new(null, [], string.Empty, null);

    public bool Acquired => RefusedSeatId is null;

    /// <summary>The seat whose lock another request holds.</summary>
    public Guid? RefusedSeatId { get; }

    public ValueTask DisposeAsync() => _owner is null ? ValueTask.CompletedTask : new ValueTask(_owner.ReleaseAllAsync(_lockedSeatIds, _token));

    internal static SeatLocks Held(RedisSeatLockService owner, IReadOnlyList<Guid> seatIds, string token) => new(owner, seatIds, token, null);

    internal static SeatLocks Refused(Guid seatId) => new(null, [], string.Empty, seatId);
}

/// <summary>Seat locks taken before a hold (ISKELET §5.6 step 2).</summary>
public interface ISeatLockService
{
    Task<SeatLocks> AcquireAsync(IReadOnlyCollection<Guid> eventSeatIds);
}

/// <summary>
/// Layer 1 of ISKELET §5.4: a short technical lock per seat (<c>lock:event-seat:{id}</c>, random token, TTL
/// <c>Locking:TtlSeconds</c>). It keeps losers of a hot seat away from the database without waiting; it is not the
/// 10-minute hold (V-02). Redis errors are fail-open: the request continues under the database's protection (AC-14.3).
/// </summary>
public sealed class RedisSeatLockService(IConnectionMultiplexer redis, IOptions<LockingOptions> options, ILogger<RedisSeatLockService> logger) : ISeatLockService
{
    public static RedisKey Key(Guid eventSeatId) => $"lock:event-seat:{eventSeatId}";

    /// <summary>
    /// Takes the locks in ascending id order (no deadlock between requests with overlapping seats); on the first refusal
    /// the taken ones are released and nothing is waited for.
    /// </summary>
    public async Task<SeatLocks> AcquireAsync(IReadOnlyCollection<Guid> eventSeatIds)
    {
        if (!options.Value.Enabled)
        {
            return SeatLocks.Skipped;
        }

        if (!redis.IsConnected)
        {
            logger.LogWarning("Seat lock skipped: Redis is not connected; the database guards the hold");
            return SeatLocks.Skipped;
        }

        var token = Guid.NewGuid().ToString("N");
        var ttl = TimeSpan.FromSeconds(options.Value.TtlSeconds);
        var taken = new List<Guid>();
        try
        {
            foreach (var seatId in eventSeatIds.Order())
            {
                if (!await redis.GetDatabase().LockTakeAsync(Key(seatId), token, ttl))
                {
                    await ReleaseAllAsync(taken, token);
                    return SeatLocks.Refused(seatId);
                }

                taken.Add(seatId);
            }
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Seat lock skipped: Redis call failed; the database guards the hold");
            await ReleaseAllAsync(taken, token);
            return SeatLocks.Skipped;
        }

        return SeatLocks.Held(this, taken, token);
    }

    /// <summary>Releases one lock only if <paramref name="token"/> still owns it (AC-14.2).</summary>
    public Task<bool> ReleaseAsync(Guid eventSeatId, string token) => redis.GetDatabase().LockReleaseAsync(Key(eventSeatId), token);

    internal async Task ReleaseAllAsync(IReadOnlyList<Guid> seatIds, string token)
    {
        foreach (var seatId in seatIds)
        {
            try
            {
                await ReleaseAsync(seatId, token);
            }
            catch (RedisException exception)
            {
                // The TTL frees the lock anyway.
                logger.LogWarning(exception, "Seat lock for {EventSeatId} could not be released; it expires by its TTL", seatId);
            }
        }
    }
}
