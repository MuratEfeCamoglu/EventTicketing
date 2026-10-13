using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Caching;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Deletes a Draft together with its section prices; any other state → 409 (AC-05.6).</summary>
public static class DeleteEvent
{
    public sealed class Handler(AppDbContext db, EventCacheInvalidator cache, ILogger<Handler> logger)
    {
        public async Task<Result> HandleAsync(Guid eventId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
        {
            var draft = await db.Events.SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (draft is null)
            {
                return DomainErrors.NotFound;
            }

            if (draft.EnsureCanBeChangedBy(userId, isAdmin) is { IsSuccess: false } denied)
            {
                return denied;
            }

            if (draft.EnsureDeletable() is { IsSuccess: false } notDraft)
            {
                return notDraft;
            }

            db.Events.Remove(draft);
            await db.SaveChangesAsync(cancellationToken);
            await cache.EventChangedAsync(eventId, cancellationToken);

            logger.LogInformation("Draft event {EventId} deleted", eventId);
            return Result.Success();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapDelete("/events/{id:guid}", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), cancellationToken)).ToHttpResult(TypedResults.NoContent))
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
