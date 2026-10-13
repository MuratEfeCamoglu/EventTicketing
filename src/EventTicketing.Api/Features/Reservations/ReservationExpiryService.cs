using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Observability;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventTicketing.Api.Features.Reservations;

/// <summary>
/// Sweeper (ISKELET §5.8, AC-09.3): turns expired Pending reservations into Expired and frees their seats. Correctness does
/// not depend on it (every read and write already applies the expiry rule); it keeps the stored state tidy. Safe to run
/// on several API instances at once: every statement re-checks <c>status = 'Pending'</c> / <c>'Held'</c>.
/// </summary>
public sealed class ReservationExpiryService(
    IServiceScopeFactory scopes,
    EventCacheInvalidator cache,
    EventTicketingMetrics metrics,
    TimeProvider clock,
    IOptions<ReservationOptions> options,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    public const int BatchSize = 500;

    /// <summary>One sweep: batches of up to 500, repeated while a batch comes back full. Returns the reservations expired.</summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        int batch;
        do
        {
            batch = await SweepBatchAsync(cancellationToken);
            total += batch;
        }
        while (batch == BatchSize);

        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ExpirySweepIntervalSeconds), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The next tick retries; reads and writes stay correct meanwhile (lazy expiry).
                logger.LogError(exception, "Reservation expiry sweep failed");
            }
        }
    }

    private async Task<int> SweepBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();

        var expired = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Take(BatchSize)
            .Select(r => new { r.Id, r.EventId })
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        var ids = expired.Select(r => r.Id).ToList();
        var holdIds = ids.Select(id => (Guid?)id).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.Reservations
            .Where(r => ids.Contains(r.Id) && r.Status == ReservationStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired).SetProperty(r => r.UpdatedAt, now), cancellationToken);

        // A seat that moved on to another reservation no longer points at these ids and is left alone.
        await db.EventSeats
            .Where(s => holdIds.Contains(s.HoldReservationId) && s.Status == EventSeatStatus.Held)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, EventSeatStatus.Available).SetProperty(x => x.HoldReservationId, (Guid?)null).SetProperty(x => x.HeldUntil, (DateTimeOffset?)null),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        metrics.ReservationsExpired(updated);
        foreach (var eventId in expired.Select(r => r.EventId).Distinct())
        {
            await cache.SeatsChangedAsync(eventId, cancellationToken);
        }

        logger.LogInformation("Expiry sweep expired {ReservationCount} reservations", updated);
        return expired.Count;
    }
}
