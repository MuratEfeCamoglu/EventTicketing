using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>Draft → Published; creates one Available event seat per hall seat at its section price (AC-05.3).</summary>
public static class PublishEvent
{
    public sealed class Handler(AppDbContext db, TimeProvider clock, ILogger<Handler> logger)
    {
        public async Task<Result<EventResponse>> HandleAsync(Guid eventId, Guid userId, bool isAdmin, CancellationToken cancellationToken)
        {
            var draft = await db.Events.Include(e => e.SectionPrices).SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (draft is null)
            {
                return DomainErrors.NotFound;
            }

            if (draft.EnsureCanBeChangedBy(userId, isAdmin) is { IsSuccess: false } denied)
            {
                return denied.Error;
            }

            var hallSeats = await db.Seats.AsNoTracking().Where(s => s.HallId == draft.HallId).ToListAsync(cancellationToken);
            var published = draft.Publish(hallSeats, clock.GetUtcNow());
            if (!published.IsSuccess)
            {
                return published.Error;
            }

            db.EventSeats.AddRange(published.Value);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Event {EventId} published with {SeatCount} seats", draft.Id, published.Value.Count);
            return EventResponse.From(draft);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapPost("/events/{id:guid}/publish", async (Guid id, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, context.User.GetUserId(), context.User.IsAdmin(), cancellationToken)).ToHttpResult(TypedResults.Ok))
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
