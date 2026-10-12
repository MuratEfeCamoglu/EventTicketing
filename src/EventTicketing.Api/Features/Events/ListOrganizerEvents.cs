using EventTicketing.Api.Common;
using EventTicketing.Api.Domain;
using EventTicketing.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Features.Events;

/// <summary>An organizer sees all of their own events in every state; an Admin sees every event (AC-06.5).</summary>
public static class ListOrganizerEvents
{
    public sealed record Request(int? Page, int? PageSize);

    public sealed record Item(
        Guid Id,
        Guid OrganizerId,
        string Title,
        EventCategory Category,
        EventStatus Status,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        string VenueName,
        string City,
        string HallName,
        decimal? MinPrice);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => this.AddPagingRules(r => r.Page, r => r.PageSize);
    }

    public sealed class Handler(AppDbContext db)
    {
        public Task<PagedResponse<Item>> HandleAsync(Guid userId, bool isAdmin, Request request, CancellationToken cancellationToken)
        {
            var events = db.Events.AsNoTracking();
            if (!isAdmin)
            {
                events = events.Where(e => e.OrganizerId == userId);
            }

            return (
                    from e in events
                    join h in db.Halls on e.HallId equals h.Id
                    join v in db.Venues on h.VenueId equals v.Id
                    orderby e.StartsAt, e.Id
                    select new Item(
                        e.Id,
                        e.OrganizerId,
                        e.Title,
                        e.Category,
                        e.Status,
                        e.StartsAt,
                        e.EndsAt,
                        v.Name,
                        v.City,
                        h.Name,
                        e.SectionPrices.Min(p => (decimal?)p.Price)))
                .ToPagedResponseAsync(request.Page, request.PageSize, cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder app) =>
            app.MapGet("/organizer/events", async ([AsParameters] Request request, Handler handler, HttpContext context, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await handler.HandleAsync(context.User.GetUserId(), context.User.IsAdmin(), request, cancellationToken)))
                .AddEndpointFilter<ValidationFilter<Request>>()
                .RequireAuthorization(Policies.OrganizerOrAdmin)
                .WithTags("Events");
    }
}
