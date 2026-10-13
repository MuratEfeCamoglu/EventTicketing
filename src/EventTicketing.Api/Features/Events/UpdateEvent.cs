using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Full replacement of the event fields; a Published event accepts only title and description changes (AC-05.4).</summary>
public static class UpdateEvent
{
    public sealed record Request(
        Guid? HallId,
        string Title,
        string? Description,
        EventCategory? Category,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt,
        IReadOnlyList<SectionPriceInput> SectionPrices) : IEventFields;

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => Include(new EventFieldsValidator());
    }

    public sealed class Handler(AppDbContext db, EventCacheInvalidator cache, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<EventResponse>> HandleAsync(Guid eventId, Guid userId, bool isAdmin, Request request, CancellationToken cancellationToken)
        {
            var existing = await db.Events.Include(e => e.SectionPrices).SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (existing is null)
            {
                return DomainErrors.NotFound;
            }

            if (existing.EnsureCanBeChangedBy(userId, isAdmin) is { IsSuccess: false } denied)
            {
                return denied.Error;
            }

            var now = clock.GetUtcNow();
            var startsAt = request.StartsAt!.Value.ToUniversalTime();
            if (existing.Status == EventStatus.Draft)
            {
                // Only a Draft gets a new schedule, which must be in the future; a Published event keeps its start time.
                if (startsAt <= now)
                {
                    return DomainErrors.Validation("startsAt", "The event must start in the future.");
                }

                if (await db.ValidateHallAsync(request, cancellationToken) is { } invalidHall)
                {
                    return invalidHall;
                }
            }

            var updated = existing.Update(
                request.HallId!.Value,
                request.Title.Trim(),
                request.Description?.Trim() ?? string.Empty,
                request.Category!.Value,
                startsAt,
                request.EndsAt!.Value.ToUniversalTime(),
                request.NormalizedPrices(),
                now);
            if (!updated.IsSuccess)
            {
                return updated.Error;
            }

            await db.SaveChangesAsync(cancellationToken);
            await cache.EventChangedAsync(existing.Id, cancellationToken);
            logger.LogInformation("Event {EventId} updated", existing.Id);
            return EventResponse.From(existing);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPut("/events/{id:guid}", async (Guid id, Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), request, cancellationToken)).ToHttpResult(TypedResults.Ok))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
